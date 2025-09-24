param(
  [switch]$NoCache,
  [switch]$Pull
)

$ErrorActionPreference = 'Stop'

function Test-Command { param([string]$Name) try { Get-Command $Name -ErrorAction Stop | Out-Null; return $true } catch { return $false } }
if (-not (Test-Command 'docker')) { throw 'Docker CLI not found. Please install/start Docker Desktop.' }

$root = Resolve-Path "$PSScriptRoot/.."

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

# TTS proxy
$ttsDf = Join-Path $root 'speech/TTS/openaudio-s1-mini/Dockerfile'
if (-not (Test-Path $ttsDf)) { throw "Missing Dockerfile: $ttsDf" }
Build-Image -Context $root -Dockerfile $ttsDf -Tag 'swai/tts-proxy:local'

# 11labs adapter (optional)
$adapterCtx = Join-Path $root 'services/tts_11labs_adapter'
if (Test-Path (Join-Path $adapterCtx 'Dockerfile')) { Build-Image -Context $adapterCtx -Tag 'swai/tts-11labs-adapter:local' }

# Orchestrator (optional)
$orchCtx = Join-Path $root 'services/orchestrator'
if (Test-Path (Join-Path $orchCtx 'Dockerfile')) { Build-Image -Context $orchCtx -Tag 'swai/orchestrator:local' }

Write-Host 'All images built.' -ForegroundColor Green

