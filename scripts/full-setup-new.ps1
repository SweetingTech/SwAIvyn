# SwAIvyn Complete Setup and Build Script
<#
.SYNOPSIS
 Complete setup and build automation for SwAIvyn.
.DESCRIPTION
 Checks prerequisites (.NET 8 SDK, Node 18+, npm), builds backend and frontend, prepares SQLite+VSS DB, and copies artifacts into ./dist.
.PARAMETER Configuration
 Build configuration (Debug|Release). Defaults to Release.
.PARAMETER Runtime
 RID used for self‑contained publish.
.PARAMETER CleanDatabase
 Drop existing ./data/swai-vyn.db before running migration tools.
.PARAMETER SkipBuild
 Skip backend publish step.
.PARAMETER SkipFrontend
 Skip frontend npm install/build step.
#>

[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [switch]$CleanDatabase,

    [switch]$SkipBuild,

    [switch]$SkipFrontend
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$rootDir = Split-Path -Parent $PSCommandPath
$distDir = Join-Path $rootDir 'dist'

# helper function
function Test-Command {
    param(
        [Parameter(Mandatory)][string]$Command
    )
    try { Get-Command $Command -ErrorAction Stop | Out-Null; $true }
    catch { $false }
}

# SECTION 1 – prerequisites
Write-Host "`n=== 1. Prerequisites ===" -ForegroundColor Yellow

if (-not (Test-Command 'dotnet')) {
    throw '.NET SDK not found. Install .NET 8 SDK https://dotnet.microsoft.com/download'
}
$dotnetVersion = dotnet --version
Write-Host "✓ .NET SDK $dotnetVersion" -ForegroundColor Green

if (-not (Test-Command 'node')) {
    throw 'Node.js not found. Install Node.js 18+ https://nodejs.org/'
}
$nodeVersion = node --version
Write-Host "✓ Node $nodeVersion" -ForegroundColor Green

if (-not (Test-Command 'npm')) {
    throw 'npm not found. Node installer normally includes it.'
}
$npmVersion = npm --version
Write-Host "✓ npm $npmVersion" -ForegroundColor Green

# SECTION 2 – folders
Write-Host "`n=== 2. Directory Structure ===" -ForegroundColor Yellow
$dataDirectories = @(
    'data',
    'data\avatars',
    'data\uploads',
    'data\backups',
    'data\modules',
    'data\sessions',
    'logs',
    'dist'
)
foreach ($dir in $dataDirectories) {
    $path = Join-Path $rootDir $dir
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Path $path | Out-Null
        Write-Host "Created $dir" -ForegroundColor Gray
    }
}

# optional DB wipe
if ($CleanDatabase) {
    $dbFile = Join-Path $rootDir 'data\swai-vyn.db'
    if (Test-Path $dbFile) {
        Remove-Item $dbFile -Force
        Write-Host 'Existing SQLite database removed' -ForegroundColor Magenta
    }
}

# SECTION 3 – database tools
Write-Host "`n=== 3. Database Migration Tools ===" -ForegroundColor Yellow
$tools = @('CreateTables','UpdateDatabase')
foreach ($tool in $tools) {
    $toolDir = Join-Path $rootDir "tools\$tool"
    Push-Location $toolDir
    dotnet build -c $Configuration
    if ($LASTEXITCODE) { throw "$tool build failed." }
    Pop-Location
}

& (Join-Path $rootDir "tools\CreateTables\bin\$Configuration\net8.0\CreateTables.exe")
& (Join-Path $rootDir "tools\UpdateDatabase\bin\$Configuration\net8.0\UpdateDatabase.exe")
Write-Host "✓ Database prepared" -ForegroundColor Green

# SECTION 4 – backend
Write-Host "`n=== 4. Backend ===" -ForegroundColor Yellow
$backendDir = Join-Path $rootDir 'backend'
Push-Location $backendDir
dotnet restore
if ($LASTEXITCODE) { throw 'dotnet restore failed.' }
Pop-Location
Write-Host "✓ Packages restored" -ForegroundColor Green

if (-not $SkipBuild) {
    Push-Location $backendDir
    dotnet publish -c $Configuration -r $Runtime --self-contained -o $distDir
    if ($LASTEXITCODE) { throw 'dotnet publish failed.' }
    Pop-Location
    Write-Host "✓ Backend published to $distDir" -ForegroundColor Green
}

# SECTION 5 – frontend
if (-not $SkipFrontend) {
    Write-Host "`n=== 5. Frontend ===" -ForegroundColor Yellow
    $frontendDir = Join-Path $rootDir 'frontend'
    Push-Location $frontendDir
    npm ci
    if ($LASTEXITCODE) { throw 'npm ci failed.' }
    npm run build
    if ($LASTEXITCODE) { throw 'Frontend build failed.' }
    Pop-Location

    $frontendDist = Join-Path $frontendDir 'dist'
    $backendWwwRoot = Join-Path $distDir 'wwwroot'
    if (Test-Path $backendWwwRoot) { Remove-Item $backendWwwRoot -Recurse -Force }
    Copy-Item $frontendDist -Destination $backendWwwRoot -Recurse -Force
    Write-Host "✓ Frontend assets copied" -ForegroundColor Green
}

# SECTION 6 – final artifacts
Write-Host "`n=== 6. Finalising ===" -ForegroundColor Yellow
$filesToCopy = @(
    'sqlite-vss.dll',
    'appsettings.json',
    'web.config'
)
foreach ($f in $filesToCopy) {
    $src = Join-Path $rootDir $f
    if (Test-Path $src) { Copy-Item $src -Destination $distDir -Force }
}

$exeName = if ($Runtime -like 'win*') { 'SwAIvyn.exe' } else { 'SwAIvyn' }
$publishedExe = Join-Path $distDir $exeName
if (Test-Path $publishedExe) {
    Copy-Item $publishedExe -Destination (Join-Path $rootDir $exeName) -Force
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "   SwAIvyn build complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "`nOpen http://localhost:5000 once SwAIvyn is running."
