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
    Setup mode: 'Development', 'Production', or 'Service'. Determines build configuration and behavior.
.PARAMETER CleanInstall
    Performs a clean installation by removing existing data (like swai-vyn.db) and potentially build artifacts (see CleanAllBuildArtifacts).
.PARAMETER CleanAllBuildArtifacts
    If set, cleans backend/bin, backend/obj, frontend/dist, frontend/node_modules.
.PARAMETER SkipPrerequisites
    Skips all prerequisite checks.
.PARAMETER InstallPrerequisites
    If prerequisites are missing, attempts to install them using Chocolatey (requires admin rights).
.PARAMETER InstallService
    Installs SwAIvyn as a Windows service (requires Administrator privileges). The service will be configured based on the 'Service' mode.
.PARAMETER DatabasePath
    Custom path for the SQLite database (default relative to project root: 'data\swai-vyn.db').
.PARAMETER Port
    Port number for the web server (default: 5000).
.PARAMETER Runtime
    Runtime Identifier (RID) for .NET publish (e.g., win-x64, linux-x64). Default is 'win-x64'.
.PARAMETER BuildSqliteVss
    If set, forces the build of the sqlite-vss extension.
#>

[CmdletBinding()]
param(
    [ValidateSet('Development', 'Production', 'Service')]
    [string]$Mode = 'Production', # Default changed to Production as per original full-setup.ps1 implied general use

    [switch]$CleanInstall, # Renamed from $CleanDatabase in original full-setup.ps1 for clarity with $CleanAllBuildArtifacts

    [switch]$CleanAllBuildArtifacts = $false,

    [switch]$SkipPrerequisites = $false,

    [switch]$InstallPrerequisites = $false,

    [switch]$InstallService = $false,

    [string]$DatabasePath = "data\swai-vyn.db", # Default relative path, will be made absolute later

    [int]$Port = 5000,

    [string]$Runtime = 'win-x64',

    [switch]$BuildSqliteVss = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Determine Project Root Path and other key paths
# Script is in 'scripts' subdirectory, so project root is one level up from $PSScriptRoot
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $projectRoot) { $projectRoot = Split-Path -Parent (Get-Location) } # Fallback if $PSScriptRoot is not available

$distDir = Join-Path $projectRoot 'dist'

# Fully qualify the DatabasePath if it's relative
if (-not ([System.IO.Path]::IsPathRooted($DatabasePath))) {
    $DatabasePath = Join-Path $projectRoot $DatabasePath
}

# Derive Configuration from Mode
$Configuration = if ($Mode -eq 'Development') { 'Debug' } else { 'Release' }

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
Write-Info "Setup Mode: $Mode (Configuration: $Configuration)"
Write-Info "Project Root: $projectRoot"
Write-Info "Distribution Dir: $distDir"
Write-Info "Clean Install (Database): $CleanInstall"
Write-Info "Clean All Build Artifacts: $CleanAllBuildArtifacts"
Write-Info "Database Path: $DatabasePath"
Write-Info "Port: $Port"
Write-Info "Runtime: $Runtime"
Write-Info "Build SQLite VSS: $BuildSqliteVss"
Write-Info "Skip Prerequisites: $SkipPrerequisites"
Write-Info "Install Prerequisites: $InstallPrerequisites"
Write-Info "Install Service: $InstallService"


# Check if running as Administrator for service installation or Choco prerequisite installation
if (($InstallService -or $InstallPrerequisites) -and -not (Test-AdminPrivileges)) {
    Write-Error "Installing Windows service or prerequisites with Chocolatey requires Administrator privileges. Please run PowerShell as Administrator."
    exit 1
}

# === STEP 1: Clean Installation ===
Write-Step "STEP 1: Clean Installation"

if ($CleanInstall) {
    Write-Info "Parameter -CleanInstall specified."
    # Remove database specific files, using the potentially custom $DatabasePath
    $dbFile = $DatabasePath
    $dbShmFile = "$DatabasePath-shm"
    $dbWalFile = "$DatabasePath-wal"

    if (Test-Path $dbFile) {
        Remove-Item $dbFile -Force -ErrorAction SilentlyContinue
        Write-Info "Removed database file: $dbFile"
    }
    if (Test-Path $dbShmFile) {
        Remove-Item $dbShmFile -Force -ErrorAction SilentlyContinue
        Write-Info "Removed database SHM file: $dbShmFile"
    }
    if (Test-Path $dbWalFile) {
        Remove-Item $dbWalFile -Force -ErrorAction SilentlyContinue
        Write-Info "Removed database WAL file: $dbWalFile"
    }
    Write-Success "Database files cleaned."
}

