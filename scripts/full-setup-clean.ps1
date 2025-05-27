# SwAIvyn Complete Setup and Build Script
param (
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$CleanDatabase = $false,
    [switch]$SkipBuild = $false,
    [switch]$SkipFrontend = $false
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "     SwAIvyn Complete Setup Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Function to check if a command exists
function Test-Command {
    param($Command)
    $null = Get-Command $Command -ErrorAction SilentlyContinue
    return $?
}

# Check prerequisites
Write-Host "1. Checking Prerequisites..." -ForegroundColor Yellow

# Check .NET SDK
if (-not (Test-Command "dotnet")) {
    Write-Error ".NET SDK not found. Please install .NET 8.0 SDK or later from https://dotnet.microsoft.com/download"
    exit 1
}
$dotnetVersion = dotnet --version
Write-Host "✓ Found .NET SDK version: $dotnetVersion" -ForegroundColor Green

# Check Node.js
if (-not (Test-Command "node")) {
    Write-Error "Node.js not found. Please install Node.js v18 or later from https://nodejs.org/"
    exit 1
}
$nodeVersion = node --version
Write-Host "✓ Found Node.js version: $nodeVersion" -ForegroundColor Green

# Check npm
if (-not (Test-Command "npm")) {
    Write-Error "npm not found. Please install Node.js which includes npm."
    exit 1
}
$npmVersion = npm --version
Write-Host "✓ Found npm version: $npmVersion" -ForegroundColor Green

Write-Host ""

# Create required directories
Write-Host "2. Creating Required Directories..." -ForegroundColor Yellow

$dataDirectories = @(
    "data",
    "data\avatars",
    "data\uploads", 
    "data\backups",
    "data\modules",
    "data\sessions",
    "logs",
    "dist"
)

foreach ($dir in $dataDirectories) {
    $fullPath = Join-Path $rootDir $dir
    if (-not (Test-Path $fullPath)) {
        Write-Host "Creating directory: $dir" -ForegroundColor Gray
        New-Item -ItemType Directory -Path $fullPath | Out-Null
    }
}
Write-Host "✓ Directory structure verified" -ForegroundColor Green
Write-Host ""

# Database setup
Write-Host "3. Setting Up Database..." -ForegroundColor Yellow

# Build database tools
Write-Host "Building CreateTables tool..." -ForegroundColor Gray
Push-Location (Join-Path $rootDir "tools\CreateTables")
try {
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "CreateTables build failed"
    }
} finally {
    Pop-Location
}

Write-Host "Building UpdateDatabase tool..." -ForegroundColor Gray
Push-Location (Join-Path $rootDir "tools\UpdateDatabase")
try {
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        throw "UpdateDatabase build failed"
    }
} finally {
    Pop-Location
}

# Run database tools
Write-Host "Creating/verifying database tables..." -ForegroundColor Gray
$createTablesPath = Join-Path $rootDir "tools\CreateTables\bin\Release\net8.0\CreateTables.exe"
& $createTablesPath
if ($LASTEXITCODE -ne 0) {
    Write-Warning "CreateTables returned non-zero exit code, but continuing..."
}

Write-Host "Updating database schema..." -ForegroundColor Gray
$updateDatabasePath = Join-Path $rootDir "tools\UpdateDatabase\bin\Release\net8.0\UpdateDatabase.exe"
& $updateDatabasePath
if ($LASTEXITCODE -ne 0) {
    Write-Warning "UpdateDatabase returned non-zero exit code, but continuing..."
}

Write-Host "✓ Database setup completed" -ForegroundColor Green
Write-Host ""

# Backend setup
Write-Host "4. Setting Up Backend..." -ForegroundColor Yellow
Push-Location (Join-Path $rootDir "backend")
try {
    Write-Host "Restoring .NET packages..." -ForegroundColor Gray
    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to restore .NET packages"
    }
} finally {
    Pop-Location
}

Write-Host "✓ Backend dependencies restored" -ForegroundColor Green

if (-not $SkipBuild) {
    Write-Host "Building backend..." -ForegroundColor Gray
    Push-Location (Join-Path $rootDir "backend")
    try {
        dotnet publish -c $Configuration --runtime $Runtime --self-contained true -o (Join-Path $rootDir "dist")
        if ($LASTEXITCODE -ne 0) {
            throw "Backend build failed"
        }
    } finally {
        Pop-Location
    }
    Write-Host "✓ Backend built successfully" -ForegroundColor Green
}

Write-Host ""

# Frontend setup
if (-not $SkipFrontend) {
    Write-Host "5. Setting Up Frontend..." -ForegroundColor Yellow
    
    Push-Location (Join-Path $rootDir "frontend")
    try {
        Write-Host "Installing npm packages..." -ForegroundColor Gray
        npm install
        if ($LASTEXITCODE -ne 0) {
            throw "npm install failed"
        }

        Write-Host "Building frontend..." -ForegroundColor Gray
        npm run build
        if ($LASTEXITCODE -ne 0) {
            throw "Frontend build failed"
        }
    } finally {
        Pop-Location
    }

    # Copy frontend dist to backend wwwroot
    $frontendDist = Join-Path $rootDir "frontend\dist"
    $backendWwwRoot = Join-Path $rootDir "dist\wwwroot"
    
    if (Test-Path $frontendDist) {
        if (Test-Path $backendWwwRoot) {
            Remove-Item $backendWwwRoot -Recurse -Force
        }
        Copy-Item $frontendDist -Destination $backendWwwRoot -Recurse -Force
        Write-Host "✓ Frontend assets copied to backend" -ForegroundColor Green
    }
    
    Write-Host "✓ Frontend setup completed" -ForegroundColor Green
}

Write-Host ""

# Final setup
Write-Host "6. Final Setup..." -ForegroundColor Yellow

if (-not $SkipBuild) {
    # Copy essential files to dist
    $filesToCopy = @{
        "sqlite-vss.dll" = "sqlite-vss.dll"
        "appsettings.json" = "appsettings.json"
        "web.config" = "web.config"
    }

    foreach ($sourceFile in $filesToCopy.Keys) {
        $sourcePath = Join-Path $rootDir $sourceFile
        $destPath = Join-Path $rootDir "dist\$($filesToCopy[$sourceFile])"
        
        if (Test-Path $sourcePath) {
            Copy-Item $sourcePath -Destination $destPath -Force
            Write-Host "Copied $sourceFile to dist/" -ForegroundColor Gray
        }
    }

    # Copy to root for easy access
    $exePath = Join-Path $rootDir "dist\SwAIvyn.exe"
    $rootExePath = Join-Path $rootDir "SwAIvyn.exe"
    if (Test-Path $exePath) {
        Copy-Item $exePath -Destination $rootExePath -Force
        Write-Host "Copied SwAIvyn.exe to root directory" -ForegroundColor Gray
    }

    Write-Host "✓ Essential files copied" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "         Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Configure your AI API keys in appsettings.json" -ForegroundColor Gray
Write-Host "2. Run SwAIvyn.exe to start the application" -ForegroundColor Gray
Write-Host "3. Navigate to http://localhost:5000 in your browser" -ForegroundColor Gray
Write-Host ""
Write-Host "Build artifacts are in the 'dist' folder" -ForegroundColor Cyan
Write-Host "Main executable: SwAIvyn.exe" -ForegroundColor Cyan
