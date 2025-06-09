#!/usr/bin/env pwsh

# Main build script for SwAIvyn
param (
    [string] $Configuration = "Release",
    [string] $Runtime       = "win-x64",
    [switch] $SkipFrontend  = $false,
    [string] $OutputDir     = ".",
    [switch] $CleanAll      = $false
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot
$distDir = Join-Path $rootDir $OutputDir
$dllDir  = Join-Path $rootDir "dll"

# Navigate to frontend and run npm install
Write-Host "Navigating to frontend directory..." -ForegroundColor Cyan
Push-Location "$rootDir\frontend"

Write-Host "Running npm install in frontend directory..." -ForegroundColor Cyan
npm install
if ($LASTEXITCODE -ne 0) {
    Write-Error "npm install failed with exit code $LASTEXITCODE"
    exit 1
}
Write-Host "npm install completed successfully." -ForegroundColor Green

Pop-Location
Write-Host "Returned to root directory." -ForegroundColor Cyan

# Clean backend/wwwroot directory
$wwwRootDir = Join-Path $rootDir "backend\wwwroot"
Write-Host "Checking backend/wwwroot directory: $wwwRootDir" -ForegroundColor Cyan
if (Test-Path $wwwRootDir) {
    Write-Host "Cleaning backend/wwwroot directory..." -ForegroundColor Yellow
    Get-ChildItem -Path $wwwRootDir -Recurse | Remove-Item -Force -Recurse
    Write-Host "backend/wwwroot directory cleaned." -ForegroundColor Green
} else {
    Write-Host "backend/wwwroot directory does not exist. Skipping cleanup." -ForegroundColor Gray
}

# Skip frontend build - we'll use npm run dev for live development
Write-Host "=== SKIPPING FRONTEND BUILD ===" -ForegroundColor Yellow
Write-Host "Frontend will be served via 'npm run dev' for live development" -ForegroundColor Yellow
Write-Host "Run 'npm run dev' in the frontend directory after the backend is built" -ForegroundColor Cyan

Write-Host "Cleaning previous backend build..." -ForegroundColor Yellow
if (Test-Path $dllDir) {
    Remove-Item -Path $dllDir -Recurse -Force
    Write-Host "Cleaned dll directory" -ForegroundColor Green
}

$rootExePath = Join-Path $distDir "SwAIvyn.exe"
if (Test-Path $rootExePath) {
    Remove-Item -Path $rootExePath -Force
    Write-Host "Cleaned root SwAIvyn.exe" -ForegroundColor Green
}

New-Item -ItemType Directory -Path $dllDir -Force | Out-Null

Write-Host "Building backend..." -ForegroundColor Cyan
Push-Location "$rootDir\backend"

# Ensure old build artifacts are removed
dotnet clean "SwAIvyn.csproj" --configuration $Configuration


# Check for the icon file
$iconPath = Join-Path $rootDir "backend\app-icon.ico"
if (-not (Test-Path $iconPath)) {
    Write-Warning "Icon file not found at: $iconPath"
    Write-Warning "Application will be built without an icon."
}

dotnet publish "SwAIvyn.csproj" --configuration $Configuration --runtime $Runtime --self-contained true --output "$dllDir" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true

# Copy SQLite-VSS and dependencies from dnd_dll folder into the published dll folder
$buildFilesDir = Join-Path $rootDir "dnd_dll"
$dllFiles = @(
    "sqlite-vss.dll",
    "faiss.dll",
    "libopenblas.dll"
)

Write-Host "Copying DLL files from dnd_dll directory..." -ForegroundColor Cyan

foreach ($dllFile in $dllFiles) {
    $sourceDll = Join-Path $buildFilesDir $dllFile
    $destDll = Join-Path $dllDir $dllFile

    if (Test-Path $sourceDll) {
        $normalizedSource = (Resolve-Path $sourceDll).Path
        $normalizedDest = if (Test-Path $destDll) { (Resolve-Path $destDll).Path } else { $destDll }

        if ($normalizedSource -ne $normalizedDest) {
            Copy-Item $sourceDll -Destination $destDll -Force
            Write-Host "Copied $dllFile from dnd_dll directory" -ForegroundColor Green
        } else {
            Write-Host "$dllFile already in correct location" -ForegroundColor Green
        }
    } else {
        Write-Warning "$dllFile not found in dnd_dll directory"
    }
}

$buildResult = $LASTEXITCODE
Pop-Location

if ($buildResult -ne 0) {
    Write-Error "Backend build failed with exit code $buildResult"
    exit 1
}

$exePath = Join-Path $dllDir "SwAIvyn.exe"
$rootExePath = Join-Path $distDir "SwAIvyn.exe"
if (Test-Path $exePath) {
    Copy-Item $exePath -Destination $rootExePath -Force
    Write-Host "Copied main executable to root directory" -ForegroundColor Green
}

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Executable is located at: $rootExePath" -ForegroundColor Green
Write-Host "DLL files are organized in: $dllDir" -ForegroundColor Green
Write-Host ""
Write-Host "=== NEXT STEPS FOR DEVELOPMENT ===" -ForegroundColor Green
Write-Host "1. Start the application: $rootExePath" -ForegroundColor Yellow
Write-Host "   - This will automatically start BOTH backend AND frontend dev server" -ForegroundColor Cyan
Write-Host "   - Fish Speech TTS will auto-start in the background" -ForegroundColor Cyan
Write-Host "2. Access your app at:" -ForegroundColor Cyan
Write-Host "   - Live Development:  http://localhost:5173 (with hot reloading)" -ForegroundColor White
Write-Host "   - Backend API:       http://localhost:5000" -ForegroundColor White
Write-Host "   - Fish Speech TTS:   http://127.0.0.1:8081 (auto-started)" -ForegroundColor White
Write-Host ""
Write-Host "One command starts everything with live reloading!" -ForegroundColor Green