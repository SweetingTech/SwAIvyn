param(
    [string]$StackName = $env:STACK_NAME
)

# Idempotent Temporal database bootstrap using containers
function Setup-TemporalSchema {
    param(
        [string]$StackName = $env:STACK_NAME,
        [string]$PostgresHost = 'swai-db',
        [int]$PostgresPort = 5432
    )

    if (-not $env:POSTGRES_PASSWORD) {
        throw "POSTGRES_PASSWORD must be set (from .env) before bootstrapping Temporal schema."
    }

    Write-Host "[setup] Ensuring Temporal databases exist on Postgres..." -ForegroundColor Cyan

    # Create databases temporal and temporal_visibility if they don't exist
    $networkName = "${StackName}_default"
    $createDbScript = @"
psql -h $PostgresHost -U postgres -p $PostgresPort -tAc "SELECT 1 FROM pg_database WHERE datname='temporal'" | grep -q 1 || psql -h $PostgresHost -U postgres -p $PostgresPort -c "CREATE DATABASE temporal";
psql -h $PostgresHost -U postgres -p $PostgresPort -tAc "SELECT 1 FROM pg_database WHERE datname='temporal_visibility'" | grep -q 1 || psql -h $PostgresHost -U postgres -p $PostgresPort -c "CREATE DATABASE temporal_visibility";
"@
    $pgCmd = @('run','--rm','--network',$networkName,'-e',"PGPASSWORD=$($env:POSTGRES_PASSWORD)",'postgres:16','bash','-lc',$createDbScript)
    & docker @pgCmd | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to ensure Temporal databases exist." }

    Write-Host "[setup] Applying/Updating Temporal schemas (idempotent)..." -ForegroundColor Cyan

    # Apply schema for temporal and visibility (safe to re-run)
    $schemaScript = @"
set -e
for DB in temporal temporal_visibility; do
  if [ "\$DB" = "temporal" ]; then
    DIR=/etc/temporal/schema/postgresql/v12/temporal/versioned
  else
    DIR=/etc/temporal/schema/postgresql/v12/visibility/versioned
  fi
  temporal-sql-tool \
    --plugin postgres \
    --ep '$PostgresHost' \
    -u 'postgres' \
    -p '$PostgresPort' \
    --db "\$DB" setup-schema -v 0.0 || true
  temporal-sql-tool \
    --plugin postgres \
    --ep '$PostgresHost' \
    -u 'postgres' \
    -p '$PostgresPort' \
    --db "\$DB" update-schema -d "\$DIR"
  echo "Schema updated for \$DB"
done
"@

    $toolCmd = @('run','--rm','--network',$networkName,'-e',"POSTGRES_PWD=$($env:POSTGRES_PASSWORD)",'temporalio/admin-tools:1.23','bash','-lc', $schemaScript)
    & docker @toolCmd | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to update Temporal DB schemas." }

    Write-Host "[setup] Temporal databases and schemas are ready." -ForegroundColor Green
}



# ---------------------------
# GPU + WSL + Docker helpers
# ---------------------------
function Test-NvidiaSmi {
    try { nvidia-smi | Out-String } catch { return $null }
}

function Enable-WSLFeatures {
    Write-Host "[gpu-setup] Enabling Windows features: WSL + VirtualMachinePlatform (requires admin)" -ForegroundColor Cyan
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
    if (-not $isAdmin) {
        Write-Warning "Please re-run PowerShell as Administrator and execute:"
        Write-Host "  dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart" -ForegroundColor Yellow
        Write-Host "  dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart" -ForegroundColor Yellow
        return
    }
    & dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart | Out-Null
    & dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart | Out-Null
}

function Ensure-WSLDefaultV2 {
    Write-Host "[gpu-setup] Setting WSL default version to 2" -ForegroundColor Cyan
    & wsl.exe --set-default-version 2 | Out-Null
}

function Ensure-UbuntuInstalled {
    Write-Host "[gpu-setup] Checking WSL distributions" -ForegroundColor Cyan
    $wslList = & wsl.exe -l -v | Out-String
    if ($wslList -notmatch 'Ubuntu') {
        Write-Warning "Ubuntu WSL distro not found. Install with (admin): wsl --install -d Ubuntu"
        return $false
    }
    return $true
}

function Setup-NvidiaContainerToolkitInWSL {
    Write-Host "[gpu-setup] Installing NVIDIA Container Toolkit inside WSL (requires sudo)" -ForegroundColor Cyan
    $cmds = @(
        'set -e',
        'if ! command -v sudo >/dev/null 2>&1; then echo "sudo missing"; exit 1; fi',
        'if ! sudo -n true 2>/dev/null; then echo "sudo password is required. Please run these commands in WSL:"; echo "sudo apt-get update"; echo "sudo apt-get install -y nvidia-container-toolkit"; echo "sudo nvidia-ctk runtime configure --runtime=docker"; echo "exit then run: wsl.exe --shutdown"; exit 2; fi',
        'sudo apt-get update',
        'sudo apt-get install -y nvidia-container-toolkit',
        'sudo nvidia-ctk runtime configure --runtime=docker || true'
    ) -join ' && '
    & wsl.exe -d Ubuntu -e bash -lc $cmds
}

function Restart-DockerWSL {
    Write-Host "[gpu-setup] Restarting Docker/WSL to apply runtime changes" -ForegroundColor Cyan
    # Best-effort restart path
    try { & wsl.exe --shutdown | Out-Null } catch {}
}

function Test-DockerGPU {
    Write-Host "[gpu-setup] Verifying Docker GPU access with nvidia-smi" -ForegroundColor Cyan
    & docker run --rm --gpus all nvidia/cuda:12.1.1-base-ubuntu22.04 nvidia-smi
}

function Setup-GPUForDocker {
    Write-Host "[gpu-setup] Starting GPU + WSL + Docker configuration" -ForegroundColor Green

    $smi = Test-NvidiaSmi
    if (-not $smi) {
        Write-Warning "NVIDIA driver not detected on host. Please install the latest GeForce/Studio driver and re-run."
        return
    } else {
        Write-Host $smi
    }

    Enable-WSLFeatures
    Ensure-WSLDefaultV2
    $ubuntuOk = Ensure-UbuntuInstalled
    if (-not $ubuntuOk) { return }

    Setup-NvidiaContainerToolkitInWSL
    Restart-DockerWSL

    Write-Host "[gpu-setup] Attempting Docker GPU smoke test (may fail until Docker Desktop GUI enables GPU for WSL)" -ForegroundColor Cyan
    try { Test-DockerGPU } catch { Write-Warning $_ }

    Write-Host "[gpu-setup] Done. If GPU still not visible in containers: open Docker Desktop → Settings → Resources → WSL Integration and enable your Ubuntu distro; enable GPU support if available; then re-run this setup." -ForegroundColor Yellow
}
