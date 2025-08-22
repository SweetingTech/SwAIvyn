param(
  [string[]]$Services = @()
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
Set-Location $repo

function Write-Info($msg){ Write-Host "[UPDATE] $msg" -ForegroundColor Magenta }
function Exec($cmd){ Write-Info $cmd; & powershell -NoProfile -Command $cmd; if($LASTEXITCODE -ne 0){ throw "Command failed: $cmd" } }

Write-Info "Checking Docker..."
try { docker version | Out-Null } catch { throw "Docker does not appear to be running." }

$compose = "docker-compose.hybrid.yml"

# Pull upstream images for image-based services
if ($Services.Count -gt 0) {
  Write-Info "Pulling updates for: $($Services -join ', ')"
  Exec "docker compose -f $compose pull $($Services -join ' ')"
} else {
  Write-Info "Pulling updates for all services"
  Exec "docker compose -f $compose pull"
}

# Rebuild local build contexts
Write-Info "Rebuilding local images (if any)"
Exec "docker compose -f $compose build"

# Recreate containers with updated images
Write-Info "Recreating containers with updated images"
Exec "docker compose -f $compose up -d --remove-orphans"

Write-Info "Update complete."

