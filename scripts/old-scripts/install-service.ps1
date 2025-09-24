# SwAIvyn Windows Service Installation Script
# This script installs the SwAIvyn application as a Windows service

param (
    [string]$ServiceName = "SwAIvyn",
    [string]$DisplayName = "SwAIvyn AI Assistant",
    [string]$Description = "Privacy-focused AI assistant that runs on your local network",
    [string]$BinaryPath = (Join-Path $PSScriptRoot "..\backend\bin\Release\net7.0\win-x64\publish\SwAIvyn.exe")
)

# Check if running as administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator. Please restart PowerShell as Administrator and try again."
    exit 1
}

# Check if the service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Warning "Service '$ServiceName' already exists. Removing it first..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
}

# Check if the binary exists
if (-not (Test-Path $BinaryPath)) {
    Write-Error "Binary not found at path: $BinaryPath. Please build the application first."
    exit 1
}

# Create the service
Write-Host "Creating service '$ServiceName'..."
$result = New-Service -Name $ServiceName -BinaryPathName $BinaryPath -DisplayName $DisplayName -Description $Description -StartupType Automatic

if ($result) {
    Write-Host "Service created successfully."
    Write-Host "Starting service..."
    Start-Service -Name $ServiceName
    Write-Host "Service started. You can manage it from the Services console or with PowerShell commands."
} else {
    Write-Error "Failed to create service."
    exit 1
}

# Output service status
Get-Service -Name $ServiceName | Format-List
