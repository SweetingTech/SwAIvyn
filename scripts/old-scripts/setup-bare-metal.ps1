#!/usr/bin/env powershell
#Requires -Version 7.0
#Requires -RunAsAdministrator

<#
.SYNOPSIS
    SwAIvyn Bare Metal Setup Script
    
.DESCRIPTION
    Installs and configures all SwAIvyn dependencies and services to run natively on Windows
    without Docker. This includes databases, AI services, and the application stack.
    
.PARAMETER InstallDependencies
    Install all required software dependencies (PostgreSQL, Node.js, Python, etc.)
    
.PARAMETER SkipInstall
    Skip dependency installation and only configure/start services
    
.PARAMETER ConfigOnly
    Only generate configuration files, don't start services
    
.PARAMETER DataDir
    Base directory for data storage (default: C:\SwAIvyn\data)
    
.PARAMETER Port
    Base port for services (default: 5000)

.EXAMPLE
    .\setup-bare-metal.ps1 -InstallDependencies
    
.EXAMPLE  
    .\setup-bare-metal.ps1 -SkipInstall -Port 3000
#>

param(
    [switch]$InstallDependencies = $false,
    [switch]$SkipInstall = $false,
    [switch]$ConfigOnly = $false,
    [string]$DataDir = "C:\SwAIvyn\data",
    [int]$Port = 5000
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot

# Import environment variables
function Import-DotEnv {
    param([string]$Path = (Join-Path $rootDir '.env'))
    if (-not (Test-Path $Path)) { return }
    Get-Content -Raw $Path | ForEach-Object {
        $_ -split "`n" | ForEach-Object {
            if ($_ -match '^(\s*#|\s*)$') { return }
            if ($_ -match '^(?<k>[^=\s]+)\s*=\s*(?<v>.*)$') {
                $k = $matches['k']
                $v = $matches['v']
                if ($v -match '^"(.*)"$') { $v = $matches[1] }
                elseif ($v -match "^'(.*)'$") { $v = $matches[1] }
                Set-Item -Path ("Env:" + $k) -Value $v -ErrorAction SilentlyContinue
            }
        }
    }
}

# Utility functions
function Test-Command { param([string]$Name) try { Get-Command $Name -ErrorAction Stop | Out-Null; return $true } catch { return $false } }
function Test-Port { param([int]$Port) try { $listener = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners(); return ($listener | Where-Object { $_.Port -eq $Port }).Count -gt 0 } catch { return $false } }
function Wait-ForPort { param([int]$Port, [int]$TimeoutSec = 60) for ($i = 0; $i -lt $TimeoutSec; $i++) { if (Test-Port $Port) { return $true } Start-Sleep 1 } return $false }

Write-Host "🚀 SwAIvyn Bare Metal Setup" -ForegroundColor Cyan
Write-Host "Data Directory: $DataDir" -ForegroundColor Gray
Write-Host "Base Port: $Port" -ForegroundColor Gray

# Create directory structure
$dirs = @(
    $DataDir,
    "$DataDir\postgresql",
    "$DataDir\neo4j", 
    "$DataDir\qdrant",
    "$DataDir\temporal",
    "$DataDir\logs",
    "$DataDir\config",
    "$DataDir\voices",
    "$DataDir\models"
)

foreach ($dir in $dirs) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "✅ Created directory: $dir" -ForegroundColor Green
    }
}

# Port assignments
$ports = @{
    'frontend' = $Port           # 5000
    'backend' = $Port + 1000     # 6000  
    'postgresql' = 5432
    'neo4j_http' = 7474
    'neo4j_bolt' = 7687
    'temporal' = 7233
    'qdrant' = 6333
    'tts' = 8081
    'stt' = 9000
    'elevenlabs' = 8082
    'ollama' = 11434
    'lmstudio' = 1234
}

Write-Host "`n📦 Service Port Assignments:" -ForegroundColor Yellow
foreach ($service in $ports.Keys) {
    Write-Host "  $service : $($ports[$service])" -ForegroundColor Gray
}

