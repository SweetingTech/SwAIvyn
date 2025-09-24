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
# Ensure Vite proxies API to BFF (5000) by default
if (-not $env:VITE_PROXY_TARGET) { $env:VITE_PROXY_TARGET = 'http://localhost:5000' }


Write-Host "Starting Vite dev server on http://0.0.0.0:$Port" -ForegroundColor Green
npm run dev -- --host 0.0.0.0 --port $Port
