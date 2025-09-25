# Development script for SwAIvyn - runs frontend and backend with hot reloading
param (
    [switch] $FrontendOnly = $false,
    [switch] $BackendOnly = $false,
    [switch] $StartTTS = $false,
    [string] $TTSUpstream = 'http://host.docker.internal:8080',
    [switch] $DisableTraefik = $false,
    [switch] $Swarm = $false,
    [string] $StackName = 'swaivyn',
    [int] $TraefikPort = 80,
    [switch]$NoCache,
    [switch]$Pull
)

# Traefik is enabled by default, can be disabled with -DisableTraefik
$UseTraefik = -not $DisableTraefik

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot
$stateFile = Join-Path $PSScriptRoot '.dev-state.json'
$pids = @{}

# --- Utility Functions ---
function Test-Command { param([string]$Name) { try { Get-Command $Name -ErrorAction Stop | Out-Null; return $true } catch { return $false } } }

function Resolve-PythonExe {
    param([string]$Preferred = 'py')
    $prefCmd = $null
    $prefArgs = @()
    if ($Preferred) {
        $parts = $Preferred -split '\s+'
        if ($parts.Count -gt 0) {
            $prefCmd = $parts[0]
            if ($parts.Count -gt 1) { $prefArgs = $parts[1..($parts.Count-1)] }
            try {
                $cmd = Get-Command $prefCmd -ErrorAction Stop
                return @{ Exe = $cmd.Path; PreArgs = $prefArgs }
            } catch {}
        }
    }
    foreach ($name in @('py','python','python3')) {
        try {
            $cmd = Get-Command $name -ErrorAction Stop
            $args = @()
            if ($name -eq 'py') { $args = @('-3') }
            return @{ Exe = $cmd.Path; PreArgs = $args }
        } catch {}
    }
    throw 'Python not found in PATH. Install Python 3 and restart your shell.'
}

function Import-DotEnv {
    param([string]$Path = (Join-Path $rootDir '.env'))
    if (-not (Test-Path $Path)) { return }
    Get-Content -Raw $Path | ForEach-Object {
        $_ -split "`n" | ForEach-Object {
            if ($_ -match '^(\s*#|\s*)$') { return }
            if ($_ -match '^(?<k>[^=\s]+)\s*=\s*(?<v>.*)$') {
                $k = $matches['k']
                $v = $matches['v']
                if ($v -match '^"(.*)"$') { $v = $matches[1] }
                elseif ($v -match "^'(.*)'$") { $v = $matches[1] }
                Set-Item -Path ("Env:" + $k) -Value $v -ErrorAction SilentlyContinue
            }
        }
    }
}

function Ensure-Venv {
    param([string]$Dir)
    $venvPy = Join-Path $Dir '.venv/Scripts/python.exe'
    if (-not (Test-Path $venvPy)) {
        Write-Host "Creating Python virtual environment in $Dir..." -ForegroundColor Cyan
        $py = Resolve-PythonExe
        & $py.Exe @($py.PreArgs) -m venv (Join-Path $Dir '.venv')
    } else {
        Write-Host "Using existing virtual environment in $Dir" -ForegroundColor DarkGray
    }
}

function Install-RequirementsIfNeeded {
    param([string]$Dir, [string]$ReqFile = 'requirements.txt')
    $reqPath = Join-Path $Dir $ReqFile
    $hashFile = Join-Path $Dir '.venv/.req.hash'
    $needInstall = $true
    if (Test-Path $reqPath) {
        $hash = (Get-FileHash -Path $reqPath -Algorithm SHA256).Hash
        if (Test-Path $hashFile) {
            $prev = (Get-Content -Raw $hashFile -ErrorAction SilentlyContinue).Trim()
            if ($prev -eq $hash) { $needInstall = $false }
        }
        if ($needInstall) {
            Write-Host "Installing requirements from $reqPath..." -ForegroundColor Cyan
            $venvPy = Join-Path $Dir '.venv/Scripts/python.exe'
            & $venvPy -m pip install -r $reqPath | Write-Host
            Set-Content -Path $hashFile -Value $hash -Encoding ASCII -NoNewline
        } else {
            Write-Host "Requirements in $Dir unchanged; skipping pip install" -ForegroundColor DarkGray
        }
    }
}

