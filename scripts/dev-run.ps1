# Development script for SwAIvyn - runs frontend and backend with hot reloading
param (
    [switch] $FrontendOnly = $false,
    [switch] $BackendOnly = $false
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot

Write-Host "Starting SwAIvyn in development mode..." -ForegroundColor Cyan

if (-not $BackendOnly) {
    # Start frontend dev server
    Write-Host "Starting frontend development server..." -ForegroundColor Green
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$rootDir\frontend'; npm run dev" -WindowStyle Normal
    Write-Host "Frontend dev server starting at http://localhost:5173" -ForegroundColor Green
}

if (-not $FrontendOnly) {
    # Wait a moment for frontend to start
    if (-not $BackendOnly) {
        Start-Sleep -Seconds 2
    }
    
    # Start backend
    Write-Host "Starting backend development server..." -ForegroundColor Green
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "& '$PSScriptRoot\dev-bff.ps1'" -WindowStyle Normal
    Write-Host "Backend dev server starting at http://localhost:5000" -ForegroundColor Green
}

Write-Host "`nDevelopment servers are starting..." -ForegroundColor Yellow
Write-Host "Frontend: http://localhost:5173 (with hot reloading)" -ForegroundColor Cyan
Write-Host "Backend:  http://localhost:5000" -ForegroundColor Cyan

# Also print LAN-access URLs
try {
    $lanIPs = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName()) |
        Where-Object { $_.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork } |
        ForEach-Object { $_.IPAddressToString }
    if ($lanIPs) {
        foreach ($ip in $lanIPs) {
            Write-Host ("LAN Frontend: http://{0}:5173" -f $ip) -ForegroundColor DarkCyan
            Write-Host ("LAN Backend:  http://{0}:5000" -f $ip) -ForegroundColor DarkCyan
        }
    }
} catch {}
Write-Host "`nMake changes to your frontend code and they will automatically refresh!" -ForegroundColor Green
Write-Host "Press Ctrl+C in the terminal windows to stop the servers." -ForegroundColor Yellow
