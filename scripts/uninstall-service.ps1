# SwAIvyn Windows Service Uninstallation Script
# This script removes the SwAIvyn Windows service

param (
    [string]$ServiceName = "SwAIvyn"
)

# Check if running as administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator. Please restart PowerShell as Administrator and try again."
    exit 1
}

# Check if the service exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existingService) {
    Write-Warning "Service '$ServiceName' does not exist. Nothing to uninstall."
    exit 0
}

# Stop the service
Write-Host "Stopping service '$ServiceName'..."
Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Delete the service
Write-Host "Removing service '$ServiceName'..."
sc.exe delete $ServiceName

Write-Host "Service uninstalled successfully."
