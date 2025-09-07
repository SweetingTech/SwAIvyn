# Build (and optionally start) the Fish Speech TTS Docker service.
# This script focuses only on the `tts` service defined in docker-compose.yml.

param(
  [string]$ComposeFile = "$PSScriptRoot/../docker-compose.yml",
  [switch]$NoCache,
  [switch]$Pull,
  [switch]$Up,
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

# Validate expected Dockerfile path referenced by compose
$expectedDockerfile = Join-Path $root 'speech/TTS/openaudio-s1-mini/Dockerfile'
if (-not (Test-Path $expectedDockerfile)) {
  Write-Warning @"
The expected Dockerfile for the TTS service was not found:
  $expectedDockerfile

Your current docker-compose.yml references this path under the `tts` service.
Options:
  1) Run Fish Speech on the host (recommended for now) and set FISHSPEECH_URL=http://localhost:8081
  2) Provide a valid Dockerfile for the Fish Speech server at the path above
  3) Switch the compose service to a published Fish Speech image

Proceeding will likely fail to build. Press Ctrl+C to abort if this is unexpected.
"@
}

# Build args
$buildArgs = @('compose','-f', $ComposeFile, 'build', 'tts')
if ($Pull)   { $buildArgs += '--pull' }
if ($NoCache){ $buildArgs += '--no-cache' }

Write-Host '== Building TTS service (Fish Speech) ==' -ForegroundColor Cyan
Write-Verbose ("docker {0}" -f ($buildArgs -join ' '))
& docker @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'docker compose build tts failed' }

if ($Up) {
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
}

Write-Host 'Done.' -ForegroundColor Green

