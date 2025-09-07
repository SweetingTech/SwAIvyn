# Seed the default chat workflow into the BFF database

param([switch]$Yes)

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

Import-DotEnv

if (-not $Yes) {
  $resp = Read-Host 'This will upsert the default chat workflow. Continue? (y/N)'
  if ($resp.ToLower() -notin @('y','yes')) { Write-Host 'Aborted.'; exit 1 }
}

Ensure-Venv
& .\.venv\Scripts\Activate.ps1
Install-RequirementsIfNeeded

if (-not $env:DATABASE_URL -and $env:POSTGRES_PASSWORD) {
  $env:DATABASE_URL = "postgresql+asyncpg://postgres:$($env:POSTGRES_PASSWORD)@localhost:5432/swai"
}

Write-Host 'Seeding default chat workflow...' -ForegroundColor Green
python -m app.seed_workflows
Write-Host 'Done.' -ForegroundColor Green

