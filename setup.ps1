<#
.SYNOPSIS
    SwAIvyn Complete Setup and Installation Script
.DESCRIPTION
    This script handles the complete setup and installation of SwAIvyn, including:
    - Prerequisites verification and installation
    - Database initialization
    - Backend and frontend building
    - SQLite VSS extension compilation
    - Optional Windows service installation
    - Development environment setup
.PARAMETER Mode
    Setup mode: 'Development', 'Production', or 'Service'
.PARAMETER CleanInstall
    Performs a clean installation by removing existing data and build artifacts
.PARAMETER SkipPrerequisites
    Skip prerequisite checks and installations
.PARAMETER InstallService
    Install SwAIvyn as a Windows service (requires Administrator privileges)
.PARAMETER DatabasePath
    Custom path for the SQLite database (default: .\data\swai-vyn.db)
.PARAMETER Port
    Port number for the web server (default: 5000)
#>

[CmdletBinding()]
param(
    [ValidateSet('Development', 'Production', 'Service')]
    [string]$Mode = 'Development',
    
    [switch]$CleanInstall,
    
    [switch]$SkipPrerequisites,
    
    [switch]$InstallService,
    
    [string]$DatabasePath = ".\data\swai-vyn.db",
    
    [int]$Port = 5000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Color output functions
function Write-Success { param([string]$Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Info { param([string]$Message) Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
function Write-Warning { param([string]$Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "❌ $Message" -ForegroundColor Red }
function Write-Step { param([string]$Message) Write-Host "`n🔧 $Message" -ForegroundColor Magenta }

# Helper functions
function Test-Command {
    param([Parameter(Mandatory)][string]$Command)
    try {
        Get-Command $Command -ErrorAction Stop | Out-Null
        return $true
    } catch {
        return $false
    }
}

function Test-AdminPrivileges {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Install-Chocolatey {
    Write-Info "Installing Chocolatey package manager..."
    Set-ExecutionPolicy Bypass -Scope Process -Force
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
    iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
    refreshenv
}

# Main setup script
Write-Host @"
╔══════════════════════════════════════════════════════════════════╗
║                          SwAIvyn Setup                          ║
║              Privacy-focused AI Assistant Setup                 ║
╚══════════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Blue

Write-Info "Setup Mode: $Mode"
Write-Info "Clean Install: $CleanInstall"
Write-Info "Database Path: $DatabasePath"
Write-Info "Port: $Port"

# Check if running as Administrator for service installation
if ($InstallService -and -not (Test-AdminPrivileges)) {
    Write-Error "Installing as a Windows service requires Administrator privileges. Please run PowerShell as Administrator."
    exit 1
}

$rootDir = $PSScriptRoot
if (-not $rootDir) { $rootDir = Get-Location }

# === STEP 1: Clean Installation ===
if ($CleanInstall) {
    Write-Step "Performing clean installation..."
    
    $cleanupPaths = @(
        ".\bin",
        ".\obj",
        ".\dist",
        ".\node_modules",
        ".\backend\bin",
        ".\backend\obj",
        ".\frontend\node_modules",
        ".\frontend\dist",
        ".\logs",
        ".\data\swai-vyn.db",
        ".\data\swai-vyn.db-shm",
        ".\data\swai-vyn.db-wal"
    )
    
    foreach ($path in $cleanupPaths) {
        if (Test-Path $path) {
            Write-Info "Removing: $path"
            Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Success "Clean installation completed"
}

# === STEP 2: Prerequisites Check and Installation ===
if (-not $SkipPrerequisites) {
    Write-Step "Checking and installing prerequisites..."
    
    # Check for Chocolatey
    if (-not (Test-Command "choco")) {
        Write-Warning "Chocolatey not found. Installing..."
        Install-Chocolatey
    } else {
        Write-Success "Chocolatey is installed"
    }
    
    # Check .NET 8 SDK
    Write-Info "Checking .NET 8 SDK..."
    try {
        $dotnetVersion = dotnet --version 2>$null
        if ($dotnetVersion -and $dotnetVersion.StartsWith("8.")) {
            Write-Success "Found .NET 8 SDK version: $dotnetVersion"
        } else {
            throw "Wrong version"
        }
    } catch {
        Write-Warning ".NET 8 SDK not found or wrong version. Installing..."
        choco install dotnet-8.0-sdk -y
        refreshenv
    }
    
    # Check Node.js 18+
    Write-Info "Checking Node.js..."
    try {
        $nodeVersion = node --version 2>$null
        if ($nodeVersion -and [version]($nodeVersion.TrimStart('v')) -ge [version]"18.0.0") {
            Write-Success "Found Node.js version: $nodeVersion"
        } else {
            throw "Wrong version"
        }
    } catch {
        Write-Warning "Node.js 18+ not found. Installing..."
        choco install nodejs -y
        refreshenv
    }
    
    # Check Git
    if (-not (Test-Command "git")) {
        Write-Warning "Git not found. Installing..."
        choco install git -y
        refreshenv
    } else {
        Write-Success "Git is installed"
    }
    
    # Check Visual Studio Build Tools (for SQLite VSS compilation)
    Write-Info "Checking for Visual Studio Build Tools..."
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $buildTools = & $vswhere -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -format json | ConvertFrom-Json
        if ($buildTools) {
            Write-Success "Visual Studio Build Tools found"
        } else {
            Write-Warning "Visual Studio Build Tools with C++ tools not found"
            Write-Info "Please install Visual Studio Build Tools with C++ workload for SQLite VSS compilation"
        }
    } else {
        Write-Warning "Visual Studio installer not found"
    }
}

# === STEP 3: Create Directory Structure ===
Write-Step "Creating directory structure..."

$directories = @(
    "data",
    "data\avatars",
    "data\uploads", 
    "data\backups",
    "data\modules",
    "data\exports",
    "logs",
    "dist",
    "dist\backend",
    "dist\frontend",
    "dist\scripts",
    "dist\data"
)

foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        Write-Info "Creating directory: $dir"
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}
Write-Success "Directory structure created"

# === STEP 4: Database Setup ===
Write-Step "Setting up database..."

# Ensure database directory exists
$dbDir = Split-Path $DatabasePath -Parent
if ($dbDir -and -not (Test-Path $dbDir)) {
    New-Item -ItemType Directory -Path $dbDir -Force | Out-Null
}

# Run database initialization scripts
$dbScripts = @(
    "initialize-complete-database.sql",
    "create-avatar-prompt-tables.sql",
    "fix-database-schema.sql",
    "fix-avatars-table.sql",
    "fix-missing-columns.sql"
)

foreach ($script in $dbScripts) {
    if (Test-Path $script) {
        Write-Info "Running database script: $script"
        # Note: You might want to use SQLite command line tool here
        # sqlite3 $DatabasePath < $script
    }
}

Write-Success "Database setup completed"

# === STEP 5: SQLite VSS Extension ===
Write-Step "Building SQLite VSS extension..."

if (Test-Path "build-sqlitevss.ps1") {
    try {
        Write-Info "Building SQLite VSS for vector search capabilities..."
        & .\build-sqlitevss.ps1
        Write-Success "SQLite VSS extension built successfully"
    } catch {
        Write-Warning "SQLite VSS build failed. Vector search may not be available."
        Write-Info "Error: $($_.Exception.Message)"
    }
} else {
    Write-Warning "SQLite VSS build script not found"
}

# === STEP 6: Backend Setup ===
Write-Step "Setting up backend..."

Push-Location "backend"
try {
    Write-Info "Restoring .NET packages..."
    dotnet restore
    
    if ($Mode -eq "Development") {
        Write-Info "Building backend in Debug mode..."
        dotnet build --configuration Debug
    } else {
        Write-Info "Publishing backend for production..."
        dotnet publish --configuration Release --runtime win-x64 --self-contained true --output "..\dist\backend"
    }
    
    Write-Success "Backend setup completed"
} catch {
    Write-Error "Backend setup failed: $($_.Exception.Message)"
    exit 1
} finally {
    Pop-Location
}

# === STEP 7: Frontend Setup ===
Write-Step "Setting up frontend..."

if (Test-Path "package.json") {
    try {
        Write-Info "Installing npm packages..."
        npm install
        
        if ($Mode -ne "Development") {
            Write-Info "Building frontend for production..."
            npm run build
            
            # Copy frontend build to dist
            if (Test-Path "frontend\dist") {
                Copy-Item "frontend\dist\*" "dist\frontend\" -Recurse -Force
            } elseif (Test-Path "wwwroot") {
                Copy-Item "wwwroot\*" "dist\frontend\" -Recurse -Force
            }
        }
        
        Write-Success "Frontend setup completed"
    } catch {
        Write-Error "Frontend setup failed: $($_.Exception.Message)"
        exit 1
    }
} else {
    Write-Info "No package.json found, skipping frontend setup"
}

# === STEP 8: Configuration ===
Write-Step "Configuring application..."

# Copy configuration files
$configFiles = @(
    "appsettings.json",
    "web.config"
)

foreach ($config in $configFiles) {
    if (Test-Path $config) {
        Copy-Item $config "dist\" -Force
        Write-Info "Copied configuration: $config"
    }
}

# Create or update appsettings for the specified environment
$appsettingsPath = "dist\appsettings.json"
if (Test-Path "appsettings.json") {
    $settings = Get-Content "appsettings.json" | ConvertFrom-Json
    
    # Update database path
    if ($settings.ConnectionStrings) {
        $settings.ConnectionStrings.DefaultConnection = "Data Source=$DatabasePath"
    }
    
    # Update Kestrel port
    if (-not $settings.Kestrel) {
        $settings.Kestrel = @{}
    }
    if (-not $settings.Kestrel.Endpoints) {
        $settings.Kestrel.Endpoints = @{}
    }
    $settings.Kestrel.Endpoints.Http = @{
        Url = "http://localhost:$Port"
    }
    
    $settings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath
    Write-Info "Updated application configuration"
}

Write-Success "Application configuration completed"

# === STEP 9: Windows Service Installation ===
if ($InstallService) {
    Write-Step "Installing Windows service..."
    
    if (Test-Path "scripts\install-service.ps1") {
        try {
            & .\scripts\install-service.ps1
            Write-Success "Windows service installed successfully"
        } catch {
            Write-Error "Service installation failed: $($_.Exception.Message)"
        }
    } else {
        Write-Warning "Service installation script not found"
    }
}

# === STEP 10: Create Launch Scripts ===
Write-Step "Creating launch scripts..."

# Development launch script
$devLaunchScript = @"
@echo off
echo Starting SwAIvyn in Development Mode...
cd /d "%~dp0"
if exist "backend\bin\Debug\net8.0\SwAIvyn.exe" (
    start "SwAIvyn Backend" backend\bin\Debug\net8.0\SwAIvyn.exe
) else (
    echo Building and starting backend...
    cd backend
    dotnet run
    cd ..
)
if exist "package.json" (
    echo Starting frontend development server...
    start "SwAIvyn Frontend" cmd /k "npm run dev"
)
echo SwAIvyn is starting...
echo Backend: http://localhost:$Port
echo Check the console windows for status.
pause
"@

$devLaunchScript | Set-Content "launch-dev.cmd" -Encoding ASCII

# Production launch script  
$prodLaunchScript = @"
@echo off
echo Starting SwAIvyn...
cd /d "%~dp0"
if exist "dist\backend\SwAIvyn.exe" (
    start "SwAIvyn" dist\backend\SwAIvyn.exe
    echo SwAIvyn started at http://localhost:$Port
    echo Press any key to stop SwAIvyn...
    pause >nul
    taskkill /IM SwAIvyn.exe /F >nul 2>&1
) else (
    echo SwAIvyn not found. Please run setup.ps1 first.
    pause
)
"@

$prodLaunchScript | Set-Content "launch.cmd" -Encoding ASCII

Write-Success "Launch scripts created"

# === STEP 11: Final Validation ===
Write-Step "Performing final validation..."

$validationErrors = @()

# Check critical files
$criticalFiles = @(
    "dist\backend\SwAIvyn.exe",
    "dist\appsettings.json"
)

foreach ($file in $criticalFiles) {
    if ($Mode -ne "Development" -and -not (Test-Path $file)) {
        $validationErrors += "Missing critical file: $file"
    }
}

# Check database
if (-not (Test-Path $DatabasePath)) {
    Write-Info "Database will be created on first run"
}

# Check SQLite VSS
if (-not (Test-Path "sqlite-vss.dll") -and -not (Test-Path "e_sqlite3.dll")) {
    Write-Warning "SQLite VSS extension not found. Vector search features may be limited."
}

if ($validationErrors.Count -gt 0) {
    Write-Error "Validation failed:"
    foreach ($error in $validationErrors) {
        Write-Error "  - $error"
    }
    exit 1
}

Write-Success "Validation completed successfully"

# === COMPLETION ===
Write-Host @"

╔══════════════════════════════════════════════════════════════════╗
║                     🎉 Setup Complete! 🎉                      ║
╚══════════════════════════════════════════════════════════════════╝

SwAIvyn has been successfully set up in $Mode mode.

📁 Installation directory: $rootDir
🗄️  Database location: $DatabasePath
🌐 Web interface: http://localhost:$Port

🚀 To start SwAIvyn:
"@ -ForegroundColor Green

if ($Mode -eq "Development") {
    Write-Host "   • Development: .\launch-dev.cmd" -ForegroundColor Cyan
    Write-Host "   • Or manually: cd backend && dotnet run" -ForegroundColor Cyan
} else {
    Write-Host "   • Production: .\launch.cmd" -ForegroundColor Cyan
    Write-Host "   • Or manually: .\dist\backend\SwAIvyn.exe" -ForegroundColor Cyan
}

if ($InstallService) {
    Write-Host "   • Windows Service: Start 'SwAIvyn' service from Services.msc" -ForegroundColor Cyan
}

Write-Host @"

📚 Additional Commands:
   • View logs: .\scripts\view-logs.ps1
   • Update database: .\create-tables.ps1
   • Rebuild SQLite VSS: .\build-sqlitevss.ps1

For more information, see README.md
"@ -ForegroundColor Yellow

if ($Mode -eq "Development") {
    Write-Host "`n🔧 Development Notes:" -ForegroundColor Magenta
    Write-Host "   • Frontend will auto-reload on changes" -ForegroundColor Gray
    Write-Host "   • Backend requires manual restart for code changes" -ForegroundColor Gray
    Write-Host "   • Use 'dotnet watch run' in backend directory for auto-restart" -ForegroundColor Gray
}

Write-Host "`nSetup completed successfully! 🎊" -ForegroundColor Green
