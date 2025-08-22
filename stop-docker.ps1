# This script stops and removes all running Docker containers.

# Ensure Docker is running before executing this script.
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "Docker is not installed or not in the system's PATH. Please install Docker and try again."
    exit 1
}

if ((docker info) -match "error during connect") {
    Write-Host "Docker is not running. Please start Docker Desktop and try again."
    exit 1
}

Write-Host "Stopping and removing all running Docker containers..."
docker-compose down --remove-orphans
