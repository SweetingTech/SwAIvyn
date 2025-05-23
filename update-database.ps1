# PowerShell script to update database schema
$connectionString = "Data Source=C:\Users\djay\Desktop\data\swai-vyn.db"

# Load SQLite assembly from the backend project
Add-Type -Path "C:\Users\djay\Desktop\SwAIvyn\backend\bin\Debug\net8.0\win-x64\Microsoft.Data.Sqlite.dll"

try {
    $connection = New-Object Microsoft.Data.Sqlite.SqliteConnection($connectionString)
    $connection.Open()
    Write-Host "Connected to database successfully."

    # Add missing columns to Avatars table
    $alterCommands = @(
        "ALTER TABLE Avatars ADD COLUMN AlternateGreetings TEXT;",
        "ALTER TABLE Avatars ADD COLUMN CharacterVersion TEXT;",
        "ALTER TABLE Avatars ADD COLUMN Creator TEXT;",
        "ALTER TABLE Avatars ADD COLUMN CreatorNotes TEXT;",
        "ALTER TABLE Avatars ADD COLUMN Extensions TEXT;",
        "ALTER TABLE Avatars ADD COLUMN FirstMessage TEXT;",
        "ALTER TABLE Avatars ADD COLUMN MessageExample TEXT;",
        "ALTER TABLE Avatars ADD COLUMN PostHistoryInstructions TEXT;",
        "ALTER TABLE Avatars ADD COLUMN Scenario TEXT;",
        "ALTER TABLE Avatars ADD COLUMN SystemPrompt TEXT;",
        "ALTER TABLE Avatars ADD COLUMN Tags TEXT;",
        "ALTER TABLE Avatars ADD COLUMN Talkativeness REAL DEFAULT 0.5;",
        "ALTER TABLE Avatars ADD COLUMN YamlProfile TEXT;"
    )

    foreach ($command in $alterCommands) {
        try {
            $cmd = $connection.CreateCommand()
            $cmd.CommandText = $command
            $cmd.ExecuteNonQuery()
            Write-Host "Executed: $command"
        }
        catch {
            Write-Host "Column might already exist or error: $($_.Exception.Message)"
        }
    }

    $connection.Close()
    Write-Host "Database schema updated successfully!"
}
catch {
    Write-Host "Error: $($_.Exception.Message)"
}