function Ensure-SwarmActive {
  try {
    $state = (& docker info --format '{{.Swarm.LocalNodeState}}' 2>$null).Trim()
    if ($state.ToLower() -ne 'active') {
      Write-Host 'Initializing Docker Swarm (single-node)...' -ForegroundColor DarkGray
      & docker swarm init 2>$null | Out-Null
    }
  } catch {
    Write-Warning 'Failed to ensure Swarm is active. Commands may fail.'
  }
}

function Convert-ToDockerHostPath {
  param([string]$WinPath)
  if (-not $WinPath) { return $null }
  try { $full = (Resolve-Path $WinPath).Path } catch { $full = $WinPath }
  if ($full -match '^(?<drive>[A-Za-z]):\\(?<rest>.*)$') {
    $drive = $matches['drive'].ToLower()
    $rest = $matches['rest'] -replace '\\','/'
    return "/run/desktop/mnt/host/$drive/$rest"
  }
  return ($WinPath -replace '\\','/')
}

function Test-ImageExists { param([string]$Ref) { $id = (& docker images -q $Ref) ; return [bool]$id } }

function Build-Image {
  param([string]$Context, [string]$Dockerfile = $null, [string]$Tag)
  Push-Location $Context
  try {
    $args = @('build','-t', $Tag)
    if ($Dockerfile) { $args += @('-f', $Dockerfile) }
    $args += '.'
    if ($NoCache) { $args += '--no-cache' }
    if ($Pull)    { $args += '--pull' }
    Write-Host ("docker {0}" -f ($args -join ' ')) -ForegroundColor DarkGray
    & docker @args
    if ($LASTEXITCODE -ne 0) { throw "docker build failed for $Tag" }
  } finally { Pop-Location }
}

function Ensure-Images {
  # tts proxy
  $ttsDf = Join-Path $rootDir 'speech/TTS/openaudio-s1-mini/Dockerfile'
  if (-not (Test-ImageExists 'swai/tts-proxy:local')) { Build-Image -Context $rootDir -Dockerfile $ttsDf -Tag 'swai/tts-proxy:local' }
  # 11labs adapter (optional)
  $adapterCtx = Join-Path $rootDir 'Services/tts_11labs_adapter'
  if (Test-Path (Join-Path $adapterCtx 'Dockerfile')) {
    if (-not (Test-ImageExists 'swai/tts-11labs-adapter:local')) { Build-Image -Context $adapterCtx -Tag 'swai/tts-11labs-adapter:local' }
  }
  # orchestrator (optional)
  $orchCtx = Join-Path $rootDir 'Services/orchestrator'
  if (Test-Path (Join-Path $orchCtx 'Dockerfile')) {
    if (-not (Test-ImageExists 'swai/orchestrator:local')) { Build-Image -Context $orchCtx -Tag 'swai/orchestrator:local' }
  }
}

function Ensure-StackNetwork {
  param([string]$StackName, [string]$Network = 'swai-public')
  $netName = "${StackName}_${Network}"
  try {
    $exists = (& docker network ls --format '{{.Name}}' 2>$null) | Where-Object { $_ -eq $netName }
    if (-not $exists) {
      Write-Host ("Creating overlay network '{0}'..." -f $netName) -ForegroundColor DarkGray
      & docker network create -d overlay --attachable $netName 2>$null | Out-Null
    }
  } catch {
    Write-Warning ("Failed to ensure overlay network '{0}': {1}" -f $netName, $_.Exception.Message)
  }
}

function Deploy-Stack {
  param([string]$Name, [string]$File)
  # Export env for variable substitution in stack yaml
  $env:STACK_NAME = $Name
  if ($TTSUpstream) { $env:UPSTREAM_TTS = $TTSUpstream }
  # Voices mount path (POSIX form for Docker Desktop Linux engine)
  $voicesWin = Join-Path $rootDir 'speech/TTS/openaudio-s1-mini/voices'
  $env:SWAI_ROOT_POSIX = Convert-ToDockerHostPath -WinPath $voicesWin
  # Export .env-derived secrets for substitution
  $dotenv = Import-DotEnv (Join-Path $rootDir '.env')
  if (-not $env:POSTGRES_PASSWORD -and $dotenv.POSTGRES_PASSWORD) { $env:POSTGRES_PASSWORD = $dotenv.POSTGRES_PASSWORD }
  if (-not $env:NEO4J_PASSWORD -and $dotenv.NEO4J_PASSWORD) { $env:NEO4J_PASSWORD = $dotenv.NEO4J_PASSWORD }
  if (-not $env:ELEVENLABS_API_KEY -and $dotenv.ELEVENLABS_API_KEY) { $env:ELEVENLABS_API_KEY = $dotenv.ELEVENLABS_API_KEY }
  if ($TraefikPort -and $TraefikPort -gt 0) { $env:TRAEFIK_PORT = "$TraefikPort" }
  Write-Host ("Deploying stack '{0}' from {1}" -f $Name, $File) -ForegroundColor Cyan
  Push-Location $rootDir
  try {
    & docker stack deploy -c $File $Name --detach=false
  } finally { Pop-Location }
  if ($LASTEXITCODE -ne 0) { throw 'docker stack deploy failed' }
}

