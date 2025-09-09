# Development script for SwAIvyn - runs frontend and backend with hot reloading
param (
    [switch] $FrontendOnly = $false,
    [switch] $BackendOnly = $false,
    [switch] $StartTTS = $false,
    [string] $TTSUpstream = 'http://host.docker.internal:8080',
    [switch] $UseTraefik = $false,
    [switch] $Swarm = $false,
    [string] $StackName = 'swai'
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot
$stateFile = Join-Path $PSScriptRoot '.dev-state.json'
$pids = @{}

Write-Host "Starting SwAIvyn in development mode..." -ForegroundColor Cyan
Write-Host "Starting user-facing services first..." -ForegroundColor Yellow

# --- START OF FRONTEND/BFF/ORCHESTRATOR ---

if (-not $BackendOnly) {
    # Start frontend dev server (PowerShell window, NoExit)
    Write-Host "Starting frontend development server..." -ForegroundColor Green
    $proc = Start-Process powershell -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-Command", "cd '$rootDir\frontend'; npm run dev" -WindowStyle Normal -PassThru
    if ($proc) { $pids['frontend'] = $proc.Id }
    Write-Host "Frontend dev server starting at http://localhost:5173" -ForegroundColor Green
}

if (-not $FrontendOnly) {
    # Wait a moment for frontend to start
    if (-not $BackendOnly) {
        Start-Sleep -Seconds 2
    }
    
    # Start backend
    Write-Host "Starting backend development server..." -ForegroundColor Green
    # Route BFF to Traefik frontends by default
    $env:TTS_ADAPTER_URL = 'http://elevenlabs.localhost'
    $env:FISHSPEECH_URL = 'http://tts.localhost'
    # Prefer Traefik routes for HTTP services
    if (-not $env:QDRANT_URL) { $env:QDRANT_URL = 'http://qdrant.localhost' }
    # Temporal and Bolt are non-HTTP; keep direct ports unless TCP routers configured
    if (-not $env:TEMPORAL_HOST) { $env:TEMPORAL_HOST = 'localhost:7233' }
    if (-not $env:NEO4J_URL) { $env:NEO4J_URL = 'bolt://localhost:7687' }
    # Start BFF (PowerShell window, NoExit)
    $proc = Start-Process powershell -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-Command", "& '$PSScriptRoot\dev-bff.ps1'" -WindowStyle Normal -PassThru
    if ($proc) { $pids['bff'] = $proc.Id }
    Write-Host "Backend dev server starting at http://localhost:5000" -ForegroundColor Green

    # Start Orchestrator (Temporal worker) in its own window
    Write-Host "Starting orchestrator worker..." -ForegroundColor Green
    $env:TTS_ADAPTER_URL = 'http://elevenlabs.localhost'
    $env:FISHSPEECH_URL = 'http://tts.localhost'
    if (-not $env:TEMPORAL_HOST) { $env:TEMPORAL_HOST = 'localhost:7233' }
    # Start Orchestrator (PowerShell window, NoExit)
    $proc = Start-Process powershell -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-Command", "& '$PSScriptRoot\dev-orchestrator.ps1'" -WindowStyle Normal -PassThru
    if ($proc) { $pids['orchestrator'] = $proc.Id }
    Write-Host "Orchestrator worker started" -ForegroundColor Green
}

# --- END OF FRONTEND/BFF/ORCHESTRATOR ---

Write-Host "`nStarting background services (Docker, etc.)...`n" -ForegroundColor Cyan

# Preflight: ensure legacy Compose infra (if any) is stopped to avoid port conflicts
try {
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        $composeFile = Join-Path $rootDir 'docker-compose.yml'
        if (Test-Path $composeFile) {
            Write-Host 'Stopping any Compose infra from previous runs...' -ForegroundColor DarkGray
            & docker compose -f $composeFile down --remove-orphans 2>$null | Out-Null
        }
    }
} catch {}

# Ensure core Docker stack (Traefik + services) is up; builds images if missing
try {
    $stackUp = Join-Path $PSScriptRoot 'docker-stack-up.ps1'
    if (Test-Path $stackUp) {
        Write-Host 'Ensuring Docker Swarm stack is up (Traefik + services)...' -ForegroundColor Cyan
        & $stackUp -ErrorAction Stop
    } else {
        Write-Warning "Stack helper not found: $stackUp"
    }
} catch {
    Write-Warning ("Failed to ensure Docker stack: {0}" -f $_.Exception.Message)
}

# Wait for Temporal port from Swarm stack (up to ~10s)
$ok = $false
1..20 | ForEach-Object {
    try { if ((Test-NetConnection -ComputerName 127.0.0.1 -Port 7233 -WarningAction SilentlyContinue).TcpTestSucceeded) { $ok = $true; break } } catch {}
    Start-Sleep -Milliseconds 500
}
if ($ok) { Write-Host 'Temporal reachable on :7233' -ForegroundColor DarkGreen } else { Write-Warning 'Temporal not reachable yet; the BFF will retry connecting.' }

# Optionally start the TTS proxy container (Fish-Speech proxy)
if ($StartTTS) {
    try {
        $ttsScript = Join-Path $PSScriptRoot 'tts-docker-build.ps1'
        if (-not (Test-Path $ttsScript)) {
            Write-Warning "TTS start requested but script not found: $ttsScript"
        } else {
            if ($Swarm) {
                Write-Host "Deploying TTS via Swarm + Traefik stack..." -ForegroundColor Green
                & $ttsScript -SwarmDeploy -StackName $StackName -UpstreamUrl $TTSUpstream
            } else {
                Write-Host "Starting TTS (proxy) container..." -ForegroundColor Green
                & $ttsScript -Up -UpstreamUrl $TTSUpstream
            }
        }
    } catch {
        Write-Warning ("Failed to start TTS proxy: {0}" -f $_.Exception.Message)
    }
}

# --- FINALIZATION ---

Write-Host "`nDevelopment servers are starting...`n" -ForegroundColor Yellow
Write-Host "Frontend: http://localhost:5173 (with hot reloading)" -ForegroundColor Cyan
Write-Host "Backend:  http://localhost:5000" -ForegroundColor Cyan

# Also print LAN-access URLs
try {
    $lanIPs = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName()) |
        Where-Object { $_.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork } |
        ForEach-Object { $_.IPAddressToString }
    if ($lanIPs) {
        foreach ($ip in $lanIPs) {
            Write-Host ("LAN Frontend: http://{0}:5173" -f $ip) -ForegroundColor DarkCyan
            Write-Host ("LAN Backend:  http://{0}:5000" -f $ip) -ForegroundColor DarkCyan
        }
    }
} catch {}
Write-Host "`nMake changes to your frontend code and they will automatically refresh!" -ForegroundColor Green
Write-Host "Press Ctrl+C in the terminal windows to stop the servers." -ForegroundColor Yellow

# Save PIDs to state file for stop-all.ps1
if ($pids.Count -gt 0) {
    Write-Host "`nSaving process IDs to state file for cleanup..."
    $pids | ConvertTo-Json | Set-Content -Path $stateFile -Encoding UTF8
}