# Hybrid startup script: ensures infra containers are running/healthy, then launches BFF, Orchestrator, Frontend on host.
param(
    [switch]$IncludeTTS = $true,
    [int]$BffPort = 5100,
    [int]$FrontPort = 5173,
    [int]$ActivityThreads = 32,
    [string]$ComposeFile = "$PSScriptRoot/../docker-compose.yml",
    [int]$TimeoutSec = 600,
    [switch]$NoStopAppContainers = $false,
    [switch]$UseHostTTS = $false
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/.."
$stateFile = Join-Path $PSScriptRoot '.dev-state.json'

function Test-Command {
    param([Parameter(Mandatory)][string]$Name)
    try { Get-Command $Name -ErrorAction Stop | Out-Null; return $true } catch { return $false }
}

function Require-Docker {
    try {
        $null = docker info -f '{{.ServerVersion}}' 2>$null
    } catch {
        throw 'Docker Desktop is not running or not reachable. Please start Docker Desktop and retry.'
    }
}


function Get-ContainerId {
    param([Parameter(Mandatory)][string]$Service)
    $id = docker compose -f $ComposeFile ps -q $Service 2>$null
    return ($id | Select-Object -First 1)
}

function Ensure-Service-Up {
    param(
        [Parameter(Mandatory)][string]$Service,
        [int]$Port,
        [switch]$HasHealth,
        [string]$HttpUrl,
        [int]$Timeout = $TimeoutSec
    )
    $start = Get-Date
    $id = Get-ContainerId -Service $Service
    if (-not $id) {
        Write-Host "[$Service] starting..." -ForegroundColor Cyan
        docker compose -f $ComposeFile up -d --no-build $Service | Out-Null
        $id = Get-ContainerId -Service $Service
    } else {
        # Ensure it's running
        $status = docker inspect -f '{{.State.Status}}' $id 2>$null
        if ($status -ne 'running') {
            Write-Host "[$Service] not running (status=$status). Starting..." -ForegroundColor Yellow
            docker compose -f $ComposeFile up -d --no-build $Service | Out-Null
        } else {
            Write-Host "[$Service] already running (container $id)" -ForegroundColor DarkGray
        }
    }

    # Wait for readiness
    while ($true) {
        $elapsed = (Get-Date) - $start
        if ($elapsed.TotalSeconds -ge $Timeout) { throw "[$Service] readiness timed out after ${Timeout}s" }

        $id = Get-ContainerId -Service $Service
        if (-not $id) { Start-Sleep -Seconds 1; continue }

        $state = docker inspect -f '{{.State.Status}}' $id 2>$null
        if ($state -ne 'running') { Start-Sleep -Seconds 1; continue }

        if ($HasHealth) {
            $health = docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{end}}' $id 2>$null
            if ($health -eq 'healthy') { break }
        } elseif ($HttpUrl) {
            try {
                $resp = Invoke-WebRequest -Uri $HttpUrl -UseBasicParsing -TimeoutSec 5
                if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 500) { break }
            } catch { }
        } elseif ($Port) {
            $ok = (Test-NetConnection -ComputerName 'localhost' -Port $Port -WarningAction SilentlyContinue).TcpTestSucceeded
            if ($ok) { break }
        } else {
            break
        }
        Start-Sleep -Seconds 2
    }
    Write-Host "[$Service] ready" -ForegroundColor Green
}

function Test-PortFree {
    param([int]$Port)
    $conn = (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue)
    return -not $conn
}

function Start-HostProc {
    param(
        [Parameter(Mandatory)][string]$ScriptPath,
        [string[]]$Args,
        [string]$Title
    )
    $argLine = @('-NoExit','-Command',"& '$ScriptPath' $Args")
    $p = Start-Process powershell -ArgumentList $argLine -PassThru -WindowStyle Normal
    if ($Title) { try { $p.MainWindowTitle = $Title } catch { } }
    return $p
}

if (-not (Test-Command docker)) { throw 'Docker is not available in PATH. Install/Start Docker Desktop first.' }
Require-Docker