if ($InstallDependencies -and -not $SkipInstall) {
    Write-Host "`n🔧 Installing Dependencies..." -ForegroundColor Cyan
    
    # Check if running as Administrator
    $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        throw "This script must be run as Administrator to install dependencies. Run PowerShell as Administrator and try again."
    }
    
    # Install Chocolatey if not present
    if (-not (Test-Command 'choco')) {
        Write-Host "Installing Chocolatey..." -ForegroundColor Yellow
        Set-ExecutionPolicy Bypass -Scope Process -Force
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
        Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
        $env:PATH = [Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [Environment]::GetEnvironmentVariable("PATH", "User")
    }
    
    # Install core dependencies
    $packages = @(
        'nodejs',           # Node.js for frontend
        'python',           # Python for backend
        'postgresql16',     # PostgreSQL database  
        'neo4j-community',  # Neo4j graph database
        'git'               # Git for version control
    )
    
    foreach ($package in $packages) {
        Write-Host "Installing $package..." -ForegroundColor Yellow
        try {
            & choco install $package -y --no-progress
            Write-Host "✅ Installed $package" -ForegroundColor Green
        } catch {
            Write-Warning "Failed to install $package : $_"
        }
    }
    
    # Install Python packages
    Write-Host "Installing Python packages..." -ForegroundColor Yellow
    & python -m pip install --upgrade pip
    & pip install uvicorn fastapi asyncpg psycopg2 temporalio qdrant-client neo4j
    
    # Install Node.js packages
    Write-Host "Installing Node.js packages..." -ForegroundColor Yellow
    Push-Location (Join-Path $rootDir 'frontend')
    & npm install
    Pop-Location
    
    Push-Location (Join-Path $rootDir 'Services/bff')
    & pip install -r requirements.txt
    Pop-Location
    
    Write-Host "✅ All dependencies installed" -ForegroundColor Green
}

# Import environment variables
Import-DotEnv

# Generate configuration files
Write-Host "`n⚙️ Generating Configuration Files..." -ForegroundColor Cyan

# PostgreSQL configuration
$pgConfig = @"
# PostgreSQL Configuration for SwAIvyn
listen_addresses = 'localhost'
port = $($ports.postgresql)
max_connections = 100
shared_buffers = 128MB
dynamic_shared_memory_type = windows
log_timezone = 'UTC'
timezone = 'UTC'
lc_messages = 'English_United States.1252'
lc_monetary = 'English_United States.1252'
lc_numeric = 'English_United States.1252'
lc_time = 'English_United States.1252'
default_text_search_config = 'pg_catalog.english'
"@

Set-Content -Path "$DataDir\config\postgresql.conf" -Value $pgConfig -Encoding UTF8
Write-Host "✅ PostgreSQL configuration generated" -ForegroundColor Green

# Neo4j configuration  
$neo4jConfig = @"
# Neo4j Configuration for SwAIvyn
server.default_listen_address=0.0.0.0
server.bolt.listen_address=:$($ports.neo4j_bolt)
server.http.listen_address=:$($ports.neo4j_http)
server.directories.data=$($DataDir -replace '\\', '/')/neo4j
server.directories.logs=$($DataDir -replace '\\', '/')/logs/neo4j
initial.dbms.default_database=swai
auth.minimum_password_length=4
"@

Set-Content -Path "$DataDir\config\neo4j.conf" -Value $neo4jConfig -Encoding UTF8
Write-Host "✅ Neo4j configuration generated" -ForegroundColor Green

# Qdrant configuration
$qdrantConfig = @"
service:
  host: 127.0.0.1
  http_port: $($ports.qdrant)
  grpc_port: $($ports.qdrant + 1)

storage:
  storage_path: $($DataDir -replace '\\', '/')/qdrant

log_level: INFO

telemetry_disabled: true
"@

