param(
  [int]$BffPort = 5000,
  [int]$ActivityThreads = 32
)

$ErrorActionPreference = 'Stop'
$rootDir = Split-Path -Parent $PSScriptRoot

Write-Host 'Starting host apps (Frontend, BFF, Orchestrator)...' -ForegroundColor Cyan

# Frontend (Vite) in its own PowerShell window
try {
  Write-Host '-> Frontend' -ForegroundColor Green
  Start-Process powershell -ArgumentList '-NoExit','-ExecutionPolicy','Bypass','-Command',"cd '$rootDir\frontend'; npm run dev" -WindowStyle Normal | Out-Null
  Write-Host '   http://localhost:5173' -ForegroundColor DarkGray
} catch { Write-Warning ("Failed to start Frontend: {0}" -f $_.Exception.Message) }

# BFF (FastAPI) in its own PowerShell window
try {
  Write-Host '-> BFF' -ForegroundColor Green
  $bff = Join-Path $PSScriptRoot 'dev-bff.ps1'
  Start-Process powershell -ArgumentList '-NoExit','-ExecutionPolicy','Bypass','-File', $bff, '-Port', $BffPort -WindowStyle Normal | Out-Null
  Write-Host ("   http://localhost:{0}" -f $BffPort) -ForegroundColor DarkGray
} catch { Write-Warning ("Failed to start BFF: {0}" -f $_.Exception.Message) }

# Orchestrator (Temporal worker) in its own PowerShell window
try {
  Write-Host '-> Orchestrator' -ForegroundColor Green
  $orc = Join-Path $PSScriptRoot 'dev-orchestrator.ps1'
  Start-Process powershell -ArgumentList '-NoExit','-ExecutionPolicy','Bypass','-File', $orc, '-ActivityThreads', $ActivityThreads -WindowStyle Normal | Out-Null
} catch { Write-Warning ("Failed to start Orchestrator: {0}" -f $_.Exception.Message) }

Write-Host 'Host apps launched.' -ForegroundColor Green

