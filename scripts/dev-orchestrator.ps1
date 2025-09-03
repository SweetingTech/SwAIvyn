# Runs the Orchestrator (Temporal worker) on host
param(
    [int]$ActivityThreads = 32,
    [string]$Python = 'py'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/.."
$orcDir = Join-Path $root 'Services/orchestrator'
Set-Location $orcDir

function Resolve-PythonExe {
    param([string]$Preferred = $null)
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
    param([string]$Path = (Join-Path $root '.env'))
    if (-not (Test-Path $Path)) { return }
    Get-Content -Raw $Path | ForEach-Object {
        $_ -split "`n" | ForEach-Object {
            if ($_ -match '^(\s*#|\s*$)') { return }
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

Import-DotEnv

function Ensure-Venv {
    $venvPy = Join-Path '.venv' 'Scripts/python.exe'
    if (-not (Test-Path $venvPy)) {
        Write-Host 'Creating Python virtual environment...' -ForegroundColor Cyan
        $py = Resolve-PythonExe -Preferred $Python
        & $py.Exe @($py.PreArgs) -m venv .venv
    } else {
        Write-Host 'Using existing virtual environment' -ForegroundColor DarkGray
    }
}

function Install-RequirementsIfNeeded {
    param([string]$ReqFile = 'requirements.txt')
    $hashFile = '.venv/.req.hash'
    $needInstall = $true
    if (Test-Path $ReqFile) {
        $hash = (Get-FileHash -Path $ReqFile -Algorithm SHA256).Hash
        if (Test-Path $hashFile) {
            $prev = (Get-Content -Raw $hashFile -ErrorAction SilentlyContinue).Trim()
            if ($prev -eq $hash) { $needInstall = $false }
        }
        if ($needInstall) {
            Write-Host 'Installing Orchestrator requirements...' -ForegroundColor Cyan
            & python -m pip install -r $ReqFile | Write-Host
            Set-Content -Path $hashFile -Value $hash -Encoding ASCII -NoNewline
        } else {
            Write-Host 'Requirements unchanged; skipping pip install' -ForegroundColor DarkGray
        }
    }
}

# Ensure env ready
Ensure-Venv
& .\.venv\Scripts\Activate.ps1
Install-RequirementsIfNeeded

# Env wiring
if (-not $env:TEMPORAL_HOST) { $env:TEMPORAL_HOST = 'localhost:7233' }
$env:ACTIVITY_THREADS = "$ActivityThreads"
if (-not $env:TTS_ADAPTER_URL) { $env:TTS_ADAPTER_URL = 'http://localhost:8082' }
# Optional local tts fallback
if (-not $env:FISHSPEECH_URL) { $env:FISHSPEECH_URL = 'http://localhost:8081' }

Write-Host "Running Orchestrator worker (threads=$ActivityThreads)..." -ForegroundColor Green
python -m app.worker
