# SwAIvyn Launcher Script
param (
    [string]$ExecutablePath = "$PSScriptRoot/../backend/bin/Release/win-x64/publish/SwAIvyn.exe",
    [switch]$InstallAsService = $false
)

$ErrorActionPreference = "Stop"

# Check if the executable exists
if (-not (Test-Path $ExecutablePath)) {
    Write-Error "Executable not found at path: $ExecutablePath. Please build the application first."
    exit 1
}

# If installing as a service
if ($InstallAsService) {
    Write-Host "Installing SwAIvyn as a Windows service..." -ForegroundColor Cyan
    & "$PSScriptRoot\install-service.ps1" -BinaryPath $ExecutablePath
    exit $LASTEXITCODE
}

# Otherwise, just run the application
Write-Host "Starting SwAIvyn..." -ForegroundColor Cyan
& $ExecutablePath
