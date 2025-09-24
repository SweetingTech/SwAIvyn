<#!
.SYNOPSIS
  Purge conversations/messages from the dev database.

.DESCRIPTION
  Deletes rows from the Conversations and Messages tables. You can target:
    - All conversations (-All)
    - A specific user (-UserId <id>)
    - Legacy-only rows with missing/empty/placeholder owners (-LegacyOnly)

.EXAMPLE
  # Delete legacy/orphaned conversations only
  ./scripts/dev-purge-conversations.ps1 -LegacyOnly

.EXAMPLE
  # Delete all admin's conversations
  ./scripts/dev-purge-conversations.ps1 -UserId admin

.EXAMPLE
  # Danger: delete every conversation
  ./scripts/dev-purge-conversations.ps1 -All
#>

param(
  [switch] $All,
  [string] $UserId,
  [switch] $LegacyOnly
)

$ErrorActionPreference = 'Stop'

function Import-DotEnv {
  param([string]$Path)
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

$root = Resolve-Path "$PSScriptRoot/.."
Import-DotEnv (Join-Path $root '.env')

# Build DATABASE_URL if not already present (matches dev-bff.ps1 behavior)
if (-not $Env:DATABASE_URL) {
  if ($Env:POSTGRES_PASSWORD) {
    $Env:DATABASE_URL = "postgresql+asyncpg://postgres:$($Env:POSTGRES_PASSWORD)@localhost:5432/swai"
  }
}

if (-not $Env:DATABASE_URL) {
  throw 'DATABASE_URL is not set. Ensure POSTGRES_PASSWORD is in .env or set DATABASE_URL explicitly.'
}

# Locate the BFF virtual environment python (fallback to system python)
$bffDir = Join-Path $root 'Services/bff'
$venvPy = Join-Path $bffDir '.venv/Scripts/python.exe'
if (-not (Test-Path $venvPy)) {
  Write-Warning 'BFF virtualenv not found; falling back to system Python. Ensure SQLAlchemy + asyncpg are installed.'
  $venvPy = 'python'
}

# Determine purge mode
$mode = if ($All) { 'all' } elseif ($UserId) { 'user' } elseif ($LegacyOnly) { 'legacy' } else { 'legacy' }
if ($mode -eq 'all') {
  $confirm = Read-Host 'This will DELETE ALL conversations. Type YES to continue'
  if ($confirm -ne 'YES') { Write-Host 'Aborted.' -ForegroundColor Yellow; exit 1 }
}

$py = @"
import os, asyncio
from sqlalchemy.ext.asyncio import create_async_engine
from sqlalchemy import text

DATABASE_URL = os.environ.get('DATABASE_URL')
MODE = os.environ.get('PURGE_MODE')
USER_ID = os.environ.get('PURGE_USER')

async def main():
    if not DATABASE_URL:
        raise SystemExit('DATABASE_URL not set')
    engine = create_async_engine(DATABASE_URL, echo=False, future=True)
    where_sql = ''
    params = {}
    if MODE == 'user' and USER_ID:
        where_sql = 'WHERE user_id = :uid'
        params['uid'] = USER_ID
    elif MODE == 'legacy':
        where_sql = (
            "WHERE user_id IS NULL OR user_id = '' "
            "OR user_id = '00000000-0000-0000-0000-000000000001' "
            "OR NOT EXISTS (SELECT 1 FROM users u WHERE u.id = conversations.user_id)"
        )
    # else: MODE == 'all' -> empty WHERE

    async with engine.begin() as conn:
        # Count target conversations
        res = await conn.execute(text(f"SELECT COUNT(*) FROM conversations {where_sql}"), params)
        row = res.first()
        conv_count = int(row[0]) if row else 0

        # Delete messages then conversations
        await conn.execute(text(
            f"DELETE FROM messages WHERE conversation_id IN (SELECT id FROM conversations {where_sql})"
        ), params)
        r2 = await conn.execute(text(f"DELETE FROM conversations {where_sql}"), params)

    print({ 'success': True, 'purgeMode': MODE, 'userId': USER_ID, 'targetConversations': conv_count })

asyncio.run(main())
"@

$env:PURGE_MODE = $mode
$env:PURGE_USER = $UserId

Write-Host "Purging conversations... (mode=$mode, user=$UserId)" -ForegroundColor Cyan

# Write the Python helper to a temp file to avoid quoting issues
$tmpPy = Join-Path $env:TEMP ("purge_conversations_" + [guid]::NewGuid().ToString('N') + ".py")
Set-Content -Path $tmpPy -Value $py -Encoding UTF8
try {
  & $venvPy $tmpPy
} finally {
  Remove-Item $tmpPy -ErrorAction SilentlyContinue
}
