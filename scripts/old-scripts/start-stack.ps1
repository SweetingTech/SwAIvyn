# Start the full SwAIvyn stack using Docker Compose
param (
    [string]$ComposeFile = "$PSScriptRoot/../docker-compose.yml"
)

Write-Host "Starting SwAIvyn stack via Docker Compose..." -ForegroundColor Cyan

# Support both Docker Compose v1 and v2 syntax
if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
    docker-compose -f $ComposeFile up --build -d --remove-orphans
} else {
    docker compose -f $ComposeFile up --build -d --remove-orphans
}
