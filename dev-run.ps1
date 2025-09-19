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
$rootDir = $PSScriptRoot
$stateFile = Join-Path $PSScriptRoot '.dev-state.json'
$pids = @{}

$script:LogDirectory = Join-Path $rootDir 'logs'
if (-not (Test-Path $script:LogDirectory)) { New-Item -ItemType Directory -Path $script:LogDirectory -Force | Out-Null }
$script:LogFile = Join-Path $script:LogDirectory ("dev-run-{0}.log" -f (Get-Date).ToString('yyyyMMdd-HHmmss'))
if (-not (Test-Path $script:LogFile)) { New-Item -ItemType File -Path $script:LogFile -Force | Out-Null }
$script:TranscriptActive = $false
$script:ShutdownInvoked = $false

Write-Host ("📄 Detailed startup logs will be written to {0}" -f $script:LogFile) -ForegroundColor DarkGray

function Write-Log {
    param(
        [Parameter(Mandatory = $true)][string]$Message,
        [ValidateSet('INFO','WARN','ERROR','SUCCESS','DEBUG')][string]$Level = 'INFO'
    )

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $line = "[{0}] [{1}] {2}" -f $timestamp, $Level, $Message
    $color = 'White'
    switch ($Level) {
        'WARN' { $color = 'Yellow' }
        'ERROR' { $color = 'Red' }
        'SUCCESS' { $color = 'Green' }
        'DEBUG' { $color = 'DarkGray' }
        Default { $color = 'White' }
    }
    Write-Host $line -ForegroundColor $color
    if (-not $script:TranscriptActive) {
        Add-Content -Path $script:LogFile -Value $line
    }
}

