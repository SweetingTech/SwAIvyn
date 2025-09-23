param(
  [string]$StackName = 'swaivyn',
  [switch]$DownCompose = $true,
  [switch]$Aggressive,
  [switch]$Prune
)

$ErrorActionPreference = 'Stop'

function Test-Command { param([string]$Name) try { Get-Command $Name -ErrorAction Stop | Out-Null; return $true } catch { return $false } }

Write-Host '== Stopping SwAIvyn dev environment =='

$root = Resolve-Path "$PSScriptRoot/.."
$composeFile = Join-Path $root 'docker-compose.yml'
$stateFile = Join-Path $PSScriptRoot '.dev-state.json'

function Get-ListeningPids {
  param([int[]]$Ports)
  $pids = @()
  try {
    $net = netstat -ano | Select-String -Pattern 'LISTENING'
    foreach ($port in $Ports) {
      $matchList = $net | Where-Object { $_.Line -match (":$port[\s]") }
      foreach ($m in $matchList) {
        $parts = ($m.Line -split '\s+') | Where-Object { $_ -ne '' }
        if ($parts.Length -ge 5) { $proc = [int]$parts[-1]; if ($proc -gt 0) { $pids += $proc } }
      }
    }
  } catch {}
  return ($pids | Select-Object -Unique)
}

function Stop-HostDevProcs {
  Write-Host '-> Stopping host dev processes (frontend/backend)...'
  $ports = @(5173, 5000)
  $allow = @('node','python','py','uvicorn','cmd','powershell')
  foreach ($procId in (Get-ListeningPids -Ports $ports)) {
    try {
      $p = Get-Process -Id $procId -ErrorAction Stop
      if ($allow -contains $p.Name.ToLower()) {
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
        Write-Host ("  * Stopped PID {0} ({1})" -f $procId, $p.Name)
      }
    } catch {}
  }
}

function Stop-FromStateFile {
  if (-not (Test-Path $stateFile)) { return }
  try {
    $state = Get-Content -Raw $stateFile | ConvertFrom-Json
    if ($state -and $state.pids) {
      foreach ($k in @('frontend','bff','orchestrator')) {
        $procId = $state.pids.$k
        if ($procId) {
          try { $p = Get-Process -Id $procId -ErrorAction Stop; Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue; Write-Host ("  * Stopped {0} (PID {1})" -f $k, $procId) } catch {}
        }
      }
    }
  } catch {}
  try { Remove-Item $stateFile -Force -ErrorAction SilentlyContinue | Out-Null } catch {}
}

function Remove-SwarmStack {
  param([string]$Name)
  if (-not (Test-Command 'docker')) { return }
  try {
    Write-Host ("-> Removing Docker Swarm stack '{0}'" -f $Name)
    & docker stack rm $Name 2>$null | Out-Null
    # Wait until services are gone (up to ~60s)
    1..60 | ForEach-Object {
      try {
        $svc = (& docker stack services $Name 2>$null)
        if (-not $svc -or $svc.Count -eq 0) { throw 'gone' }
      } catch { break }
      Start-Sleep -Seconds 1
    }
  } catch {}
}

function Stop-ComposeInfra {
  if (-not (Test-Path $composeFile)) { return }
  if (-not (Test-Command 'docker')) { return }
  try {
    if ($DownCompose) {
      Write-Host '-> docker compose down (infra)'
      & docker compose -f $composeFile down --remove-orphans
    } else {
      Write-Host '-> docker compose stop (infra)'
      & docker compose -f $composeFile stop swai-db temporal qdrant neo4j stt tts-11labs-adapter
    }
  } catch {}
}

function Stop-KnownContainers {
  if (-not (Test-Command 'docker')) { return }
  foreach ($name in @('vllm-chat','vllm-embed','swaivyn-tts-1')) {
    try { & docker stop $name 2>$null | Out-Null } catch {}
    try { if ($DownCompose) { & docker rm $name 2>$null | Out-Null } } catch {}
  }
}

Stop-HostDevProcs
Stop-FromStateFile
Remove-SwarmStack -Name $StackName
Stop-ComposeInfra
Stop-KnownContainers

if ($Prune -and (Test-Command 'docker')) {
  try { Write-Host '-> docker network prune -f'; & docker network prune -f 2>$null | Out-Null } catch {}
}

Write-Host 'All stopped.' -ForegroundColor Green
