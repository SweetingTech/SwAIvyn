# Start the full SwAIvyn stack using Docker Compose
param (
    [string]$ComposeFile = "$PSScriptRoot/../docker-compose.yml"
)

Write-Host "Starting SwAIvyn stack via Docker Compose..." -ForegroundColor Cyan
docker compose -f $ComposeFile up --build -d --remove-orphans
