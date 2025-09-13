param(
  [switch]$All,
  [string[]]$Target = @(),
  [switch]$NoCache,
  [switch]$Pull,
  [switch]$List
)

$ErrorActionPreference = 'Stop'

function Test-Command { param([string]$Name) try { Get-Command $Name -ErrorAction Stop | Out-Null; return $true } catch { return $false } }
if (-not (Test-Command 'docker')) { throw 'Docker CLI not found. Please install/start Docker Desktop.' }

$root = (Resolve-Path "$PSScriptRoot/..\..").Path  # repo root
$appRoot = Join-Path $root 'SwAIvyn'

# Define buildable targets and pullable images
$targets = [ordered]@{
  # Local Dockerfiles (build)
  'tts-proxy'            = @{ kind='local'; context=$appRoot; dockerfile='speech/TTS/openaudio-s1-mini/Dockerfile'; tag='swai/tts-proxy:local' }
  'tts-11labs-adapter'   = @{ kind='local'; context=(Join-Path $root 'SwAIvyn/Services/tts_11labs_adapter'); dockerfile='Dockerfile'; tag='swai/tts-11labs-adapter:local' }
  'orchestrator'         = @{ kind='local'; context=(Join-Path $root 'SwAIvyn/Services/orchestrator'); dockerfile='Dockerfile'; tag='swai/orchestrator:local' }
  'bff'                  = @{ kind='local'; context=(Join-Path $root 'SwAIvyn/Services/bff'); dockerfile='Dockerfile'; tag='swai/bff:local' }
  'frontend'             = @{ kind='local'; context=(Join-Path $root 'SwAIvyn'); dockerfile='frontend/Dockerfile'; tag='swai/frontend:local' }
  'workers'              = @{ kind='local'; context=(Join-Path $root 'SwAIvyn_Workers'); dockerfile='Dockerfile'; tag='swai/workers:local' }
  'workers-orchestrator' = @{ kind='local'; context=(Join-Path $root 'SwAIvyn_Workers/orchestrator'); dockerfile='Dockerfile'; tag='swai/workers-orchestrator:local' }

  # Remote images (pull)
  'traefik'           = @{ kind='remote'; image='traefik:v3.1' }
  'qdrant'            = @{ kind='remote'; image='qdrant/qdrant:latest' }
  'neo4j'             = @{ kind='remote'; image='neo4j:5' }
  'postgres'          = @{ kind='remote'; image='postgres:16' }
  'kanban-postgres'   = @{ kind='remote'; image='postgres:15' }
  'temporal'          = @{ kind='remote'; image='temporalio/auto-setup:1.23' }
  'stt'               = @{ kind='remote'; image='onerahmet/openai-whisper-asr-webservice:latest' }
  'wekan'             = @{ kind='remote'; image='wekanteam/wekan:latest' }
  'mongo'             = @{ kind='remote'; image='mongo:4.4' }
  'vllm'              = @{ kind='remote'; image='vllm/vllm-openai:v0.6.3.post1' }
}

$groups = [ordered]@{
  'tts'     = @('tts-proxy','tts-11labs-adapter','stt')
  'infra'   = @('postgres','qdrant','neo4j','temporal')
  'kanban'  = @('wekan','mongo','kanban-postgres')
  'app'     = @('bff','frontend','orchestrator','workers','workers-orchestrator')
  'core'    = @('traefik','tts-proxy','tts-11labs-adapter')
  'all'     = $targets.Keys
}

function Print-Targets {
  Write-Host 'Build targets:' -ForegroundColor Cyan
  foreach ($k in $targets.Keys) {
    $t = $targets[$k]
    $desc = if ($t.kind -eq 'local') { "build -> $($t.tag)" } else { "pull -> $($t.image)" }
    Write-Host (" - {0}: {1}" -f $k, $desc) -ForegroundColor DarkGray
  }
  Write-Host ''
  Write-Host 'Groups:' -ForegroundColor Cyan
  foreach ($g in $groups.Keys) {
    Write-Host (" - {0}: {1}" -f $g, ($groups[$g] -join ', ')) -ForegroundColor DarkGray
  }
}

if ($List) { Print-Targets; exit 0 }

function Expand-Selection([string[]]$sel, [switch]$selectAll) {
  # Work with PowerShell arrays to avoid generic collection type issues
  $out = @()
  if ($selectAll -or -not $sel -or $sel.Count -eq 0) {
    $out += @($targets.Keys | ForEach-Object { [string]$_ })
  } else {
    foreach ($name in $sel) {
      if ($groups.ContainsKey($name)) { $out += @($groups[$name]) ; continue }
      if ($targets.ContainsKey($name)) { $out += @([string]$name) ; continue }
      Write-Warning ("Unknown target or group: {0}" -f $name)
    }
  }
  # de-dup while preserving order and return as a .NET List[string]
  $seen = @{}
  $uniq = New-Object System.Collections.Generic.List[string]
  foreach ($n in $out) {
    $s = [string]$n
    if (-not $seen[$s]) { [void]$uniq.Add($s); $seen[$s] = $true }
  }
  return $uniq
}

function Build-Local($ctx, $df, $tag) {
  if (-not (Test-Path (Join-Path $ctx $df))) {
    Write-Warning ("Dockerfile not found for tag {0}: {1}" -f $tag, (Join-Path $ctx $df))
    return
  }
  Push-Location $ctx
  try {
    $args = @('build','-t', $tag)
    if ($df -and $df -ne 'Dockerfile') { $args += @('-f', $df) }
    $args += '.'
    if ($NoCache) { $args += '--no-cache' }
    if ($Pull)    { $args += '--pull' }
    Write-Host ("docker {0}" -f ($args -join ' ')) -ForegroundColor DarkGray
    & docker @args
    if ($LASTEXITCODE -ne 0) { throw ("docker build failed for {0}" -f $tag) }
  } finally { Pop-Location }
}

function Pull-Remote($image) {
  Write-Host ("docker pull {0}" -f $image) -ForegroundColor DarkGray
  & docker pull $image
  if ($LASTEXITCODE -ne 0) { throw ("docker pull failed for {0}" -f $image) }
}

$selection = Expand-Selection -sel $Target -selectAll:$All
if (-not $selection -or $selection.Count -eq 0) { Write-Warning 'Nothing to build.'; exit 0 }

Write-Host ("Building targets: {0}" -f ($selection -join ', ')) -ForegroundColor Cyan

foreach ($name in $selection) {
  $spec = $targets[$name]
  if (-not $spec) { continue }
  if ($spec.kind -eq 'local') {
    Build-Local -ctx $spec.context -df $spec.dockerfile -tag $spec.tag
  } elseif ($spec.kind -eq 'remote') {
    if ($Pull) { Pull-Remote -image $spec.image } else { Write-Host ("Skipping pull for {0} (use -Pull)" -f $spec.image) -ForegroundColor DarkGray }
  }
}

Write-Host 'Build complete.' -ForegroundColor Green
