# Script to view SwAIvyn logs
param (
    [string]$LogDirectory = "",
    [switch]$ShowCrashLogs = $false,
    [switch]$ShowLatest = $true,
    [int]$Lines = 50
)

# Determine the logs directory
if ([string]::IsNullOrEmpty($LogDirectory)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $rootDir = Split-Path -Parent $scriptDir
    $LogDirectory = Join-Path $rootDir "logs"
}

$ErrorActionPreference = "Stop"

# Ensure the log directory exists
if (-not (Test-Path $LogDirectory)) {
    Write-Error "Log directory not found: $LogDirectory"
    exit 1
}

# Function to display a log file
function Show-LogFile {
    param (
        [string]$FilePath,
        [int]$Lines
    )

    Write-Host "`n===== $FilePath =====" -ForegroundColor Cyan

    if (Test-Path $FilePath) {
        Get-Content -Path $FilePath -Tail $Lines
    } else {
        Write-Host "File not found: $FilePath" -ForegroundColor Red
    }

    Write-Host "=====================`n" -ForegroundColor Cyan
}

# Get all log files
$logFiles = @()

if ($ShowCrashLogs) {
    $crashLogs = Get-ChildItem -Path $LogDirectory -Filter "crash_*.txt" | Sort-Object LastWriteTime -Descending
    $logFiles += $crashLogs
}

$appLogs = Get-ChildItem -Path $LogDirectory -Filter "SwAIvyn_*.log" | Sort-Object LastWriteTime -Descending
$logFiles += $appLogs

$otherLogs = Get-ChildItem -Path $LogDirectory -Filter "*.log" | Where-Object { $_.Name -notlike "SwAIvyn_*.log" } | Sort-Object LastWriteTime -Descending
$logFiles += $otherLogs

# Display logs
if ($logFiles.Count -eq 0) {
    Write-Host "No log files found in $LogDirectory" -ForegroundColor Yellow
    exit 0
}

if ($ShowLatest) {
    # Show only the most recent log
    Show-LogFile -FilePath $logFiles[0].FullName -Lines $Lines
} else {
    # Show all logs
    foreach ($log in $logFiles) {
        Show-LogFile -FilePath $log.FullName -Lines $Lines
    }
}

# Display summary
Write-Host "Log Summary:" -ForegroundColor Green
Write-Host "Total log files: $($logFiles.Count)" -ForegroundColor Green
Write-Host "Most recent log: $($logFiles[0].Name) - $($logFiles[0].LastWriteTime)" -ForegroundColor Green

if ($ShowLatest) {
    Write-Host "`nTo see all logs, run: .\view-logs.ps1 -ShowLatest:`$false" -ForegroundColor Yellow
    Write-Host "To see crash logs, run: .\view-logs.ps1 -ShowCrashLogs" -ForegroundColor Yellow
}
