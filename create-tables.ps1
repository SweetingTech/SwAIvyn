# PowerShell script to create Avatars and Prompts tables
param(
    [string]$DatabasePath = "data\swai-vyn.db"
)

Write-Host "🔧 Creating Avatars and Prompts tables..." -ForegroundColor Cyan

try {
    # Load SQLite assembly
    Add-Type -Path "System.Data.SQLite.dll" -ErrorAction Stop
    
    # Create connection string
    $connectionString = "Data Source=$DatabasePath;Version=3;"
    
    # Create connection
    $connection = New-Object System.Data.SQLite.SQLiteConnection($connectionString)
    $connection.Open()
    
    Write-Host "✅ Connected to database: $DatabasePath" -ForegroundColor Green
    
    # SQL to create Avatars table
    $createAvatarsTable = @"
CREATE TABLE IF NOT EXISTS "Avatars" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Avatars" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "ImagePath" TEXT NOT NULL,
    "Personality" TEXT NOT NULL,
    "VoiceSettings" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "Scenario" TEXT NOT NULL,
    "FirstMessage" TEXT NOT NULL,
    "MessageExample" TEXT NOT NULL,
    "SystemPrompt" TEXT NOT NULL,
    "PostHistoryInstructions" TEXT NOT NULL,
    "AlternateGreetings" TEXT NOT NULL,
    "Tags" TEXT NOT NULL,
    "Creator" TEXT NOT NULL,
    "CreatorNotes" TEXT NOT NULL,
    "CharacterVersion" TEXT NOT NULL,
    "Talkativeness" REAL NOT NULL,
    "IsFavorite" INTEGER NOT NULL,
    "Extensions" TEXT NOT NULL,
    "YamlProfile" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "LastModified" TEXT NOT NULL,
    CONSTRAINT "FK_Avatars_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);
"@
    
    # SQL to create Prompts table
    $createPromptsTable = @"
CREATE TABLE IF NOT EXISTS "Prompts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Prompts" PRIMARY KEY,
    "AvatarId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "PromptType" TEXT NOT NULL,
    "SystemPrompt" TEXT NOT NULL,
    "InitialMessage" TEXT NOT NULL,
    "ExampleConversation" TEXT NOT NULL,
    "AdditionalContext" TEXT NOT NULL,
    "Temperature" REAL NOT NULL,
    "MaxTokens" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "Version" TEXT NOT NULL,
    "Metadata" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "LastModified" TEXT NOT NULL,
    CONSTRAINT "FK_Prompts_Avatars_AvatarId" FOREIGN KEY ("AvatarId") REFERENCES "Avatars" ("Id") ON DELETE CASCADE
);
"@
    
    # SQL to create indexes
    $createIndexes = @"
CREATE INDEX IF NOT EXISTS "IX_Avatars_UserId" ON "Avatars" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_Prompts_AvatarId_IsActive" ON "Prompts" ("AvatarId", "IsActive");
"@
    
    # Execute SQL commands
    $command = $connection.CreateCommand()
    
    Write-Host "📋 Creating Avatars table..." -ForegroundColor Yellow
    $command.CommandText = $createAvatarsTable
    $command.ExecuteNonQuery() | Out-Null
    
    Write-Host "📋 Creating Prompts table..." -ForegroundColor Yellow
    $command.CommandText = $createPromptsTable
    $command.ExecuteNonQuery() | Out-Null
    
    Write-Host "📋 Creating indexes..." -ForegroundColor Yellow
    $command.CommandText = $createIndexes
    $command.ExecuteNonQuery() | Out-Null
    
    # Verify tables exist
    $command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND (name='Avatars' OR name='Prompts');"
    $reader = $command.ExecuteReader()
    
    $tables = @()
    while ($reader.Read()) {
        $tables += $reader["name"]
    }
    $reader.Close()
    
    Write-Host "✅ Tables created successfully:" -ForegroundColor Green
    foreach ($table in $tables) {
        Write-Host "   - $table" -ForegroundColor Green
    }
    
    # Check if Avatars table has any data
    $command.CommandText = "SELECT COUNT(*) as count FROM Avatars;"
    $avatarCount = $command.ExecuteScalar()
    Write-Host "📊 Current Avatars count: $avatarCount" -ForegroundColor Cyan
    
    $connection.Close()
    Write-Host "🎉 Database setup completed successfully!" -ForegroundColor Green
    
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "💡 Make sure System.Data.SQLite.dll is available in the current directory" -ForegroundColor Yellow
    exit 1
}
