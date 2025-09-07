# Runs the BFF (FastAPI) on host with hot reload, wired to local infra
param(
    [int]$Port = 5100,
    [string]$Python = 'py'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/.."
$bffDir = Join-Path $root 'Services/bff'
Set-Location $bffDir

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
            Write-Host 'Installing BFF requirements...' -ForegroundColor Cyan
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

# Infer required envs from .env / defaults
$pgPwd = if ($env:POSTGRES_PASSWORD) { $env:POSTGRES_PASSWORD } else { '' }
if (-not $pgPwd) { Write-Warning 'POSTGRES_PASSWORD not set; set it in .env to enable DB.' }

$uploadsDir = Join-Path $root 'wwwroot/uploads'
New-Item -ItemType Directory -Force -Path $uploadsDir | Out-Null

if ($pgPwd) { $env:DATABASE_URL = "postgresql+asyncpg://postgres:$pgPwd@localhost:5432/swai" } else { $env:DATABASE_URL = '' }
if (-not $env:TEMPORAL_HOST) { $env:TEMPORAL_HOST = 'localhost:7233' }
if (-not $env:QDRANT_URL) { $env:QDRANT_URL = 'http://localhost:6333' }
if (-not $env:NEO4J_URL) { $env:NEO4J_URL = 'bolt://localhost:7687' }

# Optional local LLMs
if (-not $env:OLLAMA_HOST) { $env:OLLAMA_HOST = 'http://localhost:11434' }
if (-not $env:LMSTUDIO_HOST) { $env:LMSTUDIO_HOST = 'http://localhost:1234' }

# TTS endpoints (prefer 11labs adapter; heavy local tts is optional)
if (-not $env:TTS_ADAPTER_URL) { $env:TTS_ADAPTER_URL = 'http://localhost:8082' }
if (-not $env:FISHSPEECH_URL) { $env:FISHSPEECH_URL = 'http://localhost:8081' }

$env:UPLOADS_DIR = $uploadsDir

Write-Host "Running BFF on http://localhost:$Port (reload on changes)..." -ForegroundColor Green
python -m uvicorn app.main:app --host 0.0.0.0 --port $Port --reload