Set-Content -Path "$DataDir\config\qdrant.yaml" -Value $qdrantConfig -Encoding UTF8
Write-Host "✅ Qdrant configuration generated" -ForegroundColor Green

# Temporal configuration
$temporalConfig = @"
global:
  membership:
    maxJoinDuration: 30s
    broadcastAddress: 127.0.0.1

persistence:
  defaultStore: default
  visibilityStore: visibility
  numHistoryShards: 4
  datastores:
    default:
      sql:
        pluginName: "postgres"
        databaseName: "temporal"
        connectAddr: "127.0.0.1:$($ports.postgresql)"
        connectProtocol: "tcp"
        user: "postgres"
        password: "$env:POSTGRES_PASSWORD"
        maxConns: 20
        maxIdleConns: 20
        maxConnLifetime: "1h"
    visibility:
      sql:
        pluginName: "postgres"  
        databaseName: "temporal_visibility"
        connectAddr: "127.0.0.1:$($ports.postgresql)"
        connectProtocol: "tcp"
        user: "postgres"
        password: "$env:POSTGRES_PASSWORD"
        maxConns: 10
        maxIdleConns: 10
        maxConnLifetime: "1h"

services:
  frontend:
    rpc:
      grpcPort: $($ports.temporal)
      membershipPort: $($ports.temporal + 100)
      bindOnLocalHost: true

  matching:
    rpc:
      grpcPort: $($ports.temporal + 1)
      membershipPort: $($ports.temporal + 101)
      bindOnLocalHost: true

  history:
    rpc:
      grpcPort: $($ports.temporal + 2)
      membershipPort: $($ports.temporal + 102)
      bindOnLocalHost: true

  worker:
    rpc:
      grpcPort: $($ports.temporal + 3)
      membershipPort: $($ports.temporal + 103)
      bindOnLocalHost: true

clusterMetadata:
  enableGlobalDomain: false
  failoverVersionIncrement: 10
  masterClusterName: "active"
  currentClusterName: "active"
  clusterInformation:
    active:
      enabled: true
      initialFailoverVersion: 1
      rpcName: "frontend"
      rpcAddress: "127.0.0.1:$($ports.temporal)"

dcRedirectionPolicy:
  policy: "noop"

archival:
  history:
    state: "enabled"
    enableRead: true
    provider:
      filestore:
        fileMode: "0666"
        dirMode: "0766"
        outputURI: "file://$($DataDir -replace '\\', '/')/temporal/archive"
  visibility:
    state: "enabled"
    enableRead: true
    provider:
      filestore:
        fileMode: "0666"
        dirMode: "0766"
        outputURI: "file://$($DataDir -replace '\\', '/')/temporal/archive"

publicClient:
  hostPort: "127.0.0.1:$($ports.temporal)"
"@

Set-Content -Path "$DataDir\config\temporal.yaml" -Value $temporalConfig -Encoding UTF8
Write-Host "✅ Temporal configuration generated" -ForegroundColor Green

# SwAIvyn environment configuration
$envConfig = @"
# SwAIvyn Environment Configuration (Bare Metal)
DATABASE_URL=postgresql+asyncpg://postgres:$env:POSTGRES_PASSWORD@localhost:$($ports.postgresql)/swai
NEO4J_URL=bolt://localhost:$($ports.neo4j_bolt)
QDRANT_URL=http://localhost:$($ports.qdrant)
TEMPORAL_HOST=localhost:$($ports.temporal)

# Service URLs
TTS_ADAPTER_URL=http://localhost:$($ports.elevenlabs)
FISHSPEECH_URL=http://localhost:$($ports.tts)
STT_URL=http://localhost:$($ports.stt)
OLLAMA_HOST=http://localhost:$($ports.ollama)
LMSTUDIO_HOST=http://localhost:$($ports.lmstudio)

# Data directories
DATA_DIR=$DataDir
VOICES_DIR=$DataDir\voices
MODELS_DIR=$DataDir\models
LOGS_DIR=$DataDir\logs

