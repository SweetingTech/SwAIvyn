<# =========================================================================
  re_run.ps1  - SwAIvyn quick rebuild and restart

  This script performs quick rebuilds for development:
  - Frontend build (optional)
  - Backend compilation
  - Skips database initialization (assumes already set up)

  Usage examples:

      # Quick rebuild (includes frontend)
      .\re_run.ps1

      # Backend only rebuild
      .\re_run.ps1 -SkipFrontend

  For first-time setup, use first_run.ps1 instead.
=========================================================================== #>

param (
    [string] $Configuration = "Release",
    [string] $Runtime       = "win-x64",
    [switch] $SkipFrontend  = $false,
    [string] $OutputDir     = ".",
    [switch] $CleanAll      = $false
)

$ErrorActionPreference = "Stop"

# ------------------------------------------------------------------ Paths
$rootDir    = Split-Path -Parent $PSScriptRoot
$distDir    = Join-Path $rootDir $OutputDir
$dllDir     = Join-Path $rootDir "dll"
$wwwrootDir = Join-Path $rootDir "backend\wwwroot"

Write-Host "==> SwAIvyn Quick Rebuild" -ForegroundColor Cyan
Write-Host "Root: $rootDir" -ForegroundColor Gray

# ---------------------------------------------------------------- Frontend
if (-not $SkipFrontend) {
    Write-Host "==> Building frontend..." -ForegroundColor Cyan
    Push-Location "$rootDir\frontend"

    # Clean dist and caches
    Write-Host "Cleaning old frontend build..." -ForegroundColor Yellow
    if (Test-Path "dist") { Remove-Item "dist" -Recurse -Force }

    $cacheDirs = @(".vite","node_modules\.vite","node_modules\.cache",".cache")
    foreach ($dir in $cacheDirs) {
        if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    }

    if ($CleanAll -and (Test-Path "node_modules")) {
        Write-Host "Removing node_modules..." -ForegroundColor Yellow
        Remove-Item "node_modules" -Recurse -Force
    }

    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing frontend deps..." -ForegroundColor Cyan
        npm install
        if ($LASTEXITCODE) { throw "npm install failed ($LASTEXITCODE)" }
    }

    npm run build -- --force
    if ($LASTEXITCODE) { throw "Frontend build failed ($LASTEXITCODE)" }

    Pop-Location

    # Copy built assets to backend/wwwroot
    if (Test-Path $wwwrootDir) { Remove-Item $wwwrootDir -Recurse -Force }
    New-Item -ItemType Directory -Path $wwwrootDir | Out-Null
    Copy-Item "$rootDir\frontend\dist\*" $wwwrootDir -Recurse -Force
}

# ------------------------------------------------ clean backend bin
Write-Host "Cleaning backend bin..." -ForegroundColor Yellow
if (Test-Path $dllDir) { Remove-Item $dllDir -Recurse -Force }
if (Test-Path (Join-Path $distDir "SwAIvyn.exe")) {
    Remove-Item (Join-Path $distDir "SwAIvyn.exe") -Force
}
New-Item -ItemType Directory -Path $dllDir | Out-Null

# ------------------------------------------------- backend publish
Write-Host "==> Publishing backend..." -ForegroundColor Cyan
Push-Location "$rootDir\backend"

$iconPath = Join-Path $rootDir "backend\app-icon.ico"
if (-not (Test-Path $iconPath)) {
    Write-Warning "Icon file not found - building without icon."
}

dotnet publish "SwAIvyn.csproj" `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $dllDir `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:EnableCompressionInSingleFile=true

if ($LASTEXITCODE) {
    Pop-Location
    throw "dotnet publish failed ($LASTEXITCODE)"
}

# ------------------------------------------------ copy extra native DLLs
$extraDir  = Join-Path $rootDir "dnd_dll"
$dllExtras = @("sqlite-vss.dll","faiss.dll","libopenblas.dll")
foreach ($file in $dllExtras) {
    $src = Join-Path $extraDir $file
    if (Test-Path $src) {
        Copy-Item $src -Destination (Join-Path $dllDir $file) -Force
    } else {
        Write-Warning "$file missing in dnd_dll"
    }
}

Pop-Location

# ------------------------------------------------ copy exe to root
$exeSrc  = Join-Path $dllDir "SwAIvyn.exe"
$exeDest = Join-Path $distDir "SwAIvyn.exe"
if (Test-Path $exeSrc) { Copy-Item $exeSrc $exeDest -Force }

Write-Host "`nQuick rebuild completed successfully!" -ForegroundColor Green
Write-Host "Executable : $exeDest" -ForegroundColor White
Write-Host "DLL folder : $dllDir" -ForegroundColor White
Write-Host "Run        : double-click SwAIvyn.exe or launch via CLI.`n" -ForegroundColor White