try {
    Start-Transcript -Path $script:LogFile -Append -ErrorAction Stop | Out-Null
    $script:TranscriptActive = $true
    Write-Log ("Logging to {0}" -f $script:LogFile) 'DEBUG'
} catch {
    Write-Warning "Failed to start transcript logging: $($_.Exception.Message)"
    Write-Log ("Falling back to direct log writes at {0}" -f $script:LogFile) 'WARN'
}

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
function Ensure-SwarmSafePool {
  try {
    $ingressSubnet = (& docker network inspect ingress --format "{{(index .IPAM.Config 0).Subnet}}" 2>$null).Trim()
    if (-not $ingressSubnet -or $ingressSubnet -like "10.255.*" -or $ingressSubnet -like "10.0.*") {
      Write-Host 'Reinitializing Docker Swarm with a safe address pool to avoid network overlap...' -ForegroundColor DarkGray
      & docker swarm leave --force 2>$null | Out-Null
      & docker swarm init --default-addr-pool 10.123.0.0/16 --default-addr-pool-mask-length 24 2>$null | Out-Null
    }
  } catch {
    Write-Warning ("Could not ensure safe Swarm address pool: {0}" -f $_.Exception.Message)
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

function Get-StackInfo {
  param([string]$Name)
  try {
    $services = & docker stack services $Name --format "{{.Name}}" 2>$null
    if ($LASTEXITCODE -eq 0 -and $services) {
      return @{ Exists = $true; Services = $services }
    }
  } catch {}
  return @{ Exists = $false; Services = @() }
}

function Get-FileHash-Quick {
  param([string]$Path)
  try {
    return (Get-FileHash -Path $Path -Algorithm SHA256).Hash
  } catch {
    return $null
  }
}

function Should-UpdateStack {
  param([string]$Name, [string]$File)

  # Check if stack exists
  $stackInfo = Get-StackInfo -Name $Name
  if (-not $stackInfo.Exists) {
    Write-Host "Stack '$Name' does not exist - will create" -ForegroundColor DarkGray
    return $true
  }

  # Check if configuration file has changed
  $hashFile = Join-Path $rootDir ".stack-$Name.hash"
  $currentHash = Get-FileHash-Quick -Path $File

  if (Test-Path $hashFile) {
    $previousHash = (Get-Content -Raw $hashFile -ErrorAction SilentlyContinue).Trim()
    if ($previousHash -eq $currentHash) {
      # Check if all services are actually running
      $svcLines = & docker stack services $Name --format "{{.Name}}|{{.Replicas}}" 2>$null
      $expectedCount = ($stackInfo.Services | Measure-Object).Count
      $runningCount = 0
      foreach ($ln in $svcLines) {
        if (-not $ln) { continue }
        $parts = $ln -split '\|'
        if ($parts.Count -lt 2) { continue }
        $rep = ($parts[1] -split '\s+')[0]
        if ($rep -match '^(\d+)/(\d+)$') {
          if ([int]$matches[1] -ge [int]$matches[2] -and [int]$matches[2] -gt 0) { $runningCount++ }
        }
      }

      if ($runningCount -eq $expectedCount -and $expectedCount -gt 0) {
        Write-Host "Stack '$Name' is up-to-date and all $runningCount services are running - skipping deployment" -ForegroundColor Green
        return $false
      } else {
        Write-Host "Stack '$Name' configuration unchanged, but only $runningCount/$expectedCount services running - will update" -ForegroundColor Yellow
        return $true
      }
    }
  }

  Write-Host "Stack '$Name' configuration has changed - will update" -ForegroundColor Yellow
  return $true
}

function Get-ServiceLogs {
  param(
    [Parameter(Mandatory = $true)][string]$ServiceName,
    [int]$Tail = 100
  )

  try {
    $logs = & docker service logs $ServiceName --tail $Tail 2>&1
    if (-not $logs) {
      return @("No logs available for service '$ServiceName'.")
    }
    return $logs
  } catch {
    return @("Failed to read logs for service '$ServiceName': $($_.Exception.Message)")
  }
}

function Wait-StackConverged {
  param(
    [Parameter(Mandatory = $true)][string]$Name,
    [int]$TimeoutSec = 420,
    [int]$PollIntervalSec = 5
  )

  $deadline = (Get-Date).AddSeconds($TimeoutSec)
  $lastStatus = @{}
  $taskSnapshot = @{}

  while ((Get-Date) -lt $deadline) {
    $servicesRaw = & docker stack services $Name --format "{{.Name}}|{{.Replicas}}" 2>&1
    $servicesExitCode = $LASTEXITCODE
    $services = @($servicesRaw | Where-Object { $_ })

    if ($servicesExitCode -ne 0) {
      $detailMessage = "Failed to list services for stack '$Name' (exit code $servicesExitCode)."
      Write-Log $detailMessage 'WARN'
      foreach ($line in $services) {
        Write-Log ("  {0}" -f $line) 'WARN'
      }
      return [pscustomobject]@{
        Success = $false
        Reason = $detailMessage
        FailedServices = @()
        TaskDetails = @{}
      }
    }

    if (-not $services) {
      return [pscustomobject]@{
        Success = $false
        Reason = "Stack '$Name' has no services or failed to deploy."
        FailedServices = @()
        TaskDetails = @{}
      }
    }

    $currentStatus = @{}
    $taskSnapshot = @{}
    $allReady = $true

    foreach ($entry in $services) {
      $parts = $entry -split '\|'
      if ($parts.Count -lt 2) { continue }
      $serviceName = $parts[0]
      $replicaField = ($parts[1] -split '\s+')[0]
      $currentStatus[$serviceName] = $replicaField

      # Some services are one-shot or can be deferred until later health checks
      # - temporal: one-shot auto-setup may Complete
      # - traefik: readiness is verified separately via /ping; don't block stack convergence
      $allowComplete = (($serviceName -match 'temporal') -or ($serviceName -match 'traefik'))

      if ($replicaField -match '^(\d+)/(\d+)') {
        $running = [int]$matches[1]
        $desired = [int]$matches[2]
        if (-not $allowComplete) {
          if ($running -lt $desired) { $allReady = $false }
        }
      } else {
        if (-not $allowComplete) { $allReady = $false }
      }

      $psResult = & docker service ps $serviceName --no-trunc --format "{{.Name}}|{{.CurrentState}}|{{.Error}}" 2>&1
      $psExitCode = $LASTEXITCODE
      if ($psExitCode -ne 0) {
        if (-not $allowComplete) { $allReady = $false }
        $errorLine = "Failed to inspect tasks for service '$serviceName' (exit code $psExitCode)."
        Write-Log $errorLine 'WARN'
        $detail = @($errorLine)
        if ($psResult) { $detail += @($psResult | Where-Object { $_ }) }
        $taskSnapshot[$serviceName] = $detail
        continue
      }

      $tasks = @($psResult | Where-Object { $_ })
      $taskSnapshot[$serviceName] = $tasks

      if ($allowComplete) {
        # For Temporal (one-shot/auto-setup), evaluate only the most recent task state.
        # If the latest attempt is Failed/Rejected, treat as failure; otherwise ignore interim states.
        $latest = $tasks | Select-Object -First 1
        if ($latest -match 'Failed|Rejected') {
          Write-Log ("Service '{0}' latest task reported failure during setup" -f $serviceName) 'ERROR'
          return [pscustomobject]@{
            Success = $false
            Reason = "Service '$serviceName' failed during setup."
            FailedServices = @($serviceName)
            TaskDetails = $taskSnapshot
          }
        }
        continue
      }

      foreach ($task in $tasks) {
        $taskParts = $task -split '\|'
        if ($taskParts.Count -lt 2) { continue }
        $state = $taskParts[1]
        $errorMessage = if ($taskParts.Count -ge 3) { $taskParts[2] } else { '' }

        if ($state -match 'Failed|Rejected') {
          Write-Log ("Service '{0}' reported failure state '{1}' {2}" -f $serviceName, $state, $errorMessage) 'ERROR'
          return [pscustomobject]@{
            Success = $false
            Reason = "Service '$serviceName' failed to start: $state $errorMessage".Trim()
            FailedServices = @($serviceName)
            TaskDetails = $taskSnapshot
          }
        }

        if ($state -match '^Running') { continue }
        if ($allowComplete -and $state -match '^Complete') { continue }

        $allReady = $false
      }
    }

    $statusChanged = $false
    foreach ($key in $currentStatus.Keys) {
      if (-not $lastStatus.ContainsKey($key) -or $lastStatus[$key] -ne $currentStatus[$key]) {
        $statusChanged = $true
        break
      }
    }

    if ($statusChanged) {
      $statusText = $currentStatus.GetEnumerator() | Sort-Object Name | ForEach-Object { "{0}={1}" -f $_.Key, $_.Value }
      Write-Log ("Stack '{0}' progress: {1}" -f $Name, ($statusText -join ', ')) 'DEBUG'
    }

    $lastStatus = $currentStatus.Clone()

    if ($allReady) {
      Write-Log ("All services in stack '{0}' reached desired state." -f $Name) 'SUCCESS'
      return [pscustomobject]@{
        Success = $true
        Reason = ''
        FailedServices = @()
        TaskDetails = $taskSnapshot
      }
    }

    Start-Sleep -Seconds $PollIntervalSec
  }

  $notReady = @()
  foreach ($svc in $lastStatus.Keys) {
    $replicaField = $lastStatus[$svc]
    if ($replicaField -match '^(\d+)/(\d+)') {
      if ([int]$matches[1] -lt [int]$matches[2]) { $notReady += $svc }
    } else {
      $notReady += $svc
    }
  }

  if (-not $notReady -and $taskSnapshot.Keys) {
    $notReady = $taskSnapshot.Keys
  }

  return [pscustomobject]@{
    Success = $false
    Reason = "Timed out waiting for Docker stack '$Name' to reach the running state after $TimeoutSec seconds."
    FailedServices = $notReady
    TaskDetails = $taskSnapshot
  }
}

function Handle-StartupFailure {
  param(
    [Parameter(Mandatory = $true)][string]$Reason,
    [string[]]$FailingServices = @(),
    [hashtable]$TaskDetails = @{}
  )

  Write-Log $Reason 'ERROR'

  if ($TaskDetails -and $TaskDetails.Count -gt 0) {
    foreach ($svcName in $TaskDetails.Keys) {
      Write-Log ("Task status for {0}:" -f $svcName) 'ERROR'
      foreach ($taskLine in $TaskDetails[$svcName]) {
        if ($taskLine) {
          Write-Log ("  {0}" -f $taskLine) 'ERROR'
        }
      }
    }
  }

  $servicesToInspect = @()
  if ($FailingServices) { $servicesToInspect += $FailingServices }
  if ($TaskDetails.Keys) { $servicesToInspect += $TaskDetails.Keys }
  $servicesToInspect = $servicesToInspect | Where-Object { $_ } | Select-Object -Unique

  foreach ($svc in $servicesToInspect) {
    Write-Log ("Collecting logs for service '{0}'..." -f $svc) 'ERROR'
    try {
      $servicePs = & docker service ps $svc --no-trunc 2>&1
      if ($servicePs) {
        Write-Log ("  docker service ps {0}:" -f $svc) 'ERROR'
        foreach ($psLine in $servicePs) {
          if ($psLine) { Write-Log ("    {0}" -f $psLine) 'ERROR' }
        }
      }
    } catch {
      Write-Log ("  Unable to inspect service '{0}': {1}" -f $svc, $_.Exception.Message) 'WARN'
    }
    foreach ($line in (Get-ServiceLogs -ServiceName $svc -Tail 120)) {
      if ($line) { Write-Log $line 'ERROR' }
    }
  }

  throw [System.InvalidOperationException]::new($Reason)
}

function Invoke-GracefulShutdown {
  param(
    [Parameter(Mandatory = $true)][string]$Reason,
    [string]$Stack = $StackName
  )

  if ($script:ShutdownInvoked) { return }
  $script:ShutdownInvoked = $true

  Write-Log ("Initiating graceful shutdown due to: {0}" -f $Reason) 'WARN'
  $shutdownScript = Join-Path $rootDir 'dev-shutdown.ps1'
  if (-not (Test-Path $shutdownScript)) {
    Write-Log "dev-shutdown.ps1 not found; manual cleanup may be required." 'WARN'
    return
  }

  try {
    & $shutdownScript -StackName $Stack -DownCompose -Prune
  } catch {
    Write-Log ("Failed to execute dev-shutdown.ps1: {0}" -f $_.Exception.Message) 'ERROR'
  }
}

function Deploy-Stack {
  param([string]$Name, [string]$File)

  if (-not $Name) {
    throw 'StackName not set'
  }

  # Check if we need to deploy/update
  if (-not (Should-UpdateStack -Name $Name -File $File)) {
    return
  }

  # Export env for variable substitution in stack yaml
  $env:STACK_NAME = $Name
  if ($TTSUpstream) { $env:UPSTREAM_TTS = $TTSUpstream }
  # Voices mount path (POSIX form for Docker Desktop Linux engine)
  $voicesWin = Join-Path $rootDir 'speech/TTS/openaudio-s1-mini/voices'
  if (-not (Test-Path $voicesWin)) {
    throw "Voices directory not found at $voicesWin"
  }
  $env:SWAI_ROOT_POSIX = Convert-ToDockerHostPath -WinPath $voicesWin
  # Export .env-derived secrets for substitution
  Import-DotEnv (Join-Path $rootDir '.env')
  if (-not $env:POSTGRES_PASSWORD) {
    throw 'POSTGRES_PASSWORD is required for stack deploy'
  }
  if ($TraefikPort -and $TraefikPort -gt 0) { $env:TRAEFIK_PORT = "$TraefikPort" }

  Write-Host ("Deploying stack '{0}' from {1}" -f $Name, $File) -ForegroundColor Cyan
  Push-Location $rootDir
  try {
    & docker stack deploy -c $File $Name --prune
    if ($LASTEXITCODE -eq 0) {
      # Save hash of successfully deployed configuration
      $hashFile = Join-Path $rootDir ".stack-$Name.hash"
      $currentHash = Get-FileHash-Quick -Path $File
      if ($currentHash) {
        Set-Content -Path $hashFile -Value $currentHash -Encoding ASCII -NoNewline
      }
    }
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
  param([string]$HostName = '127.0.0.1', [int]$Port = 7233, [int]$Retries = 120, [int]$DelaySec = 2)
  Write-Host "Waiting for Temporal service to be fully operational..." -ForegroundColor DarkGray

  # First ensure the port is open
  if (-not (Wait-TcpPort -HostName $HostName -Port $Port -Retries 30 -DelaySec 2)) {
    return $false
  }

  # Wait additional time for Temporal to fully initialize after port opens
  Write-Host "TCP port ready, waiting for Temporal service initialization..." -ForegroundColor DarkGray

  # Check if Temporal container is running and properly initialized
  $maxAttempts = [Math]::Min($Retries, 150)  # Cap at 150 attempts (5 minutes)
  $currentDelay = 2

  for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
    try {
      # Check if temporal container is running and ready
      $containerCheck = & docker ps --filter "name=temporal" --filter "status=running" --format "{{.Names}}" 2>$null
      if ($containerCheck) {
        Write-Host "Attempt $attempt/${maxAttempts}: Temporal container running, checking cluster health..." -ForegroundColor DarkGray

        # Use the proper temporal operator cluster health check
        $healthCmd = "docker exec $($containerCheck | Select-Object -First 1) temporal operator cluster health"
        $healthResult = & cmd /c $healthCmd 2>$null

        if ($LASTEXITCODE -eq 0) {
          Write-Host "✅ Temporal cluster health check passed!" -ForegroundColor Green
          return $true
        } else {
          # Try fallback tctl check
          $fallbackCmd = "docker exec $($containerCheck | Select-Object -First 1) tctl --address 127.0.0.1:7233 cluster health"
          $fallbackResult = & cmd /c $fallbackCmd 2>$null
          if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Temporal cluster health check passed (via tctl)!" -ForegroundColor Green
            return $true
          }
        }

        Write-Host "Temporal container running but cluster not yet healthy (attempt $attempt/$maxAttempts)" -ForegroundColor DarkGray
      } else {
        Write-Host "Attempt $attempt/${maxAttempts}: Waiting for Temporal container to start..." -ForegroundColor DarkGray
      }
    } catch {
      Write-Host "Health check attempt $attempt failed: $($_.Exception.Message)" -ForegroundColor DarkGray
    }

    if ($attempt -lt $maxAttempts) {
      Write-Host "Waiting $currentDelay seconds before next attempt..." -ForegroundColor DarkGray
      Start-Sleep -Seconds $currentDelay

      # Exponential backoff (cap at 8 seconds)
      $currentDelay = [Math]::Min($currentDelay * 1.3, 8)
    }
  }

  # If health check fails after 5 minutes, show logs and fail hard
  Write-Error "❌ Temporal cluster health check failed after 5 minutes!"
  Write-Host "📋 Checking Temporal service logs for errors..." -ForegroundColor Yellow
  try {
    & docker service logs swaivyn_temporal --tail 50 2>$null
  } catch {
    Write-Warning "Could not retrieve Temporal service logs"
  }
  Write-Error "Temporal is required for Orchestrator. Please check the logs above and ensure POSTGRES_PASSWORD is set correctly in your .env file."
  return $false
}

function Start-ServiceScript {
  param([string]$ScriptPath, [string]$ServiceName, [hashtable]$ExtraEnv = @{})

  Write-Host "Starting $ServiceName..." -ForegroundColor Green

  # Create a temp wrapper script to avoid -Command quoting and '&' parsing issues
  $tmpDir = Join-Path $PSScriptRoot '.dev-tmp'
  if (-not (Test-Path $tmpDir)) { New-Item -ItemType Directory -Path $tmpDir | Out-Null }

  $safeName = ($ServiceName -replace '[^A-Za-z0-9_.-]','_')
  $stubPath = Join-Path $tmpDir ("start-{0}-{1}.ps1" -f $safeName, [Guid]::NewGuid().ToString('N'))

  $lines = @()
  $lines += "# Auto-generated wrapper for $ServiceName"
  foreach ($kv in $ExtraEnv.GetEnumerator()) {
    $val = "$($kv.Value)".Replace("'", "''")
    $lines += "`$env:{0} = '{1}'" -f $kv.Key, $val
  }
  # Ensure script path is quoted correctly
  $quotedScript = $ScriptPath.Replace("'", "''")
  $lines += "& '{0}'" -f $quotedScript

  Set-Content -Path $stubPath -Value ($lines -join "`r`n") -Encoding UTF8

  try {
    $proc = Start-Process powershell -ArgumentList @(
      "-NoExit",
      "-ExecutionPolicy","Bypass",
      "-File", $stubPath
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

$startupSucceeded = $false
$startupError = $null

try {
Ensure-SwarmSafePool

Write-Log "Starting SwAIvyn in development mode..." 'INFO'

# --- Import .env file ---
Import-DotEnv

# --- START OF BACKGROUND SERVICES (Docker, etc.) ---
Write-Host "`nStarting background services (Docker, etc.)...`n" -ForegroundColor Cyan
if (-not (Test-Command 'docker')) { throw 'Docker CLI not found. Please install/start Docker Desktop.' }

$stackFile = Join-Path $rootDir 'docker-stack.yml'
if (-not (Test-Path $stackFile)) { throw "Stack file not found: $stackFile" }

Ensure-SwarmActive
Ensure-Images

# Ensure external overlay network exists for the stack
$networkName = "${StackName}_swai-public"
try {
    $exists = (& docker network ls --format '{{.Name}}' 2>$null) | Where-Object { $_ -eq $networkName }
    if (-not $exists) {
        Write-Host "Creating overlay network '$networkName'..." -ForegroundColor DarkGray
        & docker network create -d overlay --attachable $networkName 2>$null | Out-Null
    }
    # Wait briefly until network is visible
    $ok = $false
    for ($i = 0; $i -lt 10; $i++) {
        $chk = (& docker network ls --format '{{.Name}}' 2>$null) | Where-Object { $_ -eq $networkName }
        if ($chk) { $ok = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $ok) { Write-Warning "Overlay network '$networkName' not visible yet; deploy may fail." }
} catch {
    Write-Warning "Failed to ensure network '$networkName': $($_.Exception.Message)"
}

# Final sanity check for overlay network before deploy
try {
  & docker network inspect $networkName 2>$null | Out-Null
} catch {
  Write-Host "Recreating overlay network '$networkName'..." -ForegroundColor DarkGray
  & docker network create -d overlay --attachable $networkName 2>$null | Out-Null
}
Start-Sleep -Seconds 3

Ensure-StackNetwork -StackName $StackName

# Smart deployment - only deploy if needed
Deploy-Stack -Name $StackName -File $stackFile

$stackConvergence = Wait-StackConverged -Name $StackName -TimeoutSec 420
if (-not $stackConvergence.Success) {
    Handle-StartupFailure -Reason $stackConvergence.Reason -FailingServices $stackConvergence.FailedServices -TaskDetails $stackConvergence.TaskDetails
}

# --- Temporal Startup ---
# Startup ordering is handled by health checks now; do not scale Temporal here to avoid interruption.

Write-Host ("Waiting on TTS via Traefik (http://tts.localhost:{0}/health)..." -f $TraefikPort) -ForegroundColor DarkGray
if (Wait-Health -HostName 'tts.localhost') { Write-Host 'TTS ready.' -ForegroundColor Green } else { Write-Warning 'TTS did not report healthy in time.' }

Write-Host ("Traefik dashboard: http://traefik.localhost:{0}" -f $TraefikPort) -ForegroundColor DarkGray

# --- Wait for critical services ---
Write-Host "`nWaiting for critical infrastructure services...`n" -ForegroundColor Yellow

# Wait for PostgreSQL first (Temporal depends on it)
$pgReady = Wait-TcpPort -HostName '127.0.0.1' -Port 5432 -Retries 30 -DelaySec 2

if (-not $pgReady) {
    Handle-StartupFailure -Reason "PostgreSQL did not become ready within the expected time window." -FailingServices @("${StackName}_swai-db")
} else {
    Write-Log "PostgreSQL is ready." 'SUCCESS'
}

# Verify POSTGRES_PASSWORD is set (required for Temporal to connect to DB)
if (-not $env:POSTGRES_PASSWORD) {
    Write-Warning "POSTGRES_PASSWORD not found in environment. Temporal may fail to initialize."
    Write-Host "Please ensure your .env file contains POSTGRES_PASSWORD=<your-password>" -ForegroundColor Yellow
} else {
    Write-Host "✅ POSTGRES_PASSWORD is set" -ForegroundColor Green
}

# Bootstrap Temporal DB schema (idempotent; handled via scripts/setup.ps1)
$setupScript = Join-Path $PSScriptRoot 'scripts\setup.ps1'
if (Test-Path $setupScript) {
  . $setupScript
  try {
    Setup-TemporalSchema -StackName $StackName
  } catch {
    Write-Warning ("Temporal schema bootstrap failed: {0}" -f $_.Exception.Message)
  }
} else {
  Write-Host "scripts/setup.ps1 not found; skipping Temporal schema bootstrap." -ForegroundColor DarkGray
}

# Wait for critical infrastructure services with endpoint polling
Write-Host "🔍 Checking infrastructure service health..." -ForegroundColor Cyan

# Check Traefik with retry logic
$traefikReady = $false
Write-Host "Checking Traefik readiness..." -ForegroundColor DarkGray
for ($i = 1; $i -le 20; $i++) {
    try {
        $traefikUrl = "http://127.0.0.1:$TraefikPort/ping"
        $response = Invoke-WebRequest -Uri $traefikUrl -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $traefikReady = $true
            Write-Host "✅ Traefik is ready (attempt $i)" -ForegroundColor Green
            break
        }
    } catch {
        if ($i % 5 -eq 0) {
            Write-Host "  Still waiting for Traefik (attempt $i)..." -ForegroundColor DarkGray
        }
    }
    Start-Sleep -Seconds 3
}
if (-not $traefikReady) {
    Handle-StartupFailure -Reason "Traefik reverse proxy failed to report ready status within the allotted time." -FailingServices @("${StackName}_traefik")
}

# Check Qdrant with retry logic
$qdrantReady = $false
Write-Host "Checking Qdrant readiness..." -ForegroundColor DarkGray
for ($i = 1; $i -le 15; $i++) {
    try {
        $qdrantUrl = "http://localhost:6333/readyz"
        $response = Invoke-WebRequest -Uri $qdrantUrl -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $qdrantReady = $true
            Write-Host "✅ Qdrant is ready (attempt $i)" -ForegroundColor Green
            break
        }
    } catch {
        if ($i % 5 -eq 0) {
            Write-Host "  Still waiting for Qdrant (attempt $i)..." -ForegroundColor DarkGray
        }
    }
    Start-Sleep -Seconds 4
}
if (-not $qdrantReady) {
    Handle-StartupFailure -Reason "Qdrant vector database did not become ready in time." -FailingServices @("${StackName}_qdrant")
}

# Enhanced Temporal check using admin-tools (works with temporalio/server)
$temporalReady = $false
Write-Host "Checking Temporal cluster health..." -ForegroundColor DarkGray
for ($i = 1; $i -le 100; $i++) {
    try {
        # Use admin-tools container to check health over the stack network
        $dockerArgs = @('run','--rm','--network',"${StackName}_default",'temporalio/admin-tools:1.23','temporal','operator','cluster','health','--address','temporal:7233')
        $healthResult = & docker @dockerArgs 2>$null
        if ($LASTEXITCODE -eq 0 -and $healthResult -match 'SERVING') {
            $temporalReady = $true
            Write-Host "✅ Temporal cluster is healthy (attempt $i)" -ForegroundColor Green
            break
        }
        if ($i % 10 -eq 0) {
            Write-Host "  Temporal not yet healthy (attempt $i)..." -ForegroundColor DarkGray
        }
    } catch {
        if ($i % 15 -eq 0) {
            Write-Host "  Temporal health check error (attempt $i): $($_.Exception.Message)" -ForegroundColor DarkGray
        }
    }
    Start-Sleep -Seconds 3
}
if (-not $temporalReady) {
    Write-Warning "Temporal cluster failed health checks after startup. Continuing without orchestrator; features requiring workflows will be unavailable."
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
    'DATABASE_URL' = if ($env:POSTGRES_PASSWORD) { "postgresql+asyncpg://postgres:$($env:POSTGRES_PASSWORD)@127.0.0.1:5432/swai" } else { '' }
    'QDRANT_URL' = 'http://localhost:6333'
    'TTS_ADAPTER_URL' = 'http://localhost:8082'
    'FISHSPEECH_URL' = 'http://localhost:8081'
    'OLLAMA_HOST' = 'http://localhost:11434'
    'LMSTUDIO_HOST' = 'http://localhost:1234'
}

# Override URLs if using Traefik routing
if ($UseTraefik) {
    $serviceEnv['TTS_ADAPTER_URL'] = "http://elevenlabs.localhost:$TraefikPort"
    $serviceEnv['FISHSPEECH_URL'] = "http://tts.localhost:$TraefikPort"
    $serviceEnv['QDRANT_URL'] = "http://qdrant.localhost:$TraefikPort"
}

if (-not $BackendOnly) {
    # --- Start Frontend ---
    $frontendScript = Join-Path $PSScriptRoot 'scripts/dev-frontend.ps1'
    Start-ServiceScript -ScriptPath $frontendScript -ServiceName 'frontend' -ExtraEnv @{}
    Write-Host "Frontend dev server starting at http://0.0.0.0:5173" -ForegroundColor Green

    # Give frontend a moment to start
    Start-Sleep -Seconds 2
}

if (-not $FrontendOnly) {
    # --- Start BFF ---

    if ($pgReady) {
        $bffScript = Join-Path $PSScriptRoot 'scripts/dev-bff.ps1'
        Start-ServiceScript -ScriptPath $bffScript -ServiceName 'bff' -ExtraEnv $serviceEnv
        Write-Host "BFF dev server starting at http://0.0.0.0:5000" -ForegroundColor Green

        # Give BFF a moment to start
        Start-Sleep -Seconds 3
    } else {
        Write-Warning "Skipping BFF startup due to PostgreSQL not being ready."
    }

    # --- Start Orchestrator ---
    if ($temporalReady) {
        $orchestratorScript = Join-Path $PSScriptRoot 'scripts/dev-orchestrator.ps1'
        Start-ServiceScript -ScriptPath $orchestratorScript -ServiceName 'orchestrator' -ExtraEnv $serviceEnv
        Write-Host "Orchestrator worker started" -ForegroundColor Green
    } else {
        Write-Warning "Skipping Orchestrator startup due to Temporal not being ready."
    }
}

if ($temporalReady) {
    # Ensure Temporal default namespace exists (idempotent)
    try {
        $nsArgs = @('run','--rm','--network',"${StackName}_default",'temporalio/admin-tools:1.23','bash','-lc',
            "temporal operator namespace describe --namespace default --address temporal:7233 || temporal operator namespace create --namespace default --address temporal:7233 --retention 3d --yes")
        & docker @nsArgs | Out-Null
    } catch {
        Write-Warning ("Failed to ensure Temporal default namespace: {0}" -f $_.Exception.Message)
    }
}

# --- FINALIZATION ---
Write-Host ("`n" + "="*60) -ForegroundColor Yellow
Write-Host "🚀 SwAIvyn Development Environment Ready!" -ForegroundColor Green
Write-Host ("="*60) -ForegroundColor Yellow

Write-Host "`n📱 APPLICATION ACCESS:" -ForegroundColor Cyan
Write-Host "Frontend: http://localhost:5173" -ForegroundColor White
Write-Host "Backend API: http://localhost:5000" -ForegroundColor White

if ($UseTraefik) {
    # Set UTF-8 encoding to handle emoji properly
    [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()
    Write-Host "`nTRAEFIK ROUTING (recommended for production-like testing):" -ForegroundColor Cyan
    Write-Host "Frontend (via Traefik): http://app.localhost:$TraefikPort" -ForegroundColor White
    Write-Host "Backend API (via Traefik): http://bff.localhost:$TraefikPort" -ForegroundColor White
}

Write-Host "`n🔧 INFRASTRUCTURE SERVICES:" -ForegroundColor Cyan
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
        Write-Host "`n🌍 REMOTE ACCESS (for other devices on your network):" -ForegroundColor Cyan
        foreach ($ip in $lanIPs) {
            Write-Host "Frontend: http://${ip}:5173" -ForegroundColor White
            Write-Host "Backend API: http://${ip}:5000" -ForegroundColor White
            if ($UseTraefik) {
                Write-Host "Traefik (all services): http://${ip}:$TraefikPort" -ForegroundColor White
            }
        }
    }
} catch {}

Write-Host "`n💡 TIPS:" -ForegroundColor Yellow
Write-Host "• Frontend supports hot reloading - changes will refresh automatically" -ForegroundColor White
Write-Host "• Use Traefik URLs for testing production-like routing" -ForegroundColor White
Write-Host "• Check individual PowerShell windows for service-specific logs" -ForegroundColor White
Write-Host "• Press Ctrl+C in service windows to stop individual services" -ForegroundColor White

Write-Host ("`n" + "="*60) -ForegroundColor Yellow

# --- AUTOMATIC BROWSER OPENING ---
function Test-Url {
    param([string]$Url)
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
        return $response.StatusCode -lt 400
    } catch {
        return $false
    }
}

Write-Host "`n🌐 Opening web browser..." -ForegroundColor Cyan

# Determine the best URL to open
$primaryUrl = if ($UseTraefik) { "http://app.localhost:$TraefikPort" } else { "http://localhost:5173" }
$fallbackUrl = if ($UseTraefik) { "http://localhost:5173" } else { "http://app.localhost:$TraefikPort" }

# Wait for the application to be ready (up to 2 minutes)
$deadline = (Get-Date).AddMinutes(2)
$appReady = $false

Write-Host "Waiting for application to be ready at $primaryUrl..." -ForegroundColor DarkGray

while ((Get-Date) -lt $deadline) {
    if (Test-Url $primaryUrl) {
        $appReady = $true
        break
    }
    Start-Sleep -Seconds 2
}

# Try fallback URL if primary failed
if (-not $appReady -and $fallbackUrl -ne $primaryUrl) {
    Write-Host "Trying fallback URL $fallbackUrl..." -ForegroundColor DarkGray
    if (Test-Url $fallbackUrl) {
        $primaryUrl = $fallbackUrl
        $appReady = $true
    }
}

# Open the browser
if ($appReady) {
    try {
        Start-Process $primaryUrl
        Write-Host "✅ Browser opened to $primaryUrl" -ForegroundColor Green
    } catch {
        Write-Warning "Could not open browser automatically. Please visit: $primaryUrl"
    }
} else {
    Write-Warning "Application not ready yet. Please wait a moment and visit: $primaryUrl"
}

Write-Host ("`n" + "="*60) -ForegroundColor Yellow

if ($pids.Count -gt 0) {
    Write-Host "`nSaving process IDs to state file for cleanup..." -ForegroundColor DarkGray
    $stateObj = @{ pids = $pids }
    $stateObj | ConvertTo-Json | Set-Content -Path $stateFile -Encoding UTF8
}
$startupSucceeded = $true
}
catch {
    $startupError = $_
    $errorMessage = if ($_.Exception) { $_.Exception.Message } else { "$_" }
    Write-Log ("Startup failed: {0}" -f $errorMessage) 'ERROR'
    Invoke-GracefulShutdown -Reason $errorMessage -Stack $StackName
}
finally {
    if ($startupSucceeded) {
        Write-Log ("SwAIvyn startup completed successfully. Log file: {0}" -f $script:LogFile) 'SUCCESS'
    } else {
        Write-Log ("SwAIvyn startup failed. Review log file: {0}" -f $script:LogFile) 'ERROR'
    }

    if ($script:TranscriptActive) {
        try { Stop-Transcript | Out-Null } catch {}
        $script:TranscriptActive = $false
    }
}

if ($startupError) { exit 1 }
