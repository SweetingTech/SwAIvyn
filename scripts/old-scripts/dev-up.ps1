# One-command hybrid dev: starts infra in Docker, then BFF, Orchestrator, Frontend on host
param(
    [switch]$IncludeTTS = $false,
    [int]$BffPort = 5000,
    [int]$FrontPort = 5173,
    [int]$ActivityThreads = 32
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/.."

Write-Host '=== SwAIvyn Hybrid Dev ===' -ForegroundColor Cyan

# 1) Start infra containers
& $PSScriptRoot/dev-start-infra.ps1 -IncludeTTS:$IncludeTTS

# 2) Start host services in separate windows for easy stop/restart
Write-Host 'Launching host BFF (FastAPI)...' -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit","-Command","& '$PSScriptRoot/dev-bff.ps1' -Port $BffPort" -WindowStyle Normal

Write-Host 'Launching host Orchestrator (Temporal worker)...' -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit","-Command","& '$PSScriptRoot/dev-orchestrator.ps1' -ActivityThreads $ActivityThreads" -WindowStyle Normal

Write-Host 'Launching host Frontend (Vite)...' -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit","-Command","& '$PSScriptRoot/dev-frontend.ps1' -Port $FrontPort" -WindowStyle Normal

Write-Host "\nReady:" -ForegroundColor Yellow
Write-Host "- UI:           http://localhost:$FrontPort" -ForegroundColor Yellow
Write-Host "- BFF API:      http://localhost:$BffPort" -ForegroundColor Yellow
Write-Host "- Temporal:     localhost:7233 (containers)" -ForegroundColor Yellow
Write-Host "- Qdrant:       http://localhost:6333 (containers)" -ForegroundColor Yellow
Write-Host "- Postgres:     localhost:5432 (containers)" -ForegroundColor Yellow

