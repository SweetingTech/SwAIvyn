# Simple script to show SwAIvyn logs

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$logsDir = Join-Path $rootDir "logs"

# Create logs directory if it doesn't exist
if (-not (Test-Path $logsDir)) {
    New-Item -Path $logsDir -ItemType Directory -Force | Out-Null
    Write-Host "Created logs directory: $logsDir" -ForegroundColor Yellow
    Write-Host "No logs found yet. Run the application to generate logs." -ForegroundColor Yellow
    exit 0
}

# Get all log files
$logFiles = Get-ChildItem -Path $logsDir -Filter "*.log" | Sort-Object LastWriteTime -Descending
$crashLogs = Get-ChildItem -Path $logsDir -Filter "crash_*.txt" | Sort-Object LastWriteTime -Descending

# Display summary
Write-Host "`nSwAIvyn Logs Summary:" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan
Write-Host "Log files found: $($logFiles.Count)" -ForegroundColor White
Write-Host "Crash logs found: $($crashLogs.Count)" -ForegroundColor White
Write-Host "Logs directory: $logsDir" -ForegroundColor White
Write-Host "======================" -ForegroundColor Cyan

# Show most recent log if available
if ($logFiles.Count -gt 0) {
    $mostRecentLog = $logFiles[0]
    Write-Host "`nMost recent log ($($mostRecentLog.LastWriteTime)):" -ForegroundColor Green
    Write-Host "======================" -ForegroundColor Green
    Get-Content -Path $mostRecentLog.FullName -Tail 20
    Write-Host "======================" -ForegroundColor Green
    Write-Host "Showing last 20 lines of $($mostRecentLog.Name)" -ForegroundColor Green
}

# Show most recent crash log if available
if ($crashLogs.Count -gt 0) {
    $mostRecentCrash = $crashLogs[0]
    Write-Host "`nMost recent crash log ($($mostRecentCrash.LastWriteTime)):" -ForegroundColor Red
    Write-Host "======================" -ForegroundColor Red
    Get-Content -Path $mostRecentCrash.FullName
    Write-Host "======================" -ForegroundColor Red
}

# Show help
Write-Host "`nCommands to view more logs:" -ForegroundColor Yellow
Write-Host "  Get-Content -Path '$logsDir\<log-file-name>' | Select-Object -Last 50" -ForegroundColor Yellow
if ($logFiles.Count -gt 0) {
    Write-Host "  Get-Content -Path '$($mostRecentLog.FullName)' | Select-Object -Last 100" -ForegroundColor Yellow
}
Write-Host "  Get-ChildItem -Path '$logsDir' | Sort-Object LastWriteTime -Descending" -ForegroundColor Yellow
