# Shutdown script for SwAIvyn - stops all Docker containers and non-Docker services
param(
    [string]$StackName = 'swaivyn',
    [switch]$DownCompose = $false,
    [switch]$Prune = $false,
    [switch]$Aggressive = $false
)

$ErrorActionPreference = 'Stop'
$rootDir = $PSScriptRoot
$stateFile = Join-Path $rootDir '.dev-state.json'

# --- Utility Functions ---
function Test-Command { 
    param([string]$Name) 
    try { Get-Command $Name -ErrorAction Stop | Out-Null; return $true } catch { return $false } 
}

function Get-ListeningPids {
    param([int[]]$Ports)
    $pids = @()
    try {
        $net = netstat -ano | Select-String -Pattern 'LISTENING'
        foreach ($port in $Ports) {
            $matchList = $net | Where-Object { $_.Line -match (":$port[\s]") }
            foreach ($m in $matchList) {
                $parts = ($m.Line -split '\s+') | Where-Object { $_ -ne '' }
                if ($parts.Length -ge 5) { 
                    $proc = [int]$parts[-1]
                    if ($proc -gt 0) { $pids += $proc }
                }
            }
        }
    } catch {}
    return ($pids | Select-Object -Unique)
}

function Stop-HostDevProcs {
    Write-Host '-> Stopping host dev processes (frontend/backend/orchestrator)...' -ForegroundColor Yellow
    $ports = @(5173, 5000, 8000)  # Frontend, BFF, and potential backend ports
    $allowedProcesses = @('node', 'python', 'py', 'uvicorn', 'cmd', 'powershell', 'python.exe')
    
    foreach ($procId in (Get-ListeningPids -Ports $ports)) {
        try {
            $p = Get-Process -Id $procId -ErrorAction Stop
            if ($allowedProcesses -contains $p.Name.ToLower()) {
                Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
                Write-Host ("  ✓ Stopped PID {0} ({1})" -f $procId, $p.Name) -ForegroundColor Green
            }
        } catch {}
    }
}

function Stop-FromStateFile {
    Write-Host '-> Stopping services from state file...' -ForegroundColor Yellow
    if (-not (Test-Path $stateFile)) { 
        Write-Host "  No state file found at $stateFile" -ForegroundColor DarkGray
        return 
    }
    
    try {
        $state = Get-Content -Raw $stateFile | ConvertFrom-Json
        if ($state -and $state.pids) {
            foreach ($serviceName in @('frontend', 'bff', 'orchestrator')) {
                $procId = $state.pids.$serviceName
                if ($procId) {
                    try { 
                        $p = Get-Process -Id $procId -ErrorAction Stop
                        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
                        Write-Host ("  ✓ Stopped {0} (PID {1})" -f $serviceName, $procId) -ForegroundColor Green
                    } catch {
                        Write-Host ("  ⚠ Process {0} (PID {1}) not found" -f $serviceName, $procId) -ForegroundColor DarkGray
                    }
                }
            }
        }
    } catch {
        Write-Warning "Failed to read state file: $($_.Exception.Message)"
    }
    
    # Clean up state file
    try { 
        Remove-Item $stateFile -Force -ErrorAction SilentlyContinue | Out-Null
        Write-Host "  ✓ Cleaned up state file" -ForegroundColor Green
    } catch {}
}

function Remove-SwarmStack {
    param([string]$Name)
    if (-not (Test-Command 'docker')) { 
        Write-Warning "Docker CLI not found. Skipping Docker stack removal."
        return 
    }
    
    try {
        Write-Host ("-> Removing Docker Swarm stack '{0}'..." -f $Name) -ForegroundColor Yellow
        & docker stack rm $Name 2>$null | Out-Null
        
        # Wait until services are gone (up to ~60s)
        Write-Host "  Waiting for stack services to stop..." -ForegroundColor DarkGray
        $maxWait = 60
        for ($i = 1; $i -le $maxWait; $i++) {
            try {
                $services = (& docker stack services $Name 2>$null)
                if (-not $services -or $services.Count -eq 0) { 
                    Write-Host ("  ✓ Stack '{0}' removed successfully" -f $Name) -ForegroundColor Green
                    break 
                }
                if ($i -eq $maxWait) {
                    Write-Warning "Stack removal timed out after $maxWait seconds"
                }
            } catch { 
                Write-Host ("  ✓ Stack '{0}' removed successfully" -f $Name) -ForegroundColor Green
                break 
            }
            Start-Sleep -Seconds 1
        }
    } catch {
        Write-Warning "Failed to remove Docker stack: $($_.Exception.Message)"
    }
}

