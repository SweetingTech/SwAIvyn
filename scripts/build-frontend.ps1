# Build script for the frontend
param (
    [string]$OutputPath = "../backend/wwwroot"
)

# Ensure we're in the frontend directory
Set-Location $PSScriptRoot/../frontend

# Install dependencies if needed
if (-not (Test-Path "node_modules")) {
    Write-Host "Installing frontend dependencies..."
    npm install
}

# Build the frontend
Write-Host "Building frontend..."
npm run build

# Create the output directory if it doesn't exist
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath | Out-Null
}

# Copy the built files to the backend's wwwroot folder
Write-Host "Copying built files to $OutputPath..."
Copy-Item -Path "dist/*" -Destination $OutputPath -Recurse -Force

# Copy character assets
$CharacterSourcePath = "AI"
$CharacterDestPath = Join-Path $OutputPath "character-images"

if (Test-Path $CharacterSourcePath) {
    Write-Host "Copying character assets from $CharacterSourcePath to $CharacterDestPath..."
    Copy-Item -Path "$CharacterSourcePath/*" -Destination $CharacterDestPath -Recurse -Force
} else {
    Write-Host "Character source path $CharacterSourcePath not found. Skipping character assets copy."
}

Write-Host "Frontend build complete!" -ForegroundColor Green