# Application settings
JWT_SECRET=$(([System.Web.Security.Membership]::GeneratePassword(32, 0)))
FRONTEND_PORT=$($ports.frontend)
BACKEND_PORT=$($ports.backend)
"@

Set-Content -Path "$rootDir\.env.baremetal" -Value $envConfig -Encoding UTF8
Write-Host "✅ SwAIvyn environment configuration generated" -ForegroundColor Green

if ($ConfigOnly) {
    Write-Host "`n✅ Configuration files generated successfully!" -ForegroundColor Green
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Review and customize config files in: $DataDir\config" -ForegroundColor Gray
    Write-Host "2. Run this script again without -ConfigOnly to start services" -ForegroundColor Gray
    exit 0
}

# Create service management functions
Write-Host "`n🎯 Creating Service Management Scripts..." -ForegroundColor Cyan

# PostgreSQL service script
$pgServiceScript = @"
`$ErrorActionPreference = "Stop"
`$dataDir = "$($DataDir -replace '\\', '\\')"

# Initialize PostgreSQL if needed
if (-not (Test-Path "`$dataDir\postgresql\PG_VERSION")) {
    Write-Host "Initializing PostgreSQL database..." -ForegroundColor Yellow
    & initdb -D "`$dataDir\postgresql" -U postgres --auth=trust
    Write-Host "✅ PostgreSQL initialized" -ForegroundColor Green
}

# Start PostgreSQL
Write-Host "Starting PostgreSQL on port $($ports.postgresql)..." -ForegroundColor Green
& postgres -D "`$dataDir\postgresql" -p $($ports.postgresql)
"@

Set-Content -Path "$DataDir\start-postgresql.ps1" -Value $pgServiceScript -Encoding UTF8

# Neo4j service script  
$neo4jServiceScript = @"
`$ErrorActionPreference = "Stop"
`$dataDir = "$($DataDir -replace '\\', '\\')"

Write-Host "Starting Neo4j on ports $($ports.neo4j_http)/$($ports.neo4j_bolt)..." -ForegroundColor Green

# Set Neo4j environment
`$env:NEO4J_HOME = (Get-Command neo4j).Path | Split-Path | Split-Path
`$env:NEO4J_DATA = "`$dataDir\neo4j"
`$env:NEO4J_LOGS = "`$dataDir\logs\neo4j"

# Start Neo4j
& neo4j console --config-dir="`$dataDir\config"
"@

Set-Content -Path "$DataDir\start-neo4j.ps1" -Value $neo4jServiceScript -Encoding UTF8

# Qdrant service script
$qdrantServiceScript = @"
`$ErrorActionPreference = "Stop"

Write-Host "Starting Qdrant vector database on port $($ports.qdrant)..." -ForegroundColor Green

# Download Qdrant if not present
`$qdrantPath = "$DataDir\qdrant.exe"
if (-not (Test-Path `$qdrantPath)) {
    Write-Host "Downloading Qdrant..." -ForegroundColor Yellow
    `$url = "https://github.com/qdrant/qdrant/releases/latest/download/qdrant-x86_64-pc-windows-msvc.exe"
    Invoke-WebRequest -Uri `$url -OutFile `$qdrantPath
    Write-Host "✅ Qdrant downloaded" -ForegroundColor Green
}

# Start Qdrant
& `$qdrantPath --config-path "$DataDir\config\qdrant.yaml"
"@

Set-Content -Path "$DataDir\start-qdrant.ps1" -Value $qdrantServiceScript -Encoding UTF8

# Temporal service script
$temporalServiceScript = @"
`$ErrorActionPreference = "Stop"

Write-Host "Starting Temporal workflow engine on port $($ports.temporal)..." -ForegroundColor Green

# Download Temporal CLI if not present
`$temporalPath = "$DataDir\temporal.exe"
if (-not (Test-Path `$temporalPath)) {
    Write-Host "Downloading Temporal CLI..." -ForegroundColor Yellow
    `$url = "https://github.com/temporalio/cli/releases/latest/download/temporal_cli_windows_amd64.tar.gz"
    `$tempFile = "$DataDir\temporal.tar.gz"
    Invoke-WebRequest -Uri `$url -OutFile `$tempFile
    & tar -xzf `$tempFile -C "$DataDir"
    Remove-Item `$tempFile
    Write-Host "✅ Temporal CLI downloaded" -ForegroundColor Green
}

# Start Temporal server
& `$temporalPath server start-dev --config "$DataDir\config\temporal.yaml" --db-filename "$DataDir\temporal\temporal.db"
"@

Set-Content -Path "$DataDir\start-temporal.ps1" -Value $temporalServiceScript -Encoding UTF8

# TTS service script (basic HTTP server for Fish Speech)
$ttsServiceScript = @"
`$ErrorActionPreference = "Stop"

Write-Host "Starting Fish Speech TTS service on port $($ports.tts)..." -ForegroundColor Green

# Basic TTS server implementation
`$listener = [System.Net.HttpListener]::new()
`$listener.Prefixes.Add("http://localhost:$($ports.tts)/")
`$listener.Start()

Write-Host "✅ TTS service listening on http://localhost:$($ports.tts)" -ForegroundColor Green

while (`$listener.IsListening) {
    `$context = `$listener.GetContext()
    `$request = `$context.Request
    `$response = `$context.Response
    
    if (`$request.Url.AbsolutePath -eq "/health") {
        `$responseString = '{"status":"healthy","service":"fish-speech-tts"}'
        `$buffer = [System.Text.Encoding]::UTF8.GetBytes(`$responseString)
        `$response.ContentType = "application/json"
        `$response.ContentLength64 = `$buffer.Length
        `$response.OutputStream.Write(`$buffer, 0, `$buffer.Length)
    } elseif (`$request.Url.AbsolutePath -eq "/voices") {
        `$responseString = '{"voices":["default","neural","expressive"]}'
        `$buffer = [System.Text.Encoding]::UTF8.GetBytes(`$responseString)
        `$response.ContentType = "application/json"
        `$response.ContentLength64 = `$buffer.Length
        `$response.OutputStream.Write(`$buffer, 0, `$buffer.Length)
    } else {
        `$response.StatusCode = 404
    }
    
    `$response.Close()
}
"@

Set-Content -Path "$DataDir\start-tts.ps1" -Value $ttsServiceScript -Encoding UTF8

# Main startup script
$startupScript = @"
#!/usr/bin/env powershell

Write-Host "🚀 Starting SwAIvyn Bare Metal Services..." -ForegroundColor Cyan

# Import environment
Get-Content "$rootDir\.env.baremetal" | ForEach-Object {
    if (`$_ -match '^([^=]+)=(.*)$') {
        [Environment]::SetEnvironmentVariable(`$matches[1], `$matches[2], 'Process')
    }
}

# Function to start service in background
function Start-BackgroundService {
    param([string]`$Name, [string]`$ScriptPath, [int]`$Port)
    
    Write-Host "Starting `$Name..." -ForegroundColor Green
    `$process = Start-Process powershell -ArgumentList @(
        "-NoExit", "-ExecutionPolicy", "Bypass", "-File", `$ScriptPath
    ) -WindowStyle Minimized -PassThru
    
    Write-Host "✅ `$Name started (PID: `$(`$process.Id))" -ForegroundColor Green
    
    # Wait for service to be ready
    if (Wait-ForPort `$Port 30) {
        Write-Host "✅ `$Name is ready on port `$Port" -ForegroundColor Green
    } else {
        Write-Warning "`$Name may not be ready on port `$Port"
    }
    
    return `$process.Id
}