function Wait-Health {
  param([string]$HostName, [string]$Path = '/health', [int]$Retries = 40, [int]$DelaySec = 2)
  $ok = $false
  $port = $TraefikPort
  $url1 = "http://${HostName}:${port}${Path}"
  $url2 = "http://127.0.0.1:${port}${Path}"
  for ($i = 1; $i -le $Retries; $i++) {
    try {
      # Try DNS route
      $r = Invoke-WebRequest -Uri $url1 -UseBasicParsing -TimeoutSec 3
      if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300) { $ok = $true; break }
    } catch {
      # Try Host header against 127.0.0.1 in case *.localhost DNS is blocked
      try {
        $r2 = Invoke-WebRequest -Uri $url2 -Headers @{ Host = $HostName } -UseBasicParsing -TimeoutSec 3
        if ($r2.StatusCode -ge 200 -and $r2.StatusCode -lt 300) { $ok = $true; break }
      } catch {}
    }
    if ($ok) { break }
    Start-Sleep -Seconds $DelaySec
  }
  return $ok
}

function Wait-TcpPort {
  param([string]$HostName = 'localhost', [int]$Port, [int]$Retries = 60, [int]$DelaySec = 2)
  Write-Host "Waiting for ${HostName}:${Port} to be available..." -ForegroundColor DarkGray
  for ($i = 1; $i -le $Retries; $i++) {
    try {
      if ((Test-NetConnection -ComputerName $HostName -Port $Port -WarningAction SilentlyContinue).TcpTestSucceeded) {
        Write-Host "${HostName}:${Port} is ready" -ForegroundColor Green
        return $true
      }
    } catch {}
    if ($i -eq $Retries) {
      Write-Warning "${HostName}:${Port} did not become available after $($Retries * $DelaySec) seconds"
      return $false
    }
    Start-Sleep -Seconds $DelaySec
  }
  return $false
}

function Wait-TemporalService {
  param([string]$HostName = 'localhost', [int]$Port = 7233, [int]$Retries = 120, [int]$DelaySec = 2)
  Write-Host "Waiting for Temporal service to be fully operational..." -ForegroundColor DarkGray
  
  # First ensure the port is open
  if (-not (Wait-TcpPort -HostName $HostName -Port $Port -Retries 30 -DelaySec 2)) {
    return $false
  }
  
  # Then wait additional time for Temporal to fully initialize
  Write-Host "TCP port ready, waiting for Temporal service initialization..." -ForegroundColor DarkGray
  Start-Sleep -Seconds 15  # Give Temporal time to fully start after port opens
  
  # Check if we can see any Temporal containers running
  try {
    $containers = & docker ps --filter "name=temporal" --format "{{.Names}}" 2>$null
    if ($containers) {
      Write-Host "Temporal container(s) running: $($containers -join ', ')" -ForegroundColor Green
      
      # Additional wait for service readiness
      Write-Host "Allowing additional time for Temporal service readiness..." -ForegroundColor DarkGray
      Start-Sleep -Seconds 10
      
      Write-Host "Temporal service should now be ready for connections" -ForegroundColor Green
      return $true
    }
  } catch {
    Write-Warning "Could not check Temporal container status: $($_.Exception.Message)"
  }
  
  Write-Host "Temporal service readiness check completed" -ForegroundColor Green
  return $true
}

