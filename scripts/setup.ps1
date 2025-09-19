param(
    [string]$StackName = $env:STACK_NAME
)

# Idempotent Temporal database bootstrap using containers
function Setup-TemporalSchema {
    param(
        [string]$StackName = $env:STACK_NAME,
        [string]$PostgresHost = 'swai-db',
        [int]$PostgresPort = 5432
    )

    if (-not $env:POSTGRES_PASSWORD) {
        throw "POSTGRES_PASSWORD must be set (from .env) before bootstrapping Temporal schema."
    }

    Write-Host "[setup] Ensuring Temporal databases exist on Postgres..." -ForegroundColor Cyan

    # Create databases temporal and temporal_visibility if they don't exist
    $networkName = "${StackName}_default"
    $createDbScript = @"
psql -h $PostgresHost -U postgres -p $PostgresPort -tAc "SELECT 1 FROM pg_database WHERE datname='temporal'" | grep -q 1 || psql -h $PostgresHost -U postgres -p $PostgresPort -c "CREATE DATABASE temporal";
psql -h $PostgresHost -U postgres -p $PostgresPort -tAc "SELECT 1 FROM pg_database WHERE datname='temporal_visibility'" | grep -q 1 || psql -h $PostgresHost -U postgres -p $PostgresPort -c "CREATE DATABASE temporal_visibility";
"@
    $pgCmd = @('run','--rm','--network',$networkName,'-e',"PGPASSWORD=$($env:POSTGRES_PASSWORD)",'postgres:16','bash','-lc',$createDbScript)
    & docker @pgCmd | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to ensure Temporal databases exist." }

    Write-Host "[setup] Applying/Updating Temporal schemas (idempotent)..." -ForegroundColor Cyan

    # Apply schema for temporal and visibility (safe to re-run)
    $schemaScript = @"
set -e
for DB in temporal temporal_visibility; do
  if [ "\$DB" = "temporal" ]; then
    DIR=/etc/temporal/schema/postgresql/v12/temporal/versioned
  else
    DIR=/etc/temporal/schema/postgresql/v12/visibility/versioned
  fi
  temporal-sql-tool \
    --plugin postgres \
    --ep '$PostgresHost' \
    -u 'postgres' \
    -p '$PostgresPort' \
    --db "\$DB" setup-schema -v 0.0 || true
  temporal-sql-tool \
    --plugin postgres \
    --ep '$PostgresHost' \
    -u 'postgres' \
    -p '$PostgresPort' \
    --db "\$DB" update-schema -d "\$DIR"
  echo "Schema updated for \$DB"
done
"@

    $toolCmd = @('run','--rm','--network',$networkName,'-e',"POSTGRES_PWD=$($env:POSTGRES_PASSWORD)",'temporalio/admin-tools:1.23','bash','-lc', $schemaScript)
    & docker @toolCmd | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to update Temporal DB schemas." }

    Write-Host "[setup] Temporal databases and schemas are ready." -ForegroundColor Green
}