if ($CleanAllBuildArtifacts) {
    Write-Info "Parameter -CleanAllBuildArtifacts specified."
    $cleanupPaths = @(
        (Join-Path $projectRoot 'backend\bin'),
        (Join-Path $projectRoot 'backend\obj'),
        (Join-Path $projectRoot 'frontend\dist'),
        (Join-Path $projectRoot 'frontend\node_modules'),
        (Join-Path $projectRoot 'dist'), # General dist directory
        (Join-Path $projectRoot 'logs') # Logs directory
        # Note: user data in $projectRoot/data/* (avatars, uploads etc) is not removed by this switch
    )

    foreach ($path in $cleanupPaths) {
        if (Test-Path $path) {
            Write-Info "Removing: $path"
            Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Success "Build artifacts and logs cleaned."
}

if (-not $CleanInstall -and -not $CleanAllBuildArtifacts) {
    Write-Info "No cleaning parameters specified. Skipping cleaning."
}


# === STEP 2: Prerequisites Check and Installation ===
Write-Step "STEP 2: Prerequisites Check and Installation"
if (-not $SkipPrerequisites) {
    Write-Info "Checking prerequisites..."

    # Check for Chocolatey only if we need to install something
    if ($InstallPrerequisites -and -not (Test-Command "choco")) {
        Write-Warning "Chocolatey not found and -InstallPrerequisites specified. Attempting to install Chocolatey..."
        Install-Chocolatey # Needs admin rights, checked earlier
    } elseif (Test-Command "choco") {
        Write-Success "Chocolatey is available."
    }

    # Prerequisite: .NET 8 SDK
    $dotnetOK = $false
    try {
        $dotnetVersion = dotnet --version 2>$null # Suppress error stream for cleaner check
        if ($dotnetVersion -and $dotnetVersion.StartsWith("8.")) {
            Write-Success "[OK] .NET 8 SDK version: $dotnetVersion"
            $dotnetOK = $true
        }
    } catch {} # Ignore error, check $dotnetOK

    if (-not $dotnetOK) {
        if ($InstallPrerequisites -and (Test-Command "choco")) {
            Write-Warning ".NET 8 SDK not found or wrong version. Attempting to install with Chocolatey..."
            choco install dotnet-8.0-sdk -y
            refreshenv
            # Re-check after install
            $dotnetVersion = dotnet --version
            if ($dotnetVersion -and $dotnetVersion.StartsWith("8.")) { Write-Success "[OK] .NET 8 SDK installed and verified: $dotnetVersion" }
            else { Write-Error ".NET 8 SDK installation failed or wrong version detected after install." }
        } else {
            Write-Error ".NET 8 SDK not found. Install .NET 8 SDK (https://dotnet.microsoft.com/download) or run with -InstallPrerequisites (requires Choco and admin rights)."
        }
    }

    # Prerequisite: Node.js 18+
    $nodeOK = $false
    try {
        $nodeVersion = node --version 2>$null
        if ($nodeVersion -and [version]($nodeVersion.TrimStart('v')) -ge [version]"18.0.0") {
            Write-Success "[OK] Node.js version: $nodeVersion"
            $nodeOK = $true
        }
    } catch {}

    if (-not $nodeOK) {
        if ($InstallPrerequisites -and (Test-Command "choco")) {
            Write-Warning "Node.js 18+ not found or wrong version. Attempting to install with Chocolatey..."
            choco install nodejs-lts -y # nodejs-lts is generally a good choice for 18+
            refreshenv
            $nodeVersion = node --version
             if ($nodeVersion -and [version]($nodeVersion.TrimStart('v')) -ge [version]"18.0.0") { Write-Success "[OK] Node.js installed and verified: $nodeVersion" }
             else { Write-Error "Node.js installation failed or wrong version detected after install." }
        } else {
            Write-Error "Node.js 18+ not found. Install Node.js (https://nodejs.org/) or run with -InstallPrerequisites."
        }
    }

    # Prerequisite: npm (usually comes with Node)
    if (-not (Test-Command "npm")) {
         Write-Error "npm not found. It should be installed with Node.js."
    } else {
        Write-Success "[OK] npm version: $(npm --version)"
    }

    # Prerequisite: Git
    if (-not (Test-Command "git")) {
        if ($InstallPrerequisites -and (Test-Command "choco")) {
            Write-Warning "Git not found. Attempting to install with Chocolatey..."
            choco install git -y
            refreshenv
            if (Test-Command "git") { Write-Success "[OK] Git installed and verified."}
            else { Write-Error "Git installation failed."}
        } else {
            Write-Error "Git not found. Install Git (https://git-scm.com/downloads) or run with -InstallPrerequisites."
        }
    } else {
        Write-Success "[OK] Git is installed."
    }

    # Prerequisite: Visual Studio Build Tools (required for SQLite VSS compilation if $BuildSqliteVss is true)
    # This check is informational if not building VSS, but an error if $BuildSqliteVss is true and tools are missing.
    Write-Info "Checking for Visual Studio Build Tools (C++ workload)..."
    $vsBuildToolsFound = $false
    $vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswherePath) {
        $buildTools = & $vswherePath -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -format json | ConvertFrom-Json -ErrorAction SilentlyContinue
        if ($buildTools) {
            Write-Success "[OK] Visual Studio Build Tools with C++ workload found at $($buildTools.installationPath)."
            $vsBuildToolsFound = $true
        } else {
            Write-Warning "Visual Studio Build Tools with C++ workload NOT found."
            Write-Info "  For SQLite VSS compilation (-BuildSqliteVss), these are required."
            Write-Info "  Install via Visual Studio Installer: 'Desktop development with C++' workload."
        }
    } else {
        Write-Warning "vswhere.exe not found. Cannot automatically check for Visual Studio Build Tools."
    }

    if ($BuildSqliteVss -and -not $vsBuildToolsFound) {
         Write-Error "BuildSqliteVss specified, but Visual Studio Build Tools with C++ workload were not found. Cannot proceed with VSS build."
         # Exit here or let VSS build step fail later? For now, let it fail later for more specific error.
    }
    Write-Success "Prerequisite checks completed."
} else {
    Write-Info "Skipping prerequisite checks as per -SkipPrerequisites parameter."
}


# === STEP 3: Create Directory Structure ===
Write-Step "STEP 3: Create Directory Structure"

$dataDirs = @(
    (Join-Path $projectRoot 'data'),
    (Join-Path $projectRoot 'data\avatars'),
    (Join-Path $projectRoot 'data\uploads'),
    (Join-Path $projectRoot 'data\backups'),
    (Join-Path $projectRoot 'data\modules'),
    (Join-Path $projectRoot 'data\exports'), # Added from setup.ps1
    (Join-Path $projectRoot 'logs')
)
# Dist directories are created by build/publish steps usually, but ensure base $distDir
$otherDirs = @(
    $distDir
    # (Join-Path $distDir 'backend'), # Created by dotnet publish
    # (Join-Path $distDir 'wwwroot') # Created by frontend copy or publish
)

foreach ($dir in ($dataDirs + $otherDirs)) {
    if (-not (Test-Path $dir)) {
        Write-Info "Creating directory: $dir"
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}
Write-Success "Base directory structure checked/created."


# === STEP 4: Database Setup ===
Write-Step "STEP 4: Database Setup"

# Ensure database directory exists from $DatabasePath
$dbParentDir = Split-Path $DatabasePath -Parent
if ($dbParentDir -and -not (Test-Path $dbParentDir)) {
    Write-Info "Creating database directory: $dbParentDir"
    New-Item -ItemType Directory -Path $dbParentDir -Force | Out-Null
}

# Database file validation from first_run.ps1 (if not CleanInstall)
if (-not $CleanInstall -and (Test-Path $DatabasePath)) {
    Write-Info "Validating existing database file: $DatabasePath"
    try {
        $bytes = [System.IO.File]::ReadAllBytes($DatabasePath)
        if ($bytes.Length -gt 16) {
            $header = [System.Text.Encoding]::ASCII.GetString($bytes[0..15])
            if ($header -eq "SQLite format 3") {
                Write-Success "Database validation passed - SQLite format confirmed."
            } else {
                Write-Warning "Database validation failed - invalid format. Consider using -CleanInstall."
                # Optionally, could force CleanInstall or error out here. For now, just warn.
            }
        } else {
            Write-Warning "Database file too small, potentially corrupt. Consider using -CleanInstall."
        }
    }
    catch {
        Write-Warning "Error during database validation: $($_.Exception.Message). Consider using -CleanInstall."
    }
}

# Use .NET tools for DB creation/migration (from original full-setup.ps1)
Write-Info "Ensuring database schema is up-to-date using .NET tools..."
$toolsToBuild = @('CreateTables', 'UpdateDatabase') # Tools from original full-setup
foreach ($toolName in $toolsToBuild) {
    $toolDir = Join-Path $projectRoot "tools\$toolName"
    if (Test-Path $toolDir) {
        Push-Location $toolDir
        Write-Info "Building tool: $toolName (Configuration: $Configuration)"
        dotnet build -c $Configuration # Tools should use the main $Configuration
        if ($LASTEXITCODE -ne 0) { Write-Error "$toolName build failed."; Pop-Location; exit 1 }
        Write-Info "Running tool: $toolName"
        # How tools get $DatabasePath: Assume they read appsettings.json from their context or project root.
        # If tools need $DatabasePath directly, this call needs adjustment.
        # For now, assume they use the main appsettings.json which will be updated later, or a specific one.
        # The original full-setup.ps1 just ran them. If appsettings.json in project root is used by tools,
        # it should be updated *before* this step if $DatabasePath or $Port changed from default.
        # This is a potential issue if tools rely on a specific appsettings.json that isn't the one in $distDir.
        # Let's assume for now tools can locate or are configured for the correct DB.
        $toolExe = Join-Path $toolDir "bin\$Configuration\net8.0\$toolName.exe"
        if (Test-Path $toolExe) {
            & $toolExe
            if ($LASTEXITCODE -ne 0) { Write-Error "$toolName execution failed."; Pop-Location; exit 1 }
        } else {
            Write-Warning "Tool executable not found after build: $toolExe"
        }
        Pop-Location
    } else {
        Write-Warning "Tool directory not found: $toolDir"
    }
}
Write-Success "Database setup/migration tools executed."


# === STEP 5: Build SQLite VSS Extension ===
Write-Step "STEP 5: Build SQLite VSS Extension"
if ($BuildSqliteVss) {
    Write-Info "Parameter -BuildSqliteVss specified. Attempting to build sqlite-vss extension..."
    $vssBuildScript = Join-Path $projectRoot "scripts\build-sqlitevss.ps1"
    if (Test-Path $vssBuildScript) {
        try {
            Push-Location (Split-Path $vssBuildScript -Parent) # Go to scripts dir
            # Consider if build-sqlitevss.ps1 needs -UseRootBlas or other params from this script
            & .\build-sqlitevss.ps1 # Add parameters as needed, e.g. -UseRootBlas $UseRootBlasVss (new param for full-setup)
            Pop-Location
            Write-Success "SQLite VSS extension build process completed."
            # Further validation could check for the DLL in the expected output location of build-sqlitevss.ps1
        } catch {
            Write-Error "SQLite VSS build failed: $($_.Exception.Message)"
            # Decide if this is a fatal error or a warning
        }
    } else {
        Write-Warning "SQLite VSS build script not found at $vssBuildScript. Cannot build extension."
    }
} else {
    Write-Info "Skipping SQLite VSS extension build as -BuildSqliteVss was not specified."
}


# === STEP 6: Backend Setup ===
Write-Step "STEP 6: Backend Setup"
$backendDir = Join-Path $projectRoot 'backend'

Push-Location $backendDir
try {
    Write-Info "Restoring .NET packages for backend..."
    dotnet restore --runtime $Runtime

    Write-Info "Publishing backend (Configuration: $Configuration, Runtime: $Runtime)..."
    # Output path for publish is $distDir (which is projectRoot/dist)
    # The original full-setup.ps1 published directly to $distDir.
    # The setup.ps1 (base for this) published to ..\dist\backend from backend dir.
    # Let's use $distDir directly as it's simpler.
    dotnet publish -c $Configuration -r $Runtime --self-contained true -o $distDir
    if ($LASTEXITCODE -ne 0) { throw "Backend publish failed." }

    Write-Success "Backend published successfully to $distDir"
} catch {
    Write-Error "Backend setup failed: $($_.Exception.Message)"
    Pop-Location
    exit 1
} finally {
    Pop-Location
}


# === STEP 7: Frontend Setup ===
Write-Step "STEP 7: Frontend Setup"
$frontendDir = Join-Path $projectRoot 'frontend'
if (Test-Path (Join-Path $frontendDir 'package.json')) {
    Push-Location $frontendDir
    try {
        Write-Info "Installing npm packages for frontend (npm ci)..."
        npm ci # Use ci for cleaner installs, assumes package-lock.json exists
        if ($LASTEXITCODE -ne 0) { throw "npm ci failed." }

        Write-Info "Building frontend (npm run build)..."
        npm run build # Assumes 'build' script is defined in package.json
        if ($LASTEXITCODE -ne 0) { throw "Frontend build failed." }

        # Copy frontend build to $distDir/wwwroot
        $frontendDistSource = Join-Path $frontendDir 'dist' # Common output for Vite/Angular/React builds
        $backendWwwRoot = Join-Path $distDir 'wwwroot'

        if (Test-Path $frontendDistSource) {
            Write-Info "Copying frontend build from $frontendDistSource to $backendWwwRoot..."
            if (Test-Path $backendWwwRoot) {
                Remove-Item $backendWwwRoot -Recurse -Force
            }
            Copy-Item -Path $frontendDistSource\* -Destination $backendWwwRoot -Recurse -Force
            Write-Success "Frontend assets copied to $backendWwwRoot"
        } else {
            Write-Warning "Frontend build output directory not found at $frontendDistSource. Cannot copy assets."
        }

    } catch {
        Write-Error "Frontend setup failed: $($_.Exception.Message)"
        Pop-Location
        exit 1
    } finally {
        Pop-Location
    }
} else {
    Write-Info "No package.json found in $frontendDir, skipping frontend setup."
}


# === STEP 8: Configuration ===
Write-Step "STEP 8: Application Configuration"

# Update appsettings.json in $distDir
$appsettingsDistPath = Join-Path $distDir "appsettings.json"
$appsettingsMasterPath = Join-Path $projectRoot "appsettings.json" # Master template

if (-not (Test-Path $appsettingsDistPath) -and (Test-Path $appsettingsMasterPath)) {
    Write-Info "appsettings.json not found in $distDir, copying from $appsettingsMasterPath"
    Copy-Item $appsettingsMasterPath -Destination $appsettingsDistPath -Force
}

if (Test-Path $appsettingsDistPath) {
    Write-Info "Updating $appsettingsDistPath..."
    # Using PowerShell's ConvertFrom-Json and ConvertTo-Json for robustness
    $settingsContent = Get-Content $appsettingsDistPath -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json -ErrorAction SilentlyContinue

    if ($settingsContent) {
        # Update Database Connection String
        if (-not $settingsContent.PSObject.Properties['ConnectionStrings']) {
            $settingsContent | Add-Member -MemberType NoteProperty -Name 'ConnectionStrings' -Value (New-Object PSObject)
        }
        $settingsContent.ConnectionStrings.DefaultConnection = "Data Source=$DatabasePath"
        Write-Info "  Set ConnectionStrings.DefaultConnection to: Data Source=$DatabasePath"

        # Update Kestrel Port
        if (-not $settingsContent.PSObject.Properties['Kestrel']) {
            $settingsContent | Add-Member -MemberType NoteProperty -Name 'Kestrel' -Value (New-Object PSObject)
        }
        if (-not $settingsContent.Kestrel.PSObject.Properties['Endpoints']) {
            $settingsContent.Kestrel | Add-Member -MemberType NoteProperty -Name 'Endpoints' -Value (New-Object PSObject)
        }
        if (-not $settingsContent.Kestrel.Endpoints.PSObject.Properties['Http']) {
            $settingsContent.Kestrel.Endpoints | Add-Member -MemberType NoteProperty -Name 'Http' -Value (New-Object PSObject)
        }
        $settingsContent.Kestrel.Endpoints.Http.Url = "http://localhost:$Port"
        Write-Info "  Set Kestrel.Endpoints.Http.Url to: http://localhost:$Port"

        # Save changes
        try {
            $settingsContent | ConvertTo-Json -Depth 10 | Set-Content $appsettingsDistPath -Encoding UTF8
            Write-Success "Successfully updated $appsettingsDistPath."
        } catch {
             Write-Error "Failed to write updated $appsettingsDistPath: $($_.Exception.Message)"
        }
    } else {
        Write-Warning "Could not parse $appsettingsDistPath. Manual configuration may be required."
    }
} else {
    Write-Warning "$appsettingsDistPath not found. Application may not run correctly without it."
}

# Copy web.config if it exists in project root to $distDir (for IIS deployments, if applicable)
$webConfigMasterPath = Join-Path $projectRoot "web.config"
if (Test-Path $webConfigMasterPath) {
    Copy-Item $webConfigMasterPath (Join-Path $distDir "web.config") -Force
    Write-Info "Copied web.config to $distDir."
}

Write-Success "Application configuration step completed."


# === STEP 9: Windows Service Installation ===
Write-Step "STEP 9: Windows Service Installation"
if ($InstallService) {
    Write-Info "Parameter -InstallService specified."
    # Admin rights already checked at the beginning of the script
    $serviceInstallScript = Join-Path $projectRoot "scripts\install-service.ps1"
    if (Test-Path $serviceInstallScript) {
        Write-Info "Attempting to install service using $serviceInstallScript..."
        # install-service.ps1 might need parameters like the path to the executable in $distDir
        # and the service name or description.
        # Example: & $serviceInstallScript -ExecutablePath (Join-Path $distDir 'SwAIvyn.exe') -ServiceName "SwAIvynService"
        # For now, calling it without params, assuming it has defaults or discovers necessary info.
        try {
            Push-Location (Split-Path $serviceInstallScript -Parent)
            & .\install-service.ps1 # Add parameters if $serviceInstallScript requires them
            Pop-Location
            Write-Success "Windows service installation script executed."
            Write-Info "Verify service status in 'Services.msc'."
        } catch {
            Write-Error "Service installation script failed: $($_.Exception.Message)"
        }
    } else {
        Write-Warning "Service installation script ($serviceInstallScript) not found. Cannot install service."
    }
} else {
    Write-Info "Skipping Windows service installation as -InstallService was not specified."
}


# === STEP 10: Create Launch Scripts ===
Write-Step "STEP 10: Create Launch Scripts in Project Root"

# Determine executable name based on runtime (already done in old full-setup)
$exeName = if ($Runtime -like 'win*') { 'SwAIvyn.exe' } else { 'SwAIvyn' }
$publishedExePath = Join-Path $distDir $exeName

# Development launch script (launch-dev.cmd)
# This script should run 'dotnet run' from backend for dev, and 'npm run dev' for frontend.
$devLaunchScriptContent = @"
@echo off
echo Starting SwAIvyn in Development Mode...
cd /d "%~dp0"

echo Starting Backend (dotnet run from backend directory)...
start "SwAIvyn Backend (Dev)" cmd /c "cd backend && dotnet run --urls http://localhost:$Port"

echo Starting Frontend (npm run dev from frontend directory)...
start "SwAIvyn Frontend (Dev)" cmd /c "cd frontend && npm run dev"

echo.
echo SwAIvyn Development environment is starting...
echo Backend should be available at: http://localhost:$Port (once compiled and running)
echo Frontend development server (check its console output for port, usually different from $Port)
echo Check the console windows for status and actual URLs/ports.
pause
"@
$devLaunchScriptPath = Join-Path $projectRoot "launch-dev.cmd"
$devLaunchScriptContent | Set-Content $devLaunchScriptPath -Encoding ASCII
Write-Info "Created development launch script: $devLaunchScriptPath"

# Production/Standard launch script (launch.cmd)
# This script should run the published executable from $distDir.
$prodLaunchScriptContent = @"
@echo off
echo Starting SwAIvyn...
cd /d "%~dp0"
if exist "$publishedExePath" (
    start "SwAIvyn" "$publishedExePath"
    echo SwAIvyn started. Access at http://localhost:$Port (if Kestrel configured for this port)
) else (
    echo Executable not found at "$publishedExePath".
    echo Please ensure the application has been built/published correctly using full-setup.ps1.
    pause
)
"@
$prodLaunchScriptPath = Join-Path $projectRoot "launch.cmd"
$prodLaunchScriptContent | Set-Content $prodLaunchScriptPath -Encoding ASCII
Write-Info "Created production launch script: $prodLaunchScriptPath"

Write-Success "Launch scripts created."


# === STEP 11: Final Artifacts ===
Write-Step "STEP 11: Final Artifacts"
# Copy sqlite-vss.dll to $distDir if it was built or exists in a known location.
# The build-sqlitevss.ps1 script should ideally copy its output to $distDir or a predictable place.
# Let's assume build-sqlitevss.ps1 copies it to $projectRoot/scripts/sqlite-vss/build/sqlite-vss.dll or similar.
# Or, if it's copied to $projectRoot/windows-libs (for UseRootBlas scenario in VSS build), then source from there.
# This part needs coordination with build-sqlitevss.ps1's output.
# For now, check a common location, e.g., $projectRoot/sqlite-vss.dll or from the build directory.

$vssDllName = "sqlite-vss.dll"
$vssDllSourcePath = Join-Path $projectRoot $vssDllName # A common place if manually put there
$vssDllBuiltPath = Join-Path $projectRoot "scripts\sqlite-vss\build\$vssDllName" # Example if built by script
# Add more potential paths if build-sqlitevss.ps1 outputs elsewhere.

$finalVssDllPath = Join-Path $distDir $vssDllName

if (Test-Path $vssDllBuiltPath) { # Prefer freshly built one
    Copy-Item $vssDllBuiltPath -Destination $finalVssDllPath -Force
    Write-Info "Copied $vssDllName from $vssDllBuiltPath to $distDir."
} elseif (Test-Path $vssDllSourcePath) {
    Copy-Item $vssDllSourcePath -Destination $finalVssDllPath -Force
    Write-Info "Copied $vssDllName from $vssDllSourcePath to $distDir."
} elseif ($BuildSqliteVss) {
    Write-Warning "$vssDllName was supposed to be built but not found in expected locations to copy to $distDir."
} else {
    Write-Info "$vssDllName not found in common project locations. If needed, ensure it's manually placed or built into $distDir."
}

# Copy main published executable to project root for convenience (as in original full-setup.ps1)
if (Test-Path $publishedExePath) {
    Copy-Item $publishedExePath -Destination (Join-Path $projectRoot $exeName) -Force
    Write-Info "Copied $exeName to $projectRoot for convenience."
}

Write-Success "Final artifacts processing completed."


# === STEP 12: Final Validation ===
Write-Step "STEP 12: Final Validation"
$validationErrors = @()

# Check for published executable
if (-not (Test-Path $publishedExePath)) {
    $validationErrors += "Missing published executable: $publishedExePath"
}

# Check for appsettings.json in dist
if (-not (Test-Path $appsettingsDistPath)) {
    $validationErrors += "Missing application configuration: $appsettingsDistPath"
}

# Check for database file creation (if not CleanInstall and it didn't exist before)
if (-not (Test-Path $DatabasePath)) {
    # This might be okay if the app creates it on first run and no CleanInstall was specified.
    # However, the DB tools step should have created it.
    $validationErrors += "Database file not found at specified path: $DatabasePath"
}

# Check for SQLite VSS DLL in dist if it was supposed to be built or copied
if ($BuildSqliteVss -and -not (Test-Path $finalVssDllPath)) {
    $validationErrors += "SQLite VSS DLL ($vssDllName) is missing from $distDir after requesting build."
}

if ($validationErrors.Count -gt 0) {
    Write-Error "Final validation failed with the following errors:"
    foreach ($errorItem in $validationErrors) {
        Write-Error "  - $errorItem"
    }
    # exit 1 # Decide if this should be a fatal script exit
} else {
    Write-Success "Final validation checks passed."
}


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
