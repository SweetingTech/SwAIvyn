# Build (and optionally start) the Fish Speech TTS Docker service.
# This script focuses only on the `tts` service defined in docker-compose.yml.

param(
  [string]$ComposeFile = "$PSScriptRoot/../docker-compose.yml",
  [switch]$NoCache,
  [switch]$Pull,
  [switch]$Up,
  [string]$UpstreamUrl = "http://host.docker.internal:8080",
  [switch]$SwarmDeploy,
  [string]$StackName = 'swai',
  [switch]$VerboseOutput
)

$ErrorActionPreference = 'Stop'
if ($VerboseOutput) { $VerbosePreference = 'Continue' }

function Test-Command {
  param([Parameter(Mandatory)][string]$Name)
  try { Get-Command $Name -ErrorAction Stop | Out-Null; return $true } catch { return $false }
}

if (-not (Test-Command 'docker')) { throw 'Docker is not available in PATH. Install/Start Docker Desktop first.' }

# Resolve repo root
$root = Resolve-Path "$PSScriptRoot/.."
Write-Verbose ("Repo root: {0}" -f $root)

# Validate compose file
if (-not (Test-Path $ComposeFile)) {
  throw ("Compose file not found: {0}" -f $ComposeFile)
}

function Build-ImagesForSwarm {
  Write-Host '== Building local images for Swarm ==' -ForegroundColor Cyan
  # TTS proxy image
  $ttsDockerfile = Join-Path $root 'speech/TTS/openaudio-s1-mini/Dockerfile'
  if (-not (Test-Path $ttsDockerfile)) { throw "Missing Dockerfile: $ttsDockerfile" }
  Push-Location $root
  try {
    $args = @('build','-f',$ttsDockerfile,'-t','swai/tts-proxy:local','.')
    if ($NoCache) { $args += '--no-cache' }
    if ($Pull) { $args += '--pull' }
    Write-Verbose ("docker {0}" -f ($args -join ' '))
    & docker @args
    if ($LASTEXITCODE -ne 0) { throw 'docker build tts-proxy failed' }
  } finally { Pop-Location }

  # 11labs adapter image (optional)
  $adapterCtx = Join-Path $root 'services/tts_11labs_adapter'
  $adapterDockerfile = Join-Path $adapterCtx 'Dockerfile'
  if (Test-Path $adapterDockerfile) {
    Push-Location $adapterCtx
    try {
      $args = @('build','-t','swai/tts-11labs-adapter:local','.')
      if ($NoCache) { $args += '--no-cache' }
      if ($Pull) { $args += '--pull' }
      Write-Verbose ("docker {0}" -f ($args -join ' '))
      & docker @args
      if ($LASTEXITCODE -ne 0) { throw 'docker build tts-11labs-adapter failed' }
    } finally { Pop-Location }
  } else {
    Write-Host '11labs adapter Dockerfile not found; skipping image build' -ForegroundColor DarkGray
  }

  # Orchestrator worker image (optional)
  $orchCtx = Join-Path $root 'services/orchestrator'
  $orchDockerfile = Join-Path $orchCtx 'Dockerfile'
  if (Test-Path $orchDockerfile) {
    Push-Location $orchCtx
    try {
      $args = @('build','-t','swai/orchestrator:local','.')
      if ($NoCache) { $args += '--no-cache' }
      if ($Pull) { $args += '--pull' }
      Write-Verbose ("docker {0}" -f ($args -join ' '))
      & docker @args
      if ($LASTEXITCODE -ne 0) { throw 'docker build orchestrator failed' }
    } finally { Pop-Location }
  } else {
    Write-Host 'Orchestrator Dockerfile not found; skipping image build' -ForegroundColor DarkGray
  }
}

