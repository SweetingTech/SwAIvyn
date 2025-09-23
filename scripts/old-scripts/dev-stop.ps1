# Stops host app processes and infra containers
param(
    [switch]$Down = $false,
    [switch]$IncludeTTS = $false,
    [string]$ComposeFile = "$PSScriptRoot/../docker-compose.yml"
)

$ErrorActionPreference = 'Stop'
$stateFile = Join-Path $PSScriptRoot '.dev-state.json'

function Stop-IfPid {
    param([int]$Pid,[string]$Name)
    if (-not $Pid) { return }
    try {
        $p = Get-Process -Id $Pid -ErrorAction SilentlyContinue
        if ($p) {
            Write-Host "Stopping $Name (PID $Pid)..." -ForegroundColor Yellow
            Stop-Process -Id $Pid -Force -ErrorAction SilentlyContinue
        }
    } catch { }
}

if (Test-Path $stateFile) {
    $state = Get-Content -Raw $stateFile | ConvertFrom-Json
    if ($state.pids) {
        Stop-IfPid -Pid $state.pids.bff -Name 'BFF'
        Stop-IfPid -Pid $state.pids.orchestrator -Name 'Orchestrator'
        Stop-IfPid -Pid $state.pids.frontend -Name 'Frontend'
        Stop-IfPid -Pid $state.pids.tts -Name 'TTS (host)'
    }
    Remove-Item $stateFile -Force -ErrorAction SilentlyContinue | Out-Null
}

if (Get-Command docker -ErrorAction SilentlyContinue) {
    if ($Down) {
        Write-Host 'Bringing Compose project down (all services)...' -ForegroundColor Cyan
        docker compose -f $ComposeFile down --remove-orphans
    } else {
        $infra = @('swai-db','temporal','qdrant','neo4j','stt','tts-11labs-adapter')
        if ($IncludeTTS) { $infra += 'tts' }
        Write-Host 'Stopping infra containers...' -ForegroundColor Cyan
        docker compose -f $ComposeFile stop @infra
    }
}

Write-Host 'Shutdown complete.' -ForegroundColor Green
