param(
  [string]$StackName = 'swaivyn',
  [string]$UpstreamUrl = 'http://host.docker.internal:8080',
  [int]$TraefikPort = 80,
  [switch]$NoCache,
  [switch]$Pull
)

$ErrorActionPreference = 'Stop'

function Test-Command { param([string]$Name) try { Get-Command $Name -ErrorAction Stop | Out-Null; return $true } catch { return $false } }

if (-not (Test-Command 'docker')) { throw 'Docker CLI not found. Please install/start Docker Desktop.' }

$root = Resolve-Path "$PSScriptRoot/.."
$stackFile = Join-Path $root 'docker-stack.yml'
if (-not (Test-Path $stackFile)) { throw "Stack file not found: $stackFile" }

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

function Import-DotEnv {
  param([string]$Path)
  if (-not (Test-Path $Path)) { return @{} }
  $vars = @{}
  try {
    (Get-Content -Raw $Path) -split "`n" | ForEach-Object {
      if ($_ -match '^(\s*#|\s*$)') { return }
      if ($_ -match '^(?<k>[^=\s]+)\s*=\s*(?<v>.*)$') {
        $k = $matches['k']
        $v = $matches['v']
        if ($v -match '^"(.*)"$') { $v = $matches[1] }
        elseif ($v -match "^'(.*)'$") { $v = $matches[1] }
        $vars[$k] = $v
      }
    }
  } catch {}
  return $vars
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

function Test-ImageExists { param([string]$Ref) $id = (& docker images -q $Ref) ; return [bool]$id }

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
  $ttsDf = Join-Path $root 'speech/TTS/openaudio-s1-mini/Dockerfile'
  if (-not (Test-ImageExists 'swai/tts-proxy:local')) { Build-Image -Context $root -Dockerfile $ttsDf -Tag 'swai/tts-proxy:local' }
  # 11labs adapter (optional)
  $adapterCtx = Join-Path $root 'Services/tts_11labs_adapter'
  if (Test-Path (Join-Path $adapterCtx 'Dockerfile')) {
    if (-not (Test-ImageExists 'swai/tts-11labs-adapter:local')) { Build-Image -Context $adapterCtx -Tag 'swai/tts-11labs-adapter:local' }
  }
  # orchestrator (optional)
  $orchCtx = Join-Path $root 'Services/orchestrator'
  if (Test-Path (Join-Path $orchCtx 'Dockerfile')) {
    if (-not (Test-ImageExists 'swai/orchestrator:local')) { Build-Image -Context $orchCtx -Tag 'swai/orchestrator:local' }
  }
}
function Ensure-StackNetwork {
  param([string]$StackName, [string]$Network = 'swai-public')
  $netName = "$StackName`_$Network"
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
  if ($UpstreamUrl) { $env:UPSTREAM_TTS = $UpstreamUrl }
  # Voices mount path (POSIX form for Docker Desktop Linux engine)
  $voicesWin = Join-Path $root 'speech/TTS/openaudio-s1-mini/voices'
  $env:SWAI_ROOT_POSIX = Convert-ToDockerHostPath -WinPath $voicesWin
  # Export .env-derived secrets for substitution
  $dotenv = Import-DotEnv (Join-Path $root '.env')
  if (-not $env:POSTGRES_PASSWORD -and $dotenv.POSTGRES_PASSWORD) { $env:POSTGRES_PASSWORD = $dotenv.POSTGRES_PASSWORD }
  if (-not $env:NEO4J_PASSWORD -and $dotenv.NEO4J_PASSWORD) { $env:NEO4J_PASSWORD = $dotenv.NEO4J_PASSWORD }
  if (-not $env:ELEVENLABS_API_KEY -and $dotenv.ELEVENLABS_API_KEY) { $env:ELEVENLABS_API_KEY = $dotenv.ELEVENLABS_API_KEY }
  if ($TraefikPort -and $TraefikPort -gt 0) { $env:TRAEFIK_PORT = "$TraefikPort" }
  Write-Host ("Deploying stack '{0}' from {1}" -f $Name, $File) -ForegroundColor Cyan
  Push-Location $root
  try {
    & docker stack deploy -c $File $Name --detach=false
  } finally { Pop-Location }
  if ($LASTEXITCODE -ne 0) { throw 'docker stack deploy failed' }
}

function Wait-Health {
  param([string]$HostName, [string]$Path = '/health', [int]$Retries = 40, [int]$DelaySec = 2)
  $ok = $false
  $port = $TraefikPort
  $url1 = "http://$($HostName):$($port)$Path"
  $url2 = "http://127.0.0.1:$($port)$Path"
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

Ensure-SwarmActive
Ensure-Images
Ensure-StackNetwork -StackName $StackName

Deploy-Stack -Name $StackName -File $stackFile

Write-Host ("Waiting on TTS via Traefik (http://tts.localhost:{0}/health)..." -f $TraefikPort) -ForegroundColor DarkGray
if (Wait-Health -HostName 'tts.localhost') { Write-Host 'TTS ready.' -ForegroundColor Green } else { Write-Warning 'TTS did not report healthy in time.' }

Write-Host ("Traefik dashboard: http://traefik.localhost:{0}" -f $TraefikPort) -ForegroundColor DarkGray
Write-Host 'Done.' -ForegroundColor Green

# Also wait briefly for Temporal to be reachable (host-published port)
try {
  $ok = $false
  1..30 | ForEach-Object {
    try { if ((Test-NetConnection -ComputerName 127.0.0.1 -Port 7233 -WarningAction SilentlyContinue).TcpTestSucceeded) { $ok = $true; break } } catch {}
    Start-Sleep -Milliseconds 500
  }
  if ($ok) { Write-Host 'Temporal reachable on :7233' -ForegroundColor DarkGreen } else { Write-Warning 'Temporal not reachable yet; dependents will retry.' }
} catch {}
