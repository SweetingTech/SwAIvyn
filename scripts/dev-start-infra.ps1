# Starts only infra services in Docker (databases, queues, adapters)
param(
    [string]$ComposeFile = "$PSScriptRoot/../docker-compose.yml",
    [switch]$IncludeTTS = $false
)

$ErrorActionPreference = 'Stop'

function Test-Command {
    param([Parameter(Mandatory)][string]$Name)
    try { Get-Command $Name -ErrorAction Stop | Out-Null; return $true } catch { return $false }
}

if (-not (Test-Command docker)) {
    Write-Error 'Docker is not available in PATH. Install/Start Docker Desktop first.'
}

Write-Host 'Starting infra containers (DBs, queues, adapters)...' -ForegroundColor Cyan

$infra = @('swai-db','temporal','qdrant','neo4j','stt','tts-11labs-adapter')
if (-not $IncludeTTS) {
    # The TTS service is very heavy; skip unless explicitly requested
    $infra = $infra | Where-Object { $_ -ne 'tts' }
}

docker compose -f $ComposeFile up -d --no-build @infra

Write-Host 'Infra status:' -ForegroundColor Yellow
docker compose -f $ComposeFile ps @infra

Write-Host "\nNotes:" -ForegroundColor DarkGray
Write-Host "- swai-db: localhost:5432 (POSTGRES_PASSWORD from .env)" -ForegroundColor DarkGray
Write-Host "- temporal: localhost:7233" -ForegroundColor DarkGray
Write-Host "- qdrant: localhost:6333" -ForegroundColor DarkGray
Write-Host "- neo4j: http://localhost:7474 (bolt://localhost:7687)" -ForegroundColor DarkGray
Write-Host "- stt: http://localhost:9000" -ForegroundColor DarkGray
Write-Host "- tts-11labs-adapter: http://localhost:8082" -ForegroundColor DarkGray

