$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
Set-Location $repo

function Write-Info($msg){ Write-Host "[START] $msg" -ForegroundColor Green }
function Exec($cmd){ Write-Info $cmd; & powershell -NoProfile -Command $cmd; if($LASTEXITCODE -ne 0){ throw "Command failed: $cmd" } }

# 0) Kill any existing SwAIvyn backend processes to avoid SQLite locks
Write-Info "Ensuring no previous SwAIvyn process is running..."
# Kill self-hosted exe if present
Get-Process -Name "SwAIvyn" -ErrorAction SilentlyContinue | ForEach-Object { Write-Info "Killing PID $($_.Id)"; $_.Kill(); $_.WaitForExit() }
# Also kill any dotnet runner hosting SwAIvyn.dll
Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'dotnet.exe' -and $_.CommandLine -match 'SwAIvyn.dll' } | ForEach-Object {
  Write-Info "Killing dotnet PID $($_.ProcessId) hosting SwAIvyn.dll"; Stop-Process -Id $_.ProcessId -Force
}

# 1) Ensure Docker Desktop is running
Write-Info "Checking Docker..."
try { docker version | Out-Null } catch { throw "Docker does not appear to be running." }

$compose = "docker-compose.hybrid.yml"

# 2) Start DB/infra services first
Write-Info "Starting core services (Neo4j, Weaviate stack, TTS, Frontend)..."
Exec "docker compose -f $compose up -d neo4j weaviate multi2vec-clip qna-transformers sum-transformers text-spellcheck reranker-transformers swai-tts frontend"

# 3) Start backend (dev run)
Write-Info "Starting backend (dotnet run on http://localhost:5000)..."
Start-Process -NoNewWindow -FilePath "dotnet" -ArgumentList "run --project backend --urls http://localhost:5000" | Out-Null
Start-Sleep -Seconds 3

# 4) Optionally start search service if present
if (Test-Path "scripts/run-search-dev.cmd") {
  Write-Info "Starting search service (run-search-dev.cmd)..."
  Start-Process -NoNewWindow -FilePath "cmd.exe" -ArgumentList "/c scripts\\run-search-dev.cmd" | Out-Null
}

# 5) Health checks (light)
Write-Info "Waiting for backend /api/health..."
try { $r = Invoke-WebRequest -Uri "http://localhost:5000/api/health" -UseBasicParsing -TimeoutSec 10; Write-Info "Backend health: $($r.StatusCode)" } catch { Write-Info "Backend health check failed (likely still starting)" }

Write-Info "Start-up sequence initiated."

