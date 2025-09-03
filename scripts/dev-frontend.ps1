# Runs the Vite dev server for the frontend on host
param(
    [int]$Port = 5173
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/.."
Set-Location (Join-Path $root 'frontend')

if (-not (Test-Path 'node_modules')) {
    Write-Host 'Installing npm dependencies...' -ForegroundColor Cyan
    npm ci
}

Write-Host "Starting Vite dev server on http://localhost:$Port" -ForegroundColor Green
npm run dev -- --port $Port

