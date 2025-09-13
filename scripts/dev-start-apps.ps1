# Starts only the three dev apps: Frontend (Vite), BFF (FastAPI), Orchestrator (Temporal worker)
param(
  [switch]$NoFrontend = $false,
  [switch]$NoBff = $false,
  [switch]$NoOrchestrator = $false
)

$ErrorActionPreference = 'Stop'
$rootDir   = Split-Path -Parent $PSScriptRoot
$stateFile = Join-Path $PSScriptRoot '.dev-state.json'
$pids = @{}

Write-Host 'Starting Frontend, BFF and Orchestrator...' -ForegroundColor Cyan

# Prefer Traefik routes for HTTP services; direct ports for non-HTTP
if (-not $env:TTS_ADAPTER_URL) { $env:TTS_ADAPTER_URL = 'http://elevenlabs.localhost' }
if (-not $env:FISHSPEECH_URL)  { $env:FISHSPEECH_URL  = 'http://tts.localhost' }
if (-not $env:QDRANT_URL)      { $env:QDRANT_URL      = 'http://qdrant.localhost' }
if (-not $env:TEMPORAL_HOST)   { $env:TEMPORAL_HOST   = '127.0.0.1:7233' }
if (-not $env:NEO4J_URL)       { $env:NEO4J_URL       = 'bolt://localhost:7687' }

if (-not $env:VITE_PROXY_TARGET) { $env:VITE_PROXY_TARGET = 'http://localhost:5000' }

function Start-Frontend {
  Write-Host 'Starting frontend dev server...' -ForegroundColor Green
  $proc = Start-Process powershell -ArgumentList '-NoExit','-ExecutionPolicy','Bypass','-Command',"cd '$rootDir\frontend'; npm run dev" -WindowStyle Normal -PassThru
  if ($proc) { $pids['frontend'] = $proc.Id }
}

function Start-Bff {
  Write-Host 'Starting BFF (FastAPI)...' -ForegroundColor Green
  $proc = Start-Process powershell -ArgumentList '-NoExit','-ExecutionPolicy','Bypass','-Command',"& '$PSScriptRoot\dev-bff.ps1'" -WindowStyle Normal -PassThru
  if ($proc) { $pids['bff'] = $proc.Id }
}

function Start-Orchestrator {
  Write-Host 'Starting Orchestrator worker...' -ForegroundColor Green
  $proc = Start-Process powershell -ArgumentList '-NoExit','-ExecutionPolicy','Bypass','-Command',"& '$PSScriptRoot\dev-orchestrator.ps1'" -WindowStyle Normal -PassThru
  if ($proc) { $pids['orchestrator'] = $proc.Id }
}

if (-not $NoFrontend) { Start-Frontend }
if (-not $NoBff) { Start-Bff }
if (-not $NoOrchestrator) { Start-Orchestrator }

function Test-HttpOk { param([string]$Url) try { $r = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 3; return ($r.StatusCode -ge 200 -and $r.StatusCode -lt 500) } catch { return $false } }

# Verify they actually came up (quick, non-blocking)
$frontendOk = $NoFrontend
$bffOk = $NoBff
$orcOk = $NoOrchestrator

1..20 | ForEach-Object {
  if (-not $frontendOk -and $pids.ContainsKey('frontend')) {
    try { $frontendOk = (Test-NetConnection -ComputerName 127.0.0.1 -Port 5173 -WarningAction SilentlyContinue).TcpTestSucceeded -or (Test-HttpOk 'http://127.0.0.1:5173/') } catch {}
  }
  if (-not $bffOk -and $pids.ContainsKey('bff')) {
    try { $bffOk = Test-HttpOk 'http://127.0.0.1:5000/readyz' } catch {}
    if (-not $bffOk) { try { $bffOk = (Test-NetConnection -ComputerName 127.0.0.1 -Port 5000 -WarningAction SilentlyContinue).TcpTestSucceeded } catch {} }
  }
  if (-not $orcOk -and $pids.ContainsKey('orchestrator')) {
    try { $p = Get-Process -Id $pids['orchestrator'] -ErrorAction Stop; $orcOk = $true } catch {}
  }
  if ($frontendOk -and $bffOk -and $orcOk) { break }
  Start-Sleep -Seconds 1
}

# Save PIDs for stop-all.ps1 compatibility
if ($pids.Count -gt 0) {
  try {
    $stateObj = @{ pids = $pids }
    $stateObj | ConvertTo-Json | Set-Content -Path $stateFile -Encoding UTF8
    Write-Host ("Saved PIDs to {0}" -f $stateFile) -ForegroundColor DarkGray
  } catch { Write-Warning ("Failed to save state file: {0}" -f $_.Exception.Message) }
}

Write-Host ''
Write-Host ('Frontend  : {0}' -f ($(if ($NoFrontend) { 'skipped' } elseif ($frontendOk) { 'OK' } else { 'starting...' })))
Write-Host ('BFF       : {0}' -f ($(if ($NoBff) { 'skipped' } elseif ($bffOk) { 'OK' } else { 'starting...' })))
Write-Host ('Orch      : {0}' -f ($(if ($NoOrchestrator) { 'skipped' } elseif ($orcOk) { 'OK' } else { 'starting...' })))

Write-Host "Done. Windows will keep running. You can close this window." -ForegroundColor Yellow

