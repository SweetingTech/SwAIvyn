# SwAIvyn First Run Setup Script
# Complete setup including database initialization and character loading

param (
    [switch] $SkipFrontend = $false,
    [switch] $CleanAll = $false
)

$ErrorActionPreference = "Stop"
Write-Host "==> SwAIvyn First Run Setup" -ForegroundColor Cyan

# Clean build artifacts if requested
if ($CleanAll) {
    Write-Host "Cleaning all build artifacts..." -ForegroundColor Yellow
    if (Test-Path "backend\bin") { Remove-Item "backend\bin" -Recurse -Force }
    if (Test-Path "backend\obj") { Remove-Item "backend\obj" -Recurse -Force }
    if (Test-Path "frontend\dist") { Remove-Item "frontend\dist" -Recurse -Force }
    if (Test-Path "frontend\node_modules") { Remove-Item "frontend\node_modules" -Recurse -Force }
}

# Frontend build
if (-not $SkipFrontend) {
    Write-Host "==> Building frontend..." -ForegroundColor Yellow
    Push-Location frontend
    try {
        npm install
        npm run build
        Write-Host "Frontend build completed" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

# Clean backend bin
Write-Host "Cleaning backend bin..." -ForegroundColor Gray
if (Test-Path "backend\bin") {
    Remove-Item "backend\bin" -Recurse -Force
}

# Backend build
Write-Host "==> Publishing backend..." -ForegroundColor Yellow
if (-not (Test-Path "icon.ico")) {
    Write-Host "WARNING: Icon file not found - building without icon." -ForegroundColor Yellow
}

dotnet publish backend -c Release -o . --self-contained true -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Backend build failed!" -ForegroundColor Red
    exit 1
}

# Database setup and validation
Write-Host "==> Database Setup & Validation" -ForegroundColor Yellow

$dbPath = "Sqldatabase\swai-vyn.db"
if (Test-Path $dbPath) {
    Write-Host "Database found at $dbPath, validating..." -ForegroundColor Gray

    # Quick validation - check if it's a valid SQLite file
    try {
        $bytes = [System.IO.File]::ReadAllBytes($dbPath)
        if ($bytes.Length -gt 16) {
            $header = [System.Text.Encoding]::ASCII.GetString($bytes[0..15])
            if ($header -eq "SQLite format 3") {
                Write-Host "✅ Database validation passed - SQLite format confirmed" -ForegroundColor Green
            } else {
                Write-Host "Database validation failed - invalid format" -ForegroundColor Red
                Remove-Item $dbPath -Force
            }
        } else {
            Write-Host "Database file too small, removing..." -ForegroundColor Yellow
            Remove-Item $dbPath -Force
        }
    }
    catch {
        Write-Host "Database validation failed: $($_.Exception.Message)" -ForegroundColor Red
        if (Test-Path $dbPath) { Remove-Item $dbPath -Force }
    }
}

Write-Host "Running database initialization tools to ensure schema is current..." -ForegroundColor Gray

# Build and run CreateTables tool
Write-Host "Building CreateTables tool..." -ForegroundColor Gray
Push-Location "tools\CreateTables"
try {
    dotnet restore --verbosity quiet
    dotnet build -c Release --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Running CreateTables..." -ForegroundColor Gray
        dotnet run -c Release --no-build
    }
}
finally {
    Pop-Location
}

# Build and run UpdateDatabase tool
Write-Host "Building UpdateDatabase tool..." -ForegroundColor Gray
Push-Location "tools\UpdateDatabase"
try {
    dotnet restore --verbosity quiet
    dotnet build -c Release --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Running UpdateDatabase..." -ForegroundColor Gray
        dotnet run -c Release --no-build
    }
}
finally {
    Pop-Location
}

# Copy database to build location for EF compatibility
$buildDbPath = "backend\bin\Debug\net8.0\Sqldatabase"
if (-not (Test-Path $buildDbPath)) {
    New-Item -ItemType Directory -Path $buildDbPath -Force | Out-Null
}
Copy-Item $dbPath "$buildDbPath\swai-vyn.db" -Force
Write-Host "Database copied to build location for EF compatibility" -ForegroundColor Gray

Write-Host "✅ Database setup completed: $dbPath" -ForegroundColor Green

Write-Host "`nBuild completed successfully!" -ForegroundColor Green
Write-Host "Executable : $(Get-Location)\SwAIvyn.exe" -ForegroundColor White
Write-Host "DLL folder : $(Get-Location)\dll" -ForegroundColor White
Write-Host "Run        : double-click SwAIvyn.exe or launch via CLI." -ForegroundColor White