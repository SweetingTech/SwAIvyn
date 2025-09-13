# Development script for SwAIvyn - runs frontend and backend with hot reloading (without Traefik)
param (
    [switch] $FrontendOnly = $false,
    [switch] $BackendOnly = $false,
    [switch] $StartTTS = $false,
    [string] $TTSUpstream = 'http://host.docker.internal:8080',
    [string] $StackName = 'swaivyn',
    [switch]$NoCache,
    [switch]$Pull
)

# Traefik is disabled for this simple version
$UseTraefik = $false

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot
$stateFile = Join-Path $PSScriptRoot '.dev-state.json'
$pids = @{}

# --- Utility Functions ---
function Test-Command { param([string]$Name) { try { Get-Command $Name -ErrorAction Stop | Out-Null; return $true } catch { return $false } } }

function Resolve-PythonExe {
    param([string]$Preferred = 'py')
    $prefCmd = $null
    $prefArgs = @()
    if ($Preferred) {
        $parts = $Preferred -split '\s+'
        if ($parts.Count -gt 0) {
            $prefCmd = $parts[0]
            if ($parts.Count -gt 1) { $prefArgs = $parts[1..($parts.Count-1)] }
            try {
                $cmd = Get-Command $prefCmd -ErrorAction Stop
                return @{ Exe = $cmd.Path; PreArgs = $prefArgs }
            } catch {}
        }
    }
    foreach ($name in @('py','python','python3')) {
        try {
            $cmd = Get-Command $name -ErrorAction Stop
            $args = @()
            if ($name -eq 'py') { $args = @('-3') }
            return @{ Exe = $cmd.Path; PreArgs = $args }
        } catch {}
    }
    throw 'Python not found in PATH. Install Python 3 and restart your shell.'
}

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

function Wait-TcpPort {
  param([string]$HostName = 'localhost', [int]$Port, [int]$Retries = 60, [int]$DelaySec = 2)
  Write-Host "Waiting for ${HostName}:${Port} to be available..." -ForegroundColor DarkGray
  for ($i = 1; $i -le $Retries; $i++) {
    try {
      if ((Test-NetConnection -ComputerName $HostName -Port $Port -WarningAction SilentlyContinue).TcpTestSucceeded) {
        Write-Host "${HostName}:${Port} is ready" -ForegroundColor Green
        return $true
      }
    } catch {}
    if ($i -eq $Retries) {
      Write-Warning "${HostName}:${Port} did not become available after $($Retries * $DelaySec) seconds"
      return $false
    }
    Start-Sleep -Seconds $DelaySec
  }
  return $false
}

function Start-ServiceScript {
  param([string]$ScriptPath, [string]$ServiceName, [hashtable]$ExtraEnv = @{})
  
  Write-Host "Starting $ServiceName..." -ForegroundColor Green
  
  # Prepare environment variables as command arguments
  $envArgs = @()
  foreach ($key in $ExtraEnv.Keys) {
    $envArgs += "`$env:$key = '$($ExtraEnv[$key])'"
  }
  $envSetup = if ($envArgs.Count -gt 0) { ($envArgs -join '; ') + '; ' } else { '' }
  
  $fullCommand = "${envSetup}& '$ScriptPath'"
  
  try {
    $proc = Start-Process powershell -ArgumentList @(
      "-NoExit", 
      "-ExecutionPolicy", "Bypass", 
      "-Command", 
      $fullCommand
    ) -WindowStyle Normal -PassThru
    
    if ($proc) { 
      $pids[$ServiceName] = $proc.Id
      Write-Host "$ServiceName started with PID $($proc.Id)" -ForegroundColor Green
      return $proc.Id
    } else {
      Write-Warning "Failed to start $ServiceName"
      return $null
    }
  } catch {
    Write-Warning "Error starting ${ServiceName}: $($_.Exception.Message)"
    return $null
  }
}

Write-Host "Starting SwAIvyn in simple development mode (no Docker/Traefik)..." -ForegroundColor Cyan

# --- Import .env file ---
Import-DotEnv

Write-Host "`nNote: This simple version requires you to manually start Docker services if needed.`n" -ForegroundColor Yellow

# --- Prepare Environment Variables for Services ---
$serviceEnv = @{
    'TEMPORAL_HOST' = 'localhost:7233'
    'NEO4J_URL' = "bolt://localhost:7687"
    'DATABASE_URL' = if ($env:POSTGRES_PASSWORD) { "postgresql+asyncpg://postgres:$($env:POSTGRES_PASSWORD)@localhost:5432/swai" } else { '' }
    'QDRANT_URL' = 'http://localhost:6333'
    'TTS_ADAPTER_URL' = 'http://localhost:8082'
    'FISHSPEECH_URL' = 'http://localhost:8081'
    'OLLAMA_HOST' = 'http://localhost:11434'
    'LMSTUDIO_HOST' = 'http://localhost:1234'
    'LLM_MODEL' = 'llama3'
}

if (-not $BackendOnly) {
    # --- Start Frontend ---
    $frontendScript = Join-Path $PSScriptRoot 'dev-frontend.ps1'
    Start-ServiceScript -ScriptPath $frontendScript -ServiceName 'frontend' -ExtraEnv @{}
    Write-Host "Frontend dev server starting at http://0.0.0.0:5173" -ForegroundColor Green
    
    # Give frontend a moment to start
    Start-Sleep -Seconds 2
}

if (-not $FrontendOnly) {
    # --- Start BFF ---
    $bffScript = Join-Path $PSScriptRoot 'dev-bff.ps1'
    Start-ServiceScript -ScriptPath $bffScript -ServiceName 'bff' -ExtraEnv $serviceEnv
    Write-Host "BFF dev server starting at http://0.0.0.0:5000" -ForegroundColor Green
    
    # Give BFF a moment to start
    Start-Sleep -Seconds 3

    # --- Start Orchestrator ---
    $orchestratorScript = Join-Path $PSScriptRoot 'dev-orchestrator.ps1'
    Start-ServiceScript -ScriptPath $orchestratorScript -ServiceName 'orchestrator' -ExtraEnv $serviceEnv
    Write-Host "Orchestrator worker started" -ForegroundColor Green
}

# --- FINALIZATION ---
Write-Host "`n" + "="*60 -ForegroundColor Yellow
Write-Host "🚀 SwAIvyn Simple Development Environment Ready!" -ForegroundColor Green
Write-Host "="*60 -ForegroundColor Yellow

Write-Host "`n📱 APPLICATION ACCESS:" -ForegroundColor Cyan
Write-Host "Frontend: http://localhost:5173" -ForegroundColor White
Write-Host "Backend API: http://localhost:5000" -ForegroundColor White

# Also print LAN-access URLs for remote users
try {
    $lanIPs = [System.Net.Dns]::GetHostAddresses([System.Net.Dns]::GetHostName()) |
        Where-Object { $_.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork } |
        ForEach-Object { $_.IPAddressToString }
    if ($lanIPs) {
        Write-Host "`n🌍 REMOTE ACCESS (for other devices on your network):" -ForegroundColor Cyan
        foreach ($ip in $lanIPs) {
            Write-Host "Frontend: http://${ip}:5173" -ForegroundColor White
            Write-Host "Backend API: http://${ip}:5000" -ForegroundColor White
        }
    }
} catch {}

Write-Host "`n💡 TIPS:" -ForegroundColor Yellow
Write-Host "• This simple version doesn't start Docker services automatically" -ForegroundColor White
Write-Host "• Use 'dev-run.ps1' for the full Docker + Traefik experience" -ForegroundColor White
Write-Host "• Frontend supports hot reloading - changes will refresh automatically" -ForegroundColor White
Write-Host "• Check individual PowerShell windows for service-specific logs" -ForegroundColor White
Write-Host "• Press Ctrl+C in service windows to stop individual services" -ForegroundColor White

Write-Host "`n" + "="*60 -ForegroundColor Yellow

if ($pids.Count -gt 0) {
    Write-Host "`nSaving process IDs to state file for cleanup..."
    $stateObj = @{ pids = $pids }
    $stateObj | ConvertTo-Json | Set-Content -Path $stateFile -Encoding UTF8
}
