# Builds Docker images defined in docker-compose.yml without starting containers
param(
    [switch]$Up = $false,
    [switch]$NoCache = $false,
    [switch]$Pull = $false,
    [string[]]$Services = @(),
    [string]$ComposeFile = "$PSScriptRoot/../docker-compose.yml"
)

$ErrorActionPreference = 'Stop'

function Resolve-ComposeCmd {
    if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
        return @{ Name = 'docker-compose'; Args = @('-f', $ComposeFile) }
    }
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        return @{ Name = 'docker'; Args = @('compose','-f', $ComposeFile) }
    }
    throw 'Docker is not installed or not in PATH.'
}

function Ensure-ComposeFile([string]$Path) {
    $full = Resolve-Path -LiteralPath $Path -ErrorAction SilentlyContinue
    if (-not $full) {
        throw "Compose file not found at '$Path'"
    }
    return $full.Path
}

try {
    # Basic sanity check that Docker daemon is up
    docker version | Out-Null
} catch {
    Write-Error 'Docker daemon not reachable. Start Docker Desktop and retry.'
    exit 1
}

$composePath = Ensure-ComposeFile -Path $ComposeFile
$cmd = Resolve-ComposeCmd

Write-Host "Using compose file: $composePath" -ForegroundColor DarkGray

# Build arguments
$buildArgs = @($cmd.Args + @('build'))
if ($NoCache) { $buildArgs += '--no-cache' }
if ($Pull)    { $buildArgs += '--pull' }
if ($Services -and $Services.Count -gt 0) { $buildArgs += $Services }

Write-Host 'Building Docker images via Docker Compose...' -ForegroundColor Cyan
& $cmd.Name @buildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Compose build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

if ($Up) {
    Write-Host 'Starting containers (up -d)...' -ForegroundColor Cyan
    $upArgs = @($cmd.Args + @('up','-d'))
    if ($Services -and $Services.Count -gt 0) { $upArgs += $Services }
    & $cmd.Name @upArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Compose up failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}

Write-Host 'Done.' -ForegroundColor Green

# Hints: paths used by compose build contexts
Write-Host 'Notes:' -ForegroundColor DarkGray
Write-Host ' - Orchestrator Dockerfile:    Services/orchestrator/Dockerfile' -ForegroundColor DarkGray
Write-Host ' - TTS (openaudio) Dockerfile: speech/TTS/openaudio-s1-mini/Dockerfile' -ForegroundColor DarkGray
Write-Host ' - 11labs adapter Dockerfile:  Services/tts_11labs_adapter/Dockerfile' -ForegroundColor DarkGray

