<#
.SYNOPSIS
  Pulls/builds the Docker images used by SwAIvyn and exports them as tar archives for offline use.
.DESCRIPTION
  Downloads all required container images (and optional extras) so they can be distributed offline.
  Local images such as swai/tts-proxy:local are built automatically before being saved.
.PARAMETER OutputDir
  Destination directory for the exported tar files. Defaults to ../offline-images relative to this script.
.PARAMETER Force
  Overwrites existing tar archives instead of skipping them.
.PARAMETER IncludeOptional
  Includes optional images such as the orchestrator, ElevenLabs adapter, and Weaviate module sidecars.
.PARAMETER IncludeCudaBase
  Also pulls/saves the CUDA base image used during GPU prerequisite checks.
.EXAMPLE
  ./save-offline-images.ps1
.EXAMPLE
  ./save-offline-images.ps1 -IncludeOptional -IncludeCudaBase -OutputDir 'D:/archives'
#>
[CmdletBinding()]
param(
    [string]$OutputDir = (Join-Path (Split-Path -Parent $PSScriptRoot) 'offline-images'),
    [switch]$Force,
    [switch]$IncludeOptional,
    [switch]$IncludeCudaBase
)

$ErrorActionPreference = 'Stop'

function Test-Command {
    param([string]$Name)
    return [bool](Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

function Test-ImageExists {
    param([string]$Reference)
    $id = & docker images -q $Reference 2>$null
    return [bool]$id
}

function Run-Docker {
    param([string[]]$Args)
    Write-Host ("docker {0}" -f ($Args -join ' ')) -ForegroundColor DarkGray
    & docker @Args
    if ($LASTEXITCODE -ne 0) {
        throw "docker command failed: $($Args -join ' ')"
    }
}

if (-not (Test-Command 'docker')) {
    throw 'Docker CLI not found. Install Docker Desktop or ensure docker is on PATH.'
}

$rootDir = Split-Path -Parent $PSScriptRoot
$OutputDir = Resolve-Path -LiteralPath (New-Item -ItemType Directory -Force -Path $OutputDir).FullName

$baseImages = @(
    'traefik:v3.1',
    'swai/fish-speech:cuda',
    'onerahmet/openai-whisper-asr-webservice:latest',
    'qdrant/qdrant:latest',
    'neo4j:5',
    'postgres:16',
    'temporalio/server:1.23.1',
    'temporalio/admin-tools:1.23.1',
    'semitechnologies/weaviate:1.27.0'
)

$moduleImages = @(
    'semitechnologies/multi2vec-clip:sentence-transformers-clip-ViT-B-32-multilingual-v1',
    'semitechnologies/qna-transformers:distilbert-base-uncased-distilled-squad',
    'semitechnologies/sum-transformers:facebook-bart-large-cnn-1.0.0',
    'semitechnologies/text-spellcheck-model:pyspellchecker-en',
    'semitechnologies/reranker-transformers:cross-encoder-ms-marco-MiniLM-L-6-v2'
)

$remoteImages = @()
$remoteImages += $baseImages
if ($IncludeOptional) {
    $remoteImages += $moduleImages
}
if ($IncludeCudaBase) {
    $remoteImages += 'nvidia/cuda:12.2.0-base-ubuntu22.04'
}

$buildTargets = @(
    @{ Tag = 'swai/tts-proxy:local'; Context = $rootDir; Dockerfile = 'speech/TTS/openaudio-s1-mini/Dockerfile' }
)

if ($IncludeOptional) {
    $orchPath = Join-Path $rootDir 'Services/orchestrator/Dockerfile'
    if (Test-Path $orchPath) {
        $buildTargets += @{ Tag = 'swai/orchestrator:local'; Context = Join-Path $rootDir 'Services/orchestrator'; Dockerfile = 'Dockerfile' }
    }
    $adapterPath = Join-Path $rootDir 'Services/tts_11labs_adapter/Dockerfile'
    if (Test-Path $adapterPath) {
        $buildTargets += @{ Tag = 'swai/tts-11labs-adapter:local'; Context = Join-Path $rootDir 'Services/tts_11labs_adapter'; Dockerfile = 'Dockerfile' }
    }
}

# Build local images
foreach ($item in $buildTargets) {
    $tag = $item.Tag
    $ctx = $item.Context
    $df = $item.Dockerfile
    if (-not (Test-ImageExists $tag) -or $Force) {
        Write-Host ("Building local image {0}" -f $tag) -ForegroundColor Cyan
        $args = @('build','-t', $tag)
        if ($df -and $df -ne 'Dockerfile') { $args += @('-f', $df) }
        $args += '.'
        Push-Location $ctx
        try {
            Run-Docker -Args $args
        } finally {
            Pop-Location
        }
    } else {
        Write-Host ("Image {0} already exists locally; skipping build." -f $tag) -ForegroundColor DarkGray
    }
}

# Pull remote images
foreach ($ref in $remoteImages) {
    if (-not (Test-ImageExists $ref) -or $Force) {
        Write-Host ("Pulling {0}" -f $ref) -ForegroundColor Cyan
        Run-Docker -Args @('pull', $ref)
    } else {
        Write-Host ("Image {0} already exists locally; skipping pull." -f $ref) -ForegroundColor DarkGray
    }
}

$allImages = @($remoteImages)
$allImages += ($buildTargets | ForEach-Object { $_.Tag })
$allImages = $allImages | Sort-Object -Unique

foreach ($ref in $allImages) {
    $safeName = ($ref -replace '[^A-Za-z0-9_.-]', '_') + '.tar'
    $dest = Join-Path $OutputDir $safeName
    if ((Test-Path $dest) -and -not $Force) {
        Write-Host ("Skipping save for {0} because {1} exists (use -Force to overwrite)." -f $ref, $dest) -ForegroundColor DarkGray
        continue
    }
    Write-Host ("Saving {0} -> {1}" -f $ref, $dest) -ForegroundColor Green
    Run-Docker -Args @('save','-o', $dest, $ref)
}

Write-Host "Offline image export complete." -ForegroundColor Green