function Wait-ForPort { 
    param([int]`$Port, [int]`$TimeoutSec = 60) 
    for (`$i = 0; `$i -lt `$TimeoutSec; `$i++) { 
        try {
            `$connection = New-Object System.Net.Sockets.TcpClient
            `$connection.Connect("127.0.0.1", `$Port)
            `$connection.Close()
            return `$true
        } catch { 
            Start-Sleep 1 
        }
    } 
    return `$false 
}

# Start infrastructure services
Write-Host "`n🗄️ Starting Infrastructure Services..." -ForegroundColor Yellow
`$pids = @{}

`$pids.postgresql = Start-BackgroundService "PostgreSQL" "$DataDir\start-postgresql.ps1" $($ports.postgresql)
Start-Sleep 5  # Give PostgreSQL time to start

`$pids.neo4j = Start-BackgroundService "Neo4j" "$DataDir\start-neo4j.ps1" $($ports.neo4j_http)
`$pids.qdrant = Start-BackgroundService "Qdrant" "$DataDir\start-qdrant.ps1" $($ports.qdrant)
`$pids.temporal = Start-BackgroundService "Temporal" "$DataDir\start-temporal.ps1" $($ports.temporal)
`$pids.tts = Start-BackgroundService "TTS" "$DataDir\start-tts.ps1" $($ports.tts)

Write-Host "`n🎯 Starting Application Services..." -ForegroundColor Yellow

# Start backend
Write-Host "Starting SwAIvyn Backend..." -ForegroundColor Green
Push-Location "$rootDir\Services\bff"
`$backendProcess = Start-Process python -ArgumentList @(
    "-m", "uvicorn", "app.main:app", 
    "--host", "0.0.0.0", 
    "--port", "$($ports.backend)",
    "--reload"
) -WindowStyle Normal -PassThru
`$pids.backend = `$backendProcess.Id
Pop-Location

Start-Sleep 3

# Start frontend  
Write-Host "Starting SwAIvyn Frontend..." -ForegroundColor Green
Push-Location "$rootDir\frontend"
`$frontendProcess = Start-Process npm -ArgumentList @(
    "run", "dev", "--", "--host", "0.0.0.0", "--port", "$($ports.frontend)"
) -WindowStyle Normal -PassThru
`$pids.frontend = `$frontendProcess.Id
Pop-Location

Write-Host "`n" + ("="*60) -ForegroundColor Yellow
Write-Host "🎉 SwAIvyn Bare Metal Services Started!" -ForegroundColor Green
Write-Host ("="*60) -ForegroundColor Yellow

Write-Host "`n📱 ACCESS URLS:" -ForegroundColor Cyan
Write-Host "Frontend: http://localhost:$($ports.frontend)" -ForegroundColor White
Write-Host "Backend API: http://localhost:$($ports.backend)" -ForegroundColor White

Write-Host "`n🗄️ INFRASTRUCTURE:" -ForegroundColor Cyan
Write-Host "PostgreSQL: localhost:$($ports.postgresql)" -ForegroundColor White
Write-Host "Neo4j Browser: http://localhost:$($ports.neo4j_http)" -ForegroundColor White
Write-Host "Qdrant: http://localhost:$($ports.qdrant)" -ForegroundColor White
Write-Host "Temporal: http://localhost:$($ports.temporal)" -ForegroundColor White

Write-Host "`n💾 DATA DIRECTORY: $DataDir" -ForegroundColor Cyan

Write-Host "`n🔧 PROCESS IDs:" -ForegroundColor Yellow
foreach (`$service in `$pids.Keys) {
    Write-Host "  `$service : `$(`$pids[`$service])" -ForegroundColor Gray
}

Write-Host "`n⚠️  To stop all services, run: Stop-Process `$(`$pids.Values -join ',') -Force" -ForegroundColor Yellow
Write-Host "Or close this PowerShell window and all service windows." -ForegroundColor Gray

# Save PIDs for cleanup
`$pids | ConvertTo-Json | Set-Content "$DataDir\service-pids.json"

Write-Host "`nPress Ctrl+C to stop all services..." -ForegroundColor Yellow
try {
    while (`$true) { Start-Sleep 1 }
} finally {
    Write-Host "`nStopping all services..." -ForegroundColor Yellow
    Stop-Process `$pids.Values -Force -ErrorAction SilentlyContinue
}
"@

Set-Content -Path "$rootDir\start-bare-metal.ps1" -Value $startupScript -Encoding UTF8
Write-Host "✅ Main startup script created: start-bare-metal.ps1" -ForegroundColor Green

# Create shutdown script
$shutdownScript = @"
#!/usr/bin/env powershell

Write-Host "🛑 Stopping SwAIvyn Bare Metal Services..." -ForegroundColor Yellow

# Load saved PIDs
`$pidsFile = "$DataDir\service-pids.json"
if (Test-Path `$pidsFile) {
    `$pids = Get-Content `$pidsFile | ConvertFrom-Json
    
    foreach (`$service in `$pids.PSObject.Properties.Name) {
        `$pid = `$pids.`$service
        Write-Host "Stopping `$service (PID: `$pid)..." -ForegroundColor Gray
        try {
            Stop-Process -Id `$pid -Force -ErrorAction SilentlyContinue
            Write-Host "✅ `$service stopped" -ForegroundColor Green
        } catch {
            Write-Warning "Failed to stop `$service : `$_"
        }
    }
    
    Remove-Item `$pidsFile -ErrorAction SilentlyContinue
} else {
    Write-Warning "No PID file found, attempting to stop by process name..."
    
    # Stop by process name
    `$processes = @("postgres", "neo4j", "qdrant", "temporal", "python", "node", "npm")
    foreach (`$proc in `$processes) {
        try {
            Get-Process -Name `$proc -ErrorAction SilentlyContinue | Stop-Process -Force
            Write-Host "✅ Stopped `$proc processes" -ForegroundColor Green
        } catch {
            # Ignore errors for processes that don't exist
        }
    }
}

Write-Host "✅ All SwAIvyn services stopped" -ForegroundColor Green
"@

Set-Content -Path "$rootDir\stop-bare-metal.ps1" -Value $shutdownScript -Encoding UTF8
Write-Host "✅ Shutdown script created: stop-bare-metal.ps1" -ForegroundColor Green

Write-Host "`n" + ("="*60) -ForegroundColor Yellow
Write-Host "🎉 SwAIvyn Bare Metal Setup Complete!" -ForegroundColor Green  
Write-Host ("="*60) -ForegroundColor Yellow

Write-Host "`n📋 NEXT STEPS:" -ForegroundColor Cyan
Write-Host "1. Review configuration files in: $DataDir\config" -ForegroundColor White
Write-Host "2. Ensure .env file has required passwords (POSTGRES_PASSWORD, etc.)" -ForegroundColor White
Write-Host "3. Start all services: .\start-bare-metal.ps1" -ForegroundColor White
Write-Host "4. Access the application at: http://localhost:$($ports.frontend)" -ForegroundColor White

Write-Host "`n🔧 MANAGEMENT COMMANDS:" -ForegroundColor Cyan
Write-Host "Start all services: .\start-bare-metal.ps1" -ForegroundColor White
Write-Host "Stop all services: .\stop-bare-metal.ps1" -ForegroundColor White

Write-Host "`n💾 DATA LOCATION:" -ForegroundColor Cyan
Write-Host "All data stored in: $DataDir" -ForegroundColor White

Write-Host "`n⚠️  REQUIREMENTS:" -ForegroundColor Yellow
Write-Host "- Windows 10/11 with PowerShell 7+" -ForegroundColor Gray
Write-Host "- Administrator privileges (for initial setup)" -ForegroundColor Gray  
Write-Host "- At least 4GB RAM available" -ForegroundColor Gray
Write-Host "- Ports $($ports.frontend)-$($ports.backend + 100) available" -ForegroundColor Gray

if (-not $SkipInstall -and -not $InstallDependencies) {
    Write-Host "`n💡 TIP: Run with -InstallDependencies to automatically install all required software" -ForegroundColor Yellow
}