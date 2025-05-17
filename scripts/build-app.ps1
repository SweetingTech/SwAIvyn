# Main build script for SwAIvyn
param (
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipFrontend = $false,
    [string]$OutputDir = "dist"
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot
$distDir = Join-Path $rootDir $OutputDir

# Create the output directory if it doesn't exist
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
}

# Build the frontend
if (-not $SkipFrontend) {
    Write-Host "Building frontend..." -ForegroundColor Cyan

    # Navigate to frontend directory
    Push-Location "$rootDir\frontend"

    # Install dependencies if needed
    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing frontend dependencies..."
        npm install
        if ($LASTEXITCODE -ne 0) {
            Pop-Location
            Write-Error "Frontend dependency installation failed with exit code $LASTEXITCODE"
            exit 1
        }
    }

    # Build the frontend
    npm run build
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Error "Frontend build failed with exit code $LASTEXITCODE"
        exit 1
    }

    # Return to original directory
    Pop-Location

    # Create the wwwroot directory in backend if it doesn't exist
    $wwwrootDir = Join-Path $rootDir "backend\wwwroot"
    if (-not (Test-Path $wwwrootDir)) {
        New-Item -ItemType Directory -Path $wwwrootDir | Out-Null
    }

    # Copy the built frontend files to the backend's wwwroot folder
    Write-Host "Copying frontend files to backend..."
    Copy-Item -Path "$rootDir\frontend\dist\*" -Destination $wwwrootDir -Recurse -Force
}

# Build the backend
Write-Host "Building backend..." -ForegroundColor Cyan
Push-Location "$rootDir\backend"

# Ensure the icon file exists
$iconPath = Join-Path $rootDir "backend\app-icon.ico"
if (-not (Test-Path $iconPath)) {
    Write-Warning "Icon file not found at: $iconPath"
    Write-Warning "Application will be built without an icon."
}

# Publish the backend as a self-contained application
dotnet publish "SwAIvyn.csproj" `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output "$distDir" `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:EnableCompressionInSingleFile=true

$buildResult = $LASTEXITCODE
Pop-Location

if ($buildResult -ne 0) {
    Write-Error "Backend build failed with exit code $buildResult"
    exit 1
}

# Create a shortcut to the executable
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$rootDir\SwAIvyn.lnk")
$Shortcut.TargetPath = "$distDir\SwAIvyn.exe"
$Shortcut.Save()

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Executable is located at: $distDir\SwAIvyn.exe" -ForegroundColor Green
Write-Host "A shortcut has been created at: $rootDir\SwAIvyn.lnk" -ForegroundColor Green
