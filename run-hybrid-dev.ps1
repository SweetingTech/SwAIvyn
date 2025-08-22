# This script starts the SwAIvyn application in a hybrid configuration.
# It launches the supporting services (Weaviate, TTS, Frontend) in Docker
# and runs the backend directly on the host machine.

# Ensure Docker is running before executing this script.
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "Docker is not installed or not in the system's PATH. Please install Docker and try again."
    exit 1
}

if ((docker info) -match "error during connect") {
    Write-Host "Docker is not running. Please start Docker Desktop and try again."
    exit 1
}

# Start the supporting services in Docker
Write-Host "Starting supporting services in Docker..."
docker-compose -f docker-compose.hybrid.yml up -d --build

# Wait for Weaviate to be ready by polling its readiness endpoint from the host
Write-Host "Waiting for Weaviate to be ready..."
$weaviateUrl = "http://localhost:8080/v1/.well-known/ready"
$timeoutSeconds = 300
$deadline = (Get-Date).AddSeconds($timeoutSeconds)
while ($true) {
    try {
        $resp = Invoke-WebRequest -Uri $weaviateUrl -UseBasicParsing -TimeoutSec 5
        if ($resp.StatusCode -eq 200) { Write-Host "Weaviate is ready."; break }
    } catch { }
    if ((Get-Date) -gt $deadline) { Write-Error "Timed out waiting for Weaviate at $weaviateUrl"; exit 1 }
    Start-Sleep -Seconds 2
}

# Start the backend
Write-Host "Starting the backend..."
cd backend
dotnet run
