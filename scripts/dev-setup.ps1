# SwAIvyn Development Environment Setup Script
# This script sets up the development environment for SwAIvyn

# Check .NET SDK
Write-Host "Checking .NET SDK..."
$dotnetVersion = dotnet --version
if ($LASTEXITCODE -ne 0) {
    Write-Error ".NET SDK not found. Please install .NET 7.0 SDK or later from https://dotnet.microsoft.com/download"
    exit 1
}
Write-Host "Found .NET SDK version: $dotnetVersion"

# Check Node.js
Write-Host "Checking Node.js..."
$nodeVersion = node --version
if ($LASTEXITCODE -ne 0) {
    Write-Error "Node.js not found. Please install Node.js v16 or later from https://nodejs.org/"
    exit 1
}
Write-Host "Found Node.js version: $nodeVersion"

# Check npm
Write-Host "Checking npm..."
$npmVersion = npm --version
if ($LASTEXITCODE -ne 0) {
    Write-Error "npm not found. Please install Node.js which includes npm."
    exit 1
}
Write-Host "Found npm version: $npmVersion"

# Create data directories if they don't exist
$dataDirectories = @(
    "data",
    "data\avatars",
    "data\uploads",
    "data\backups",
    "data\modules"
)

foreach ($dir in $dataDirectories) {
    if (-not (Test-Path $dir)) {
        Write-Host "Creating directory: $dir"
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
}

# Install backend dependencies
Write-Host "Setting up backend..."
Set-Location backend
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to restore .NET packages."
    exit 1
}

# Install frontend dependencies
Write-Host "Setting up frontend..."
Set-Location ..\frontend
npm install
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to install npm packages."
    exit 1
}

# Return to root directory
Set-Location ..

Write-Host "Development environment setup complete!" -ForegroundColor Green
Write-Host "To start the backend: cd backend && dotnet run"
Write-Host "To start the frontend: cd frontend && npm start"