Write-Host '=== Ensuring infra is up ===' -ForegroundColor Cyan
# Ensure containerized app-tier services are stopped to free ports for host apps (can be disabled)
if (-not $NoStopAppContainers) {
    try {
        $services = @()
        try { $services = (docker compose -f $ComposeFile config --services 2>$null) } catch { $services = @() }
        $toStop = @()
        foreach ($svc in @('bff','orchestrator','frontend')) {
            if ($services -contains $svc) { $toStop += $svc }
        }
        if ($toStop.Count -gt 0) {
            Write-Host ('Stopping containerized app services to free ports: ' + ($toStop -join ', ')) -ForegroundColor DarkGray
            docker compose -f $ComposeFile stop @toStop | Out-Null
        }
    } catch { }
}

    $infra = @(
        @{ Name='swai-db'; Port=5432; HasHealth=$true },
        @{ Name='temporal'; Port=7233; HasHealth=$false },
        @{ Name='qdrant'; Port=6333; HasHealth=$false },
        @{ Name='neo4j'; Port=7474; HasHealth=$true },
        @{ Name='stt'; Port=9000; HasHealth=$true; HttpUrl='http://localhost:9000/docs' }
    )
    # Removed ElevenLabs adapter from the default infra list
    if ($IncludeTTS) {
        $infra += @{ Name='tts'; Port=8081; HasHealth=$true; HttpUrl='http://localhost:8081/health' }
    }

    foreach ($s in $infra) {
        Ensure-Service-Up -Service $s.Name -Port $s.Port -HasHealth:$($s.HasHealth) -HttpUrl $s.HttpUrl
    }
 

Write-Host '=== Launching host services ===' -ForegroundColor Cyan

$pids = @{}

# BFF
if (Test-PortFree -Port $BffPort) {
    $b = Start-HostProc -ScriptPath (Join-Path $PSScriptRoot 'dev-bff.ps1') -Args "-Port $BffPort" -Title 'SwAIvyn BFF'
    $pids.bff = $b.Id
    # quick wait for port
    1..30 | ForEach-Object { if (Test-PortFree -Port $BffPort) { Start-Sleep -Milliseconds 250 } }
} else {
    Write-Host "BFF port $BffPort is in use; assuming BFF is already running." -ForegroundColor Yellow
}

# Orchestrator (no port to test)
$o = Start-HostProc -ScriptPath (Join-Path $PSScriptRoot 'dev-orchestrator.ps1') -Args "-ActivityThreads $ActivityThreads" -Title 'SwAIvyn Orchestrator'
$pids.orchestrator = $o.Id

# Frontend
if (Test-PortFree -Port $FrontPort) {
    $f = Start-HostProc -ScriptPath (Join-Path $PSScriptRoot 'dev-frontend.ps1') -Args "-Port $FrontPort" -Title 'SwAIvyn Frontend'
    $pids.frontend = $f.Id
} else {
    Write-Host "Frontend port $FrontPort is in use; assuming Vite is already running." -ForegroundColor Yellow
}

# Host TTS (Fish Speech)
if ($UseHostTTS) {
    if (Test-PortFree -Port 8081) {
        $t = Start-HostProc -ScriptPath (Join-Path $PSScriptRoot 'dev-tts.ps1') -Args "-Port 8081" -Title 'SwAIvyn TTS (host)'
        $pids.tts = $t.Id
        # Wait up to ~10s for port
        1..40 | ForEach-Object { if (Test-PortFree -Port 8081) { Start-Sleep -Milliseconds 250 } }
    } else {
        Write-Host "TTS port 8081 is in use; assuming host TTS is already running." -ForegroundColor Yellow
    }
}

$state = [ordered]@{
    timestamp = (Get-Date).ToString('s')
    includeTTS = [bool]$IncludeTTS
    ports = @{ bff=$BffPort; frontend=$FrontPort }
    pids = $pids
}
$state | ConvertTo-Json | Set-Content -Encoding UTF8 $stateFile

Write-Host "\nReady:" -ForegroundColor Green
Write-Host "- UI:           http://localhost:$FrontPort" -ForegroundColor Green
Write-Host "- BFF API:      http://localhost:$BffPort" -ForegroundColor Green
Write-Host "- Temporal:     localhost:7233" -ForegroundColor Green
Write-Host "- Qdrant:       http://localhost:6333" -ForegroundColor Green
Write-Host "- Postgres:     localhost:5432" -ForegroundColor Green
if ($UseHostTTS) { Write-Host "- TTS (host):    http://localhost:8081" -ForegroundColor Green }

