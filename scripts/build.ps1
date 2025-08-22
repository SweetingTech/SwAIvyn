param(
  [string[]]$Services = @(),
  [switch]$NoCache
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
Set-Location $repo

function Write-Info($msg){ Write-Host "[BUILD] $msg" -ForegroundColor Cyan }
function Exec($cmd){ Write-Info $cmd; & powershell -NoProfile -Command $cmd; if($LASTEXITCODE -ne 0){ throw "Command failed: $cmd" } }

# 1) Ensure Docker Desktop is running
Write-Info "Checking Docker..."
try { docker version | Out-Null } catch { throw "Docker does not appear to be running." }

# 2) Build backend to validate
Write-Info "Building backend (dotnet build)..."
Exec "dotnet build backend -c Debug"

# 3) Remove old containers for the services being updated, then build
$compose = "docker-compose.hybrid.yml"
$svcList = if($Services.Count -gt 0){ $Services -join ' ' } else { '' }
$noCacheFlag = if($NoCache){ "--no-cache" } else { "" }

if($Services.Count -gt 0){
  Write-Info "Stopping containers for: $svcList"
  Exec "docker compose -f $compose stop $svcList"
  Write-Info "Removing old containers for: $svcList"
  Exec "docker compose -f $compose rm -f $svcList"
  Write-Info "Building specific services: $svcList"
  Exec "docker compose -f $compose build $noCacheFlag $svcList"
} else {
  Write-Info "Stopping running containers (all services)"
  Exec "docker compose -f $compose stop"
  Write-Info "Removing old containers (all services)"
  Exec "docker compose -f $compose rm -f"
  Write-Info "Building all services in $compose"
  Exec "docker compose -f $compose build $noCacheFlag"
}

Write-Info "Build complete."