function Stop-ComposeInfra {
    $composeFile = Join-Path $rootDir 'docker-compose.yml'
    if (-not (Test-Path $composeFile)) { 
        Write-Host "  No docker-compose.yml found, skipping" -ForegroundColor DarkGray
        return 
    }
    if (-not (Test-Command 'docker')) { return }
    
    try {
        if ($DownCompose) {
            Write-Host '-> Running docker compose down (complete removal)...' -ForegroundColor Yellow
            & docker compose -f $composeFile down --remove-orphans
            Write-Host "  ✓ Docker Compose services removed" -ForegroundColor Green
        } else {
            Write-Host '-> Stopping Docker Compose infrastructure services...' -ForegroundColor Yellow
            $infraServices = @('swai-db', 'temporal', 'qdrant', 'neo4j', 'stt', 'tts', 'tts-11labs-adapter')
            & docker compose -f $composeFile stop @infraServices
            Write-Host "  ✓ Infrastructure services stopped" -ForegroundColor Green
        }
    } catch {
        Write-Warning "Failed to stop Docker Compose services: $($_.Exception.Message)"
    }
}

function Stop-KnownContainers {
    if (-not (Test-Command 'docker')) { return }
    
    Write-Host '-> Stopping any remaining known containers...' -ForegroundColor Yellow
    $knownContainers = @('vllm-chat', 'vllm-embed', 'swaivyn-tts-1')
    
    foreach ($containerName in $knownContainers) {
        try { 
            & docker stop $containerName 2>$null | Out-Null
            Write-Host ("  ✓ Stopped container: {0}" -f $containerName) -ForegroundColor Green
        } catch {}
        
        if ($DownCompose) {
            try { 
                & docker rm $containerName 2>$null | Out-Null 
                Write-Host ("  ✓ Removed container: {0}" -f $containerName) -ForegroundColor Green
            } catch {}
        }
    }
}

function Cleanup-Networks {
    if (-not (Test-Command 'docker')) { return }
    if (-not $Prune) { return }
    
    try { 
        Write-Host '-> Pruning unused Docker networks...' -ForegroundColor Yellow
        & docker network prune -f 2>$null | Out-Null
        Write-Host "  ✓ Unused networks pruned" -ForegroundColor Green
    } catch {
        Write-Warning "Failed to prune networks: $($_.Exception.Message)"
    }
}

# --- MAIN EXECUTION ---
Write-Host "🛑 Shutting down SwAIvyn development environment..." -ForegroundColor Cyan
Write-Host ("Stack name: {0}" -f $StackName) -ForegroundColor DarkGray
Write-Host ""

# Stop services in reverse order of startup
Stop-HostDevProcs
Stop-FromStateFile
Remove-SwarmStack -Name $StackName
Stop-ComposeInfra
Stop-KnownContainers

if ($Prune) {
    Cleanup-Networks
}

# Final cleanup for aggressive mode
if ($Aggressive -and (Test-Command 'docker')) {
    Write-Host '-> Aggressive cleanup mode...' -ForegroundColor Yellow
    try {
        & docker system prune -f 2>$null | Out-Null
        Write-Host "  ✓ Docker system pruned" -ForegroundColor Green
    } catch {}
}

Write-Host ""
Write-Host "="*60 -ForegroundColor Yellow
Write-Host "🏁 SwAIvyn shutdown complete!" -ForegroundColor Green
Write-Host "="*60 -ForegroundColor Yellow
Write-Host ""
Write-Host "💡 All services stopped:" -ForegroundColor Cyan
Write-Host "• Frontend (React/Vite)" -ForegroundColor White
Write-Host "• Backend BFF (FastAPI)" -ForegroundColor White  
Write-Host "• Orchestrator (Temporal worker)" -ForegroundColor White
Write-Host "• All Docker containers (databases, TTS, etc.)" -ForegroundColor White
Write-Host ""
Write-Host "To restart everything, run: .\dev-run.ps1" -ForegroundColor Yellow
Write-Host ""