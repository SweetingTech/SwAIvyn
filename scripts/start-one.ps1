param(
  [Parameter(Mandatory=$true)][string]$Service,
  [switch]$Restart
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
Set-Location $repo

function Write-Info($msg){ Write-Host "[ONE] $msg" -ForegroundColor Yellow }
function Exec($cmd){ Write-Info $cmd; & powershell -NoProfile -Command $cmd; if($LASTEXITCODE -ne 0){ throw "Command failed: $cmd" } }

Write-Info "Checking Docker..."
try { docker version | Out-Null } catch { throw "Docker does not appear to be running." }

$compose = "docker-compose.hybrid.yml"

if ($Restart) {
  Exec "docker compose -f $compose restart $Service"
} else {
  Exec "docker compose -f $compose up -d $Service"
}

Exec "docker ps --filter name=$Service --format 'table {{.Names}}\t{{.Image}}\t{{.Ports}}\t{{.Status}}'"

