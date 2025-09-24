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

# Build the frontend, unless -SkipFrontend was passed
if (-not $SkipFrontend) {
    Write-Host "Building frontend..." -ForegroundColor Cyan

    # Navigate to frontend folder
    Push-Location "$rootDir\frontend"

    # Clean previous build artifacts and caches
    Write-Host "Cleaning previous frontend build..." -ForegroundColor Yellow
    if (Test-Path "dist") {
        Remove-Item -Path "dist" -Recurse -Force
        Write-Host "Cleaned frontend dist directory" -ForegroundColor Green
    }

    # Clear Vite cache
    if (Test-Path "node_modules\.vite") {
        Remove-Item -Path "node_modules\.vite" -Recurse -Force
        Write-Host "Cleaned Vite cache" -ForegroundColor Green
    }

    # Clear additional cache directories
    $cacheDirs = @(".vite", "node_modules\.cache", ".cache")
    foreach ($cacheDir in $cacheDirs) {
        if (Test-Path $cacheDir) {
            Remove-Item -Path $cacheDir -Recurse -Force
            Write-Host "Cleaned $cacheDir" -ForegroundColor Green
        }
    }

    # Note: npm cache clean can cause issues, so we skip it
    Write-Host "Skipping npm cache clean to avoid build interruption" -ForegroundColor Yellow

    # Clean node_modules if CleanAll is specified
    if ($CleanAll -and (Test-Path "node_modules")) {
        Write-Host "Cleaning node_modules for complete rebuild..." -ForegroundColor Yellow
        Remove-Item -Path "node_modules" -Recurse -Force
        Write-Host "Cleaned node_modules directory" -ForegroundColor Green
    }

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

    # Run the frontend build with no cache
    npm run build -- --force
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Error "Frontend build failed with exit code $LASTEXITCODE"
        exit 1
    }

    # Return out of the frontend folder
    Pop-Location

    # Clean and recreate backend\wwwroot
    $wwwrootDir = Join-Path $rootDir "backend\wwwroot"
    Write-Host "Cleaning previous backend wwwroot..." -ForegroundColor Yellow
    if (Test-Path $wwwrootDir) {
        Remove-Item -Path $wwwrootDir -Recurse -Force
        Write-Host "Cleaned backend wwwroot directory" -ForegroundColor Green
    }
    New-Item -ItemType Directory -Path $wwwrootDir -Force | Out-Null

    # Copy everything from frontend/dist into backend/wwwroot
    Write-Host "Copying frontend files to backend..." -ForegroundColor Cyan
    Copy-Item -Path "$rootDir\frontend\dist\*" -Destination $wwwrootDir -Recurse -Force
}

# Clean previous backend build artifacts
Write-Host "Cleaning previous backend build..." -ForegroundColor Yellow
if (Test-Path $dllDir) {
    Remove-Item -Path $dllDir -Recurse -Force
    Write-Host "Cleaned dll directory" -ForegroundColor Green
}

# Clean root executable
$rootExePath = Join-Path $distDir "SwAIvyn.exe"
if (Test-Path $rootExePath) {
    Remove-Item -Path $rootExePath -Force
    Write-Host "Cleaned root SwAIvyn.exe" -ForegroundColor Green
}

# Recreate dll directory
New-Item -ItemType Directory -Path $dllDir -Force | Out-Null

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
        # Normalize both paths
        $normalizedSource = (Resolve-Path $sourceDll).Path
        $normalizedDest = if (Test-Path $destDll) { (Resolve-Path $destDll).Path } else { $destDll }

        # Only copy if they're not already identical
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

# Copy the main .exe up to the root so it's easy to find
$exePath = Join-Path $dllDir "SwAIvyn.exe"
$rootExePath = Join-Path $distDir "SwAIvyn.exe"
if (Test-Path $exePath) {
    Copy-Item $exePath -Destination $rootExePath -Force
    Write-Host "Copied main executable to root directory" -ForegroundColor Green
}

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Executable is located at: $rootExePath" -ForegroundColor Green
Write-Host "DLL files are organized in: $dllDir" -ForegroundColor Green
Write-Host "You can double-click SwAIvyn.exe in the root directory to run the application." -ForegroundColor Green