function Start-ServiceScript {
  param([string]$ScriptPath, [string]$ServiceName, [hashtable]$ExtraEnv = @{})
  
  Write-Host "Starting $ServiceName..." -ForegroundColor Green
  
  # Prepare environment variables as command arguments
  $envArgs = @()
  foreach ($key in $ExtraEnv.Keys) {
    $envArgs += "`$env:$key = '$($ExtraEnv[$key])'"
  }
  $envSetup = if ($envArgs.Count -gt 0) { ($envArgs -join '; ') + '; ' } else { '' }
  
  $fullCommand = "${envSetup}& '$ScriptPath'"
  
  try {
    $proc = Start-Process powershell -ArgumentList @(
      "-NoExit", 
      "-ExecutionPolicy", "Bypass", 
      "-Command", 
      $fullCommand
    ) -WindowStyle Normal -PassThru
    
    if ($proc) { 
      $pids[$ServiceName] = $proc.Id
      Write-Host "$ServiceName started with PID $($proc.Id)" -ForegroundColor Green
      return $proc.Id
    } else {
      Write-Warning "Failed to start $ServiceName"
      return $null
    }
  } catch {
    Write-Warning "Error starting ${ServiceName}: $($_.Exception.Message)"
    return $null
  }
}

Write-Host "Starting SwAIvyn in development mode..." -ForegroundColor Cyan

# --- Import .env file ---
Import-DotEnv

# --- START OF BACKGROUND SERVICES (Docker, etc.) ---
Write-Host "`nStarting background services (Docker, etc.)...`n" -ForegroundColor Cyan
if (-not (Test-Command 'docker')) { throw 'Docker CLI not found. Please install/start Docker Desktop.' }

$stackFile = Join-Path $rootDir 'docker-stack.yml'
if (-not (Test-Path $stackFile)) { throw "Stack file not found: $stackFile" }

Ensure-SwarmActive
Ensure-Images
Ensure-StackNetwork -StackName $StackName
Deploy-Stack -Name $StackName -File $stackFile

Write-Host ("Waiting on TTS via Traefik (http://tts.localhost:{0}/health)..." -f $TraefikPort) -ForegroundColor DarkGray
if (Wait-Health -HostName 'tts.localhost') { Write-Host 'TTS ready.' -ForegroundColor Green } else { Write-Warning 'TTS did not report healthy in time.' }

Write-Host ("Traefik dashboard: http://traefik.localhost:{0}" -f $TraefikPort) -ForegroundColor DarkGray

# --- Wait for critical services ---
Write-Host "`nWaiting for critical infrastructure services...`n" -ForegroundColor Yellow

# Wait for Temporal (critical for orchestrator) - enhanced check
$temporalReady = Wait-TemporalService -HostName 'localhost' -Port 7233

# Wait for PostgreSQL (critical for BFF)
$pgReady = Wait-TcpPort -HostName 'localhost' -Port 5432 -Retries 30 -DelaySec 2

if (-not $temporalReady) {
    Write-Warning "Temporal is not ready. Orchestrator may fail to start."
}

if (-not $pgReady) {
    Write-Warning "PostgreSQL is not ready. BFF may fail to start."
}

# --- START OF FRONTEND/BFF/ORCHESTRATOR ---
Write-Host "`nStarting user-facing services...`n" -ForegroundColor Yellow

# --- Get LAN IP for host-to-container communication ---
$lanIP = ([System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName()) |
    Where-Object { $_.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork } |
    Select-Object -First 1).IPAddressToString

if (-not $lanIP) {
    Write-Warning "Could not determine LAN IP address. Defaulting to localhost for services."
    $lanIP = "localhost"
}

# --- Prepare Environment Variables for Services ---
$serviceEnv = @{
    'TEMPORAL_HOST' = '127.0.0.1:7233'  # force IPv4
    'NEO4J_URL' = "bolt://localhost:7687"  # Use localhost for consistency
    'DATABASE_URL' = if ($env:POSTGRES_PASSWORD) { "postgresql+asyncpg://postgres:$($env:POSTGRES_PASSWORD)@localhost:5432/swai" } else { '' }
    'QDRANT_URL' = 'http://localhost:6333'
    'TTS_ADAPTER_URL' = 'http://localhost:8082'
    'FISHSPEECH_URL' = 'http://localhost:8081'
    'OLLAMA_HOST' = 'http://localhost:11434'
    'LMSTUDIO_HOST' = 'http://localhost:1234'
    'LLM_MODEL' = 'llama3'
}

# Override URLs if using Traefik routing
if ($UseTraefik) {
    $serviceEnv['TTS_ADAPTER_URL'] = "http://elevenlabs.localhost:$TraefikPort"
    $serviceEnv['FISHSPEECH_URL'] = "http://tts.localhost:$TraefikPort"
    $serviceEnv['QDRANT_URL'] = "http://qdrant.localhost:$TraefikPort"
}

