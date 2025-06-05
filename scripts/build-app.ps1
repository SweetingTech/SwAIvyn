# Main build script for SwAIvyn
param (
    [string] $Configuration = "Release",
    [string] $Runtime       = "win-x64",
    [switch] $SkipFrontend  = $false,
    [string] $OutputDir     = "."  # Changed to root directory
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot
$distDir = Join-Path $rootDir $OutputDir
$dllDir  = Join-Path $rootDir "dll"

# Build the frontend, unless -SkipFrontend was passed
if (-not $SkipFrontend) {
    Write-Host "Building frontend..." -ForegroundColor Cyan

    # Navigate to frontend folder
    Push-Location "$rootDir\frontend"

    # Install dependencies if node_modules is missing
    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing frontend dependencies..."
        npm install
        if ($LASTEXITCODE -ne 0) {
            Pop-Location
            Write-Error "Frontend dependency installation failed with exit code $LASTEXITCODE"
            exit 1
        }
    }

    # Run the frontend build
    npm run build
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Error "Frontend build failed with exit code $LASTEXITCODE"
        exit 1
    }

    # Return out of the frontend folder
    Pop-Location

    # Make sure backend\wwwroot exists
    $wwwrootDir = Join-Path $rootDir "backend\wwwroot"
    if (-not (Test-Path $wwwrootDir)) {
        New-Item -ItemType Directory -Path $wwwrootDir | Out-Null
    }

    # Copy everything from frontend/dist into backend/wwwroot
    Write-Host "Copying frontend files to backend..." -ForegroundColor Cyan
    Copy-Item -Path "$rootDir\frontend\dist\*" `
              -Destination $wwwrootDir `
              -Recurse -Force
}

# Build the backend
Write-Host "Building backend..." -ForegroundColor Cyan
Push-Location "$rootDir\backend"

# Check for the icon file
$iconPath = Join-Path $rootDir "backend\app-icon.ico"
if (-not (Test-Path $iconPath)) {
    Write-Warning "Icon file not found at: $iconPath"
    Write-Warning "Application will be built without an icon."
}

# Publish the backend as a self-contained single-file app
# Note: the backtick (`) must be the very last character on the line—no trailing spaces
dotnet publish "SwAIvyn.csproj" `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output "$dllDir" `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:EnableCompressionInSingleFile=true

# Copy SQLite-VSS and dependencies from root into the published dll folder
$buildFilesDir = $rootDir
$dllFiles     = @(
    "sqlite-vss.dll",
    "faiss.dll",
    "libopenblas.dll"
)

Write-Host "Copying DLL files from build_files directory..." -ForegroundColor Cyan

foreach ($dllFile in $dllFiles) {
    $sourceDll = Join-Path $buildFilesDir $dllFile
    $destDll   = Join-Path $dllDir $dllFile

    if (Test-Path $sourceDll) {
        # Normalize both paths
        $normalizedSource = (Resolve-Path $sourceDll).Path
        $normalizedDest   = if (Test-Path $destDll) { (Resolve-Path $destDll).Path } else { $destDll }

        # Only copy if they're not already identical
        if ($normalizedSource -ne $normalizedDest) {
            Copy-Item $sourceDll -Destination $destDll -Force
            Write-Host "✓ Copied $dllFile from build_files" -ForegroundColor Green
        } else {
            Write-Host "✓ $dllFile already in correct location" -ForegroundColor Green
        }
    } else {
        Write-Warning "⚠ $dllFile not found in build_files directory"
    }
}

$buildResult = $LASTEXITCODE
Pop-Location

if ($buildResult -ne 0) {
    Write-Error "Backend build failed with exit code $buildResult"
    exit 1
}

# Copy the main .exe up to the root so it's easy to find
$exePath     = Join-Path $dllDir "SwAIvyn.exe"
$rootExePath = Join-Path $distDir "SwAIvyn.exe"
if (Test-Path $exePath) {
    Copy-Item $exePath -Destination $rootExePath -Force
    Write-Host "✓ Copied main executable to root directory" -ForegroundColor Green
}

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Executable is located at: $rootExePath" -ForegroundColor Green
Write-Host "DLL files are organized in: $dllDir" -ForegroundColor Green
Write-Host "You can double-click SwAIvyn.exe in the root directory to run the application." -ForegroundColor Green
