# PowerShell script to rebuild the database using bootdb.sql
param(
    [string]$DatabasePath = "data\swai-vyn.db",
    [string]$SqlScriptPath = "bootdb.sql"
)

Write-Host "Rebuilding database using $SqlScriptPath..." -ForegroundColor Cyan

try {
    # Check if the SQL script exists
    if (-not (Test-Path $SqlScriptPath)) {
        Write-Host "Error: SQL script file '$SqlScriptPath' not found!" -ForegroundColor Red
        exit 1
    }

    # Read the SQL script content
    $sqlContent = Get-Content $SqlScriptPath -Raw
    Write-Host "SQL script loaded successfully" -ForegroundColor Green

    # Load required assemblies
    Add-Type -AssemblyName System.Data
    
    # Try to load Microsoft.Data.Sqlite first (preferred)
    try {
        Add-Type -Path "Microsoft.Data.Sqlite.dll" -ErrorAction Stop
        $useMicrosoftSqlite = $true
        Write-Host "Using Microsoft.Data.Sqlite" -ForegroundColor Green
    }
    catch {
        # Fallback to System.Data.SQLite
        try {
            Add-Type -Path "System.Data.SQLite.dll" -ErrorAction Stop
            $useMicrosoftSqlite = $false
            Write-Host "Using System.Data.SQLite" -ForegroundColor Green
        }
        catch {
            Write-Host "Error: Neither Microsoft.Data.Sqlite.dll nor System.Data.SQLite.dll found!" -ForegroundColor Red
            Write-Host "Please ensure SQLite assemblies are available in the application directory" -ForegroundColor Yellow
            exit 1
        }
    }
    
    # Create connection string
    $connectionString = "Data Source=$DatabasePath"
    
    # Create connection
    if ($useMicrosoftSqlite) {
        $connection = New-Object Microsoft.Data.Sqlite.SqliteConnection($connectionString)
    } else {
        $connectionString += ";Version=3;"
        $connection = New-Object System.Data.SQLite.SQLiteConnection($connectionString)
    }
    
    $connection.Open()
    Write-Host "Connected to database: $DatabasePath" -ForegroundColor Green
    
    # Split SQL content by semicolons and execute each statement
    $statements = $sqlContent -split ';' | Where-Object { $_.Trim() -ne '' -and -not $_.Trim().StartsWith('--') }
    
    $command = $connection.CreateCommand()
    $statementCount = 0
    
    foreach ($statement in $statements) {
        $trimmedStatement = $statement.Trim()
        if ($trimmedStatement -ne '' -and -not $trimmedStatement.StartsWith('--')) {
            try {
                $command.CommandText = $trimmedStatement
                $result = $command.ExecuteNonQuery()
                $statementCount++
                
                # Show progress for major operations
                if ($trimmedStatement -match '^(CREATE|DROP|INSERT|ALTER)') {
                    $operation = ($trimmedStatement -split '\s+')[0].ToUpper()
                    Write-Host "Executed $operation statement ($statementCount)" -ForegroundColor Yellow
                }
            }
            catch {
                Write-Host "Warning: Failed to execute statement: $($_.Exception.Message)" -ForegroundColor Yellow
                Write-Host "Statement: $($trimmedStatement.Substring(0, [Math]::Min(100, $trimmedStatement.Length)))..." -ForegroundColor Gray
            }
        }
    }
    
    Write-Host "Executed $statementCount SQL statements" -ForegroundColor Green
    
    # Verify database tables
    $command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;"
    $reader = $command.ExecuteReader()
    
    $tables = @()
    while ($reader.Read()) {
        $tables += $reader["name"]
    }
    $reader.Close()
    
    Write-Host "Database tables created:" -ForegroundColor Cyan
    foreach ($table in $tables) {
        Write-Host "   - $table" -ForegroundColor Green
    }
    
    # Show verification results
    Write-Host ""
    Write-Host "Running verification queries..." -ForegroundColor Cyan
    
    $verificationQueries = @(
        @{ Query = "SELECT COUNT(*) as count FROM Users"; Name = "Users" },
        @{ Query = "SELECT COUNT(*) as count FROM Avatars"; Name = "Avatars" },
        @{ Query = "SELECT COUNT(*) as count FROM Conversations"; Name = "Conversations" },
        @{ Query = "SELECT COUNT(*) as count FROM Settings"; Name = "Settings" }
    )
    
    foreach ($query in $verificationQueries) {
        try {
            $command.CommandText = $query.Query
            $count = $command.ExecuteScalar()
            Write-Host "   $($query.Name): $count records" -ForegroundColor Green
        }
        catch {
            Write-Host "   $($query.Name): Could not verify" -ForegroundColor Yellow
        }
    }
    
    $connection.Close()
    Write-Host ""
    Write-Host "Database rebuild completed successfully!" -ForegroundColor Green
    Write-Host "Your SwAIvyn database has been reset to its initial state with seed data." -ForegroundColor Cyan
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($connection -and $connection.State -eq 'Open') {
        $connection.Close()
    }
    exit 1
}