if (-not $BackendOnly) {
    # --- Start Frontend ---
    $frontendScript = Join-Path $PSScriptRoot 'dev-frontend.ps1'
    Start-ServiceScript -ScriptPath $frontendScript -ServiceName 'frontend' -ExtraEnv @{}
    Write-Host "Frontend dev server starting at http://0.0.0.0:5173" -ForegroundColor Green
    
    # Give frontend a moment to start
    Start-Sleep -Seconds 2
}

if (-not $FrontendOnly) {
    # --- Start BFF ---
    if ($pgReady) {
        $bffScript = Join-Path $PSScriptRoot 'dev-bff.ps1'
        Start-ServiceScript -ScriptPath $bffScript -ServiceName 'bff' -ExtraEnv $serviceEnv
        Write-Host "BFF dev server starting at http://0.0.0.0:5000" -ForegroundColor Green
        
        # Give BFF a moment to start
        Start-Sleep -Seconds 3
    } else {
        Write-Warning "Skipping BFF startup due to PostgreSQL not being ready."
    }

    # --- Start Orchestrator ---
    if ($temporalReady) {
        $orchestratorScript = Join-Path $PSScriptRoot 'dev-orchestrator.ps1'
        Start-ServiceScript -ScriptPath $orchestratorScript -ServiceName 'orchestrator' -ExtraEnv $serviceEnv
        Write-Host "Orchestrator worker started" -ForegroundColor Green
    } else {
        Write-Warning "Skipping Orchestrator startup due to Temporal not being ready."
    }
}

# --- FINALIZATION ---
Write-Host "`n" + "="*60 -ForegroundColor Yellow
Write-Host " SwAIvyn Development Environment Ready!" -ForegroundColor Green
Write-Host "="*60 -ForegroundColor Yellow

Write-Host "`n APPLICATION ACCESS:" -ForegroundColor Cyan
Write-Host "Frontend: http://localhost:5173" -ForegroundColor White
Write-Host "Backend API: http://localhost:5000" -ForegroundColor White

if ($UseTraefik) {
    # Set UTF-8 encoding to handle emoji properly
    [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
    $trafikMsg = "`n`u{1F310} TRAEFIK ROUTING (recommended for production-like testing):"
    Write-Host $trafikMsg -ForegroundColor Cyan
    Write-Host "Frontend (via Traefik): http://app.localhost:$TraefikPort" -ForegroundColor White
    Write-Host "Backend API (via Traefik): http://bff.localhost:$TraefikPort" -ForegroundColor White
}

Write-Host "`n INFRASTRUCTURE SERVICES:" -ForegroundColor Cyan
Write-Host "Traefik Dashboard: http://traefik.localhost:$TraefikPort" -ForegroundColor White
Write-Host "Qdrant Vector DB: http://qdrant.localhost:$TraefikPort" -ForegroundColor White
Write-Host "Neo4j Graph DB: http://graph.localhost:$TraefikPort" -ForegroundColor White
Write-Host "Weaviate Vector DB: http://weaviate.localhost:$TraefikPort" -ForegroundColor White

# Also print LAN-access URLs for remote users
try {
    $lanIPs = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName()) |
        Where-Object { $_.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork } |
        ForEach-Object { $_.IPAddressToString }
    if ($lanIPs) {
        Write-Host "`n REMOTE ACCESS (for other devices on your network):" -ForegroundColor Cyan
        foreach ($ip in $lanIPs) {
            Write-Host "Frontend: http://${ip}:5173" -ForegroundColor White
            Write-Host "Backend API: http://${ip}:5000" -ForegroundColor White
            if ($UseTraefik) {
                Write-Host "Traefik (all services): http://${ip}:$TraefikPort" -ForegroundColor White
            }
        }
    }
} catch {}

Write-Host "`n TIPS:" -ForegroundColor Yellow
Write-Host "-  Frontend supports hot reloading - changes will refresh automatically" -ForegroundColor White
Write-Host "-  Use Traefik URLs for testing production-like routing" -ForegroundColor White
Write-Host "-  Check individual PowerShell windows for service-specific logs" -ForegroundColor White
Write-Host "-  Press Ctrl+C in service windows to stop individual services" -ForegroundColor White

Write-Host "`n" + "="*60 -ForegroundColor Yellow

if ($pids.Count -gt 0) {
    Write-Host "`nSaving process IDs to state file for cleanup..."
    $stateObj = @{ pids = $pids }
    $stateObj | ConvertTo-Json | Set-Content -Path $stateFile -Encoding UTF8
}
