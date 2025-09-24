# Force (re)seed Sam and Sherlock characters from frontend/AI into the BFF DB.
# - Loads .env for POSTGRES_PASSWORD
# - Creates/uses BFF venv
# - Builds DATABASE_URL if missing
# - Copies images to uploads and upserts characters as global (user_id=NULL)

param(
  [switch]$Yes,
  [string]$AiSourceDir = ''
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/.."
$bffDir = Join-Path $root 'Services/bff'
Set-Location $bffDir

function Import-DotEnv {
  param([string]$Path = (Join-Path $root '.env'))
  if (-not (Test-Path $Path)) { return }
  (Get-Content -Raw $Path) -split "`n" | ForEach-Object {
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

function Ensure-Venv {
  $venvPy = Join-Path '.venv' 'Scripts/python.exe'
  if (-not (Test-Path $venvPy)) {
    Write-Host 'Creating Python virtual environment for BFF...' -ForegroundColor Cyan
    & py -3 -m venv .venv
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

# 1) Load env
Import-DotEnv

# 2) Safety prompt
if (-not $Yes) {
  $resp = Read-Host 'This will upsert global characters (Sam, Sherlock) and copy images into uploads. Continue? (y/N)'
  if ($resp.ToLower() -notin @('y','yes')) { Write-Host 'Aborted.'; exit 1 }
}

# 3) Ensure venv/requirements
Ensure-Venv
& .\.venv\Scripts\Activate.ps1
Install-RequirementsIfNeeded

# 4) Build DATABASE_URL from POSTGRES_PASSWORD if missing (matches dev-bff.ps1)
if (-not $env:DATABASE_URL -and $env:POSTGRES_PASSWORD) {
  $env:DATABASE_URL = "postgresql+asyncpg://postgres:$($env:POSTGRES_PASSWORD)@localhost:5432/swai"
}

if ($AiSourceDir) { $env:AI_SOURCE_DIR = $AiSourceDir }

Write-Host 'Seeding characters from frontend/AI...' -ForegroundColor Green
python -m app.seed_characters_from_frontend

Write-Host 'Done.' -ForegroundColor Green

