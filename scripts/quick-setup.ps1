#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick Setup for SwAIvyn - Minimal installation for getting started fast
.DESCRIPTION
    This is a streamlined setup script that gets SwAIvyn running quickly with minimal configuration.
    For advanced setup options, use setup.ps1 instead.
.PARAMETER Dev
    Setup for development (keeps source code, enables hot reload)
.PARAMETER Clean
    Remove existing installation before setup
#>

[CmdletBinding()]
param(
    [switch]$Dev,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

function Write-Status($msg, $color = 'Cyan') { Write-Host "🔄 $msg" -ForegroundColor $color }
function Write-Success($msg) { Write-Host "✅ $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "⚠️ $msg" -ForegroundColor Yellow }

Write-Host @"
┌─────────────────────────────────────────────┐
│            SwAIvyn Quick Setup              │
│         Getting you started fast!          │
└─────────────────────────────────────────────┘
"@ -ForegroundColor Blue

# Quick prerequisite check
Write-Status "Checking prerequisites..."

$missing = @()
try { dotnet --version | Out-Null } catch { $missing += ".NET 8 SDK" }
try { node --version | Out-Null } catch { $missing += "Node.js" }
try { npm --version | Out-Null } catch { $missing += "npm" }

if ($missing) {
    Write-Warn "Missing prerequisites: $($missing -join ', ')"
    Write-Host "Please install them first, then run this script again."
    Write-Host "Or use the full setup.ps1 script for automatic installation."
    exit 1
}

Write-Success "Prerequisites OK"

# Clean if requested
if ($Clean) {
    Write-Status "Cleaning previous installation..."
    $cleanPaths = @("bin", "obj", "dist", "node_modules", "data\swai-vyn.db*")
    foreach ($path in $cleanPaths) {
        if (Test-Path $path) { Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue }
    }
    Write-Success "Cleaned"
}

# Create essential directories
Write-Status "Creating directories..."
@("data", "logs", "data\avatars", "data\uploads") | ForEach-Object {
    if (-not (Test-Path $_)) { New-Item -ItemType Directory -Path $_ -Force | Out-Null }
}

# Setup backend
Write-Status "Setting up backend..."
Push-Location backend
try {
    dotnet restore --quiet
    if ($Dev) {
        dotnet build --configuration Debug --verbosity quiet
    } else {
        dotnet publish --configuration Release --output ..\dist\backend --verbosity quiet
    }
    Write-Success "Backend ready"
} finally {
    Pop-Location
}

# Setup frontend
Write-Status "Setting up frontend..."
if (Test-Path package.json) {
    npm install --silent
    if (-not $Dev) {
        npm run build --silent
    }
    Write-Success "Frontend ready"
}

# Build SQLite VSS if script exists
if (Test-Path "build-sqlitevss.ps1") {
    Write-Status "Building SQLite VSS..."
    try {
        & .\build-sqlitevss.ps1 2>$null
        Write-Success "SQLite VSS built"
    } catch {
        Write-Warn "SQLite VSS build failed (vector search may be limited)"
    }
}

# Create simple launch script
$launcher = if ($Dev) {@"
@echo off
title SwAIvyn Development
cd /d "%~dp0\backend"
echo Starting SwAIvyn in development mode...
echo Open http://localhost:5000 in your browser
dotnet run
"@} else {@"
@echo off
title SwAIvyn
cd /d "%~dp0"
echo Starting SwAIvyn...
echo Open http://localhost:5000 in your browser
start http://localhost:5000
dist\backend\SwAIvyn.exe
"@}

$launcher | Out-File -FilePath "start.cmd" -Encoding ASCII

Write-Host @"

🎉 Quick setup complete!

To start SwAIvyn:
  • Run: start.cmd
  • Or double-click start.cmd
  • Then open: http://localhost:5000

$(if ($Dev) {"💻 Development mode: Changes to backend require restart"} else {"🚀 Production mode: Ready to run"})

For advanced setup options, use: .\setup.ps1 -h
"@ -ForegroundColor Green