if ($SwarmDeploy) {
  # Ensure swarm is active
  try {
    $info = & docker info --format '{{.Swarm.LocalNodeState}}' 2>$null
    if (-not $info -or $info.Trim().ToLower() -ne 'active') {
      Write-Host 'Initializing Docker Swarm (single-node)...' -ForegroundColor DarkGray
      & docker swarm init 2>$null | Out-Null
    }
  } catch {}

  # Build images and deploy as a Swarm stack through Traefik
  Build-ImagesForSwarm
  # Export UPSTREAM_TTS for variable substitution in the stack file
  if ($UpstreamUrl) { $env:UPSTREAM_TTS = $UpstreamUrl }
  $stackFile = Join-Path $root 'docker-stack.yml'
  if (-not (Test-Path $stackFile)) { throw "Stack file not found: $stackFile" }
  Write-Host ("== Deploying Swarm stack '{0}' ==" -f $StackName) -ForegroundColor Cyan
  Push-Location $root
  try {
    & docker stack deploy -c $stackFile $StackName
    if ($LASTEXITCODE -ne 0) { throw 'docker stack deploy failed' }
  } finally { Pop-Location }

  # Quick probe via Traefik route
  Write-Host 'Waiting for TTS via Traefik (http://tts.localhost/health)...' -ForegroundColor DarkGray
  $ok = $false
  1..30 | ForEach-Object {
    try {
      $resp = Invoke-WebRequest -Uri 'http://tts.localhost/health' -UseBasicParsing -TimeoutSec 3
      if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 300) { $ok = $true; break }
    } catch { }
    Start-Sleep -Seconds 2
  }
  if ($ok) { Write-Host 'TTS (via Traefik) ready.' -ForegroundColor Green } else { Write-Warning 'TTS did not report healthy via Traefik in time.' }
  Write-Host 'Done.' -ForegroundColor Green
  exit 0
}

# Fallback: Compose build path (non-swarm)
# Build args
$buildArgs = @('compose','-f', $ComposeFile, 'build', 'tts')
if ($Pull)   { $buildArgs += '--pull' }
if ($NoCache){ $buildArgs += '--no-cache' }

Write-Host '== Building TTS service (Fish Speech) ==' -ForegroundColor Cyan
Write-Verbose ("docker {0}" -f ($buildArgs -join ' '))
& docker @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'docker compose build tts failed' }

if ($Up) {
  # Set upstream endpoint for Fish-Speech server to avoid proxy recursion
  if ($UpstreamUrl) {
    Write-Host ("Using UPSTREAM_TTS={0}" -f $UpstreamUrl) -ForegroundColor DarkGray
    $env:UPSTREAM_TTS = $UpstreamUrl
  }

  Write-Host '== Starting TTS service with --build ==' -ForegroundColor Cyan
  & docker compose -f $ComposeFile up -d --build tts
  if ($LASTEXITCODE -ne 0) { throw 'docker compose up -d --build tts failed' }

  # Quick readiness ping
  Write-Host 'Waiting for TTS health endpoint (http://localhost:8081/health)...' -ForegroundColor DarkGray
  $ok = $false
  1..20 | ForEach-Object {
    try {
      $resp = Invoke-WebRequest -Uri 'http://localhost:8081/health' -UseBasicParsing -TimeoutSec 3
      if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 300) { $ok = $true; break }
    } catch { }
    Start-Sleep -Seconds 1
  }
  if ($ok) { Write-Host 'TTS ready.' -ForegroundColor Green } else { Write-Warning 'TTS did not report healthy within the initial wait.' }

  # Optional: probe upstream if provided
  if ($UpstreamUrl) {
    try {
      Write-Host ("Probing upstream Fish-Speech at {0}" -f $UpstreamUrl) -ForegroundColor DarkGray
      $u = if ($UpstreamUrl.TrimEnd('/').ToLower().EndsWith('/health')) { $UpstreamUrl } else { ($UpstreamUrl.TrimEnd('/') + '/health') }
      $resp2 = Invoke-WebRequest -Uri $u -UseBasicParsing -TimeoutSec 3
      if ($resp2.StatusCode -ge 200 -and $resp2.StatusCode -lt 300) {
        Write-Host 'Upstream Fish-Speech reachable.' -ForegroundColor Green
      } else {
        Write-Warning 'Upstream Fish-Speech did not respond OK; local voices will work, cloning may fall back.'
      }
    } catch {
      Write-Warning 'Upstream Fish-Speech not reachable; ensure it is running if you want synthesis.'
    }
  }
}

Write-Host 'Done.' -ForegroundColor Green
