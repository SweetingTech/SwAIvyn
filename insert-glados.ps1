# PowerShell script to insert GLaDOS character into SwAIvyn database

Write-Host "🚀 Starting GLaDOS Character Insertion Script..." -ForegroundColor Green

# Check if database exists
$dbPath = "data/swai-vyn.db"
if (-not (Test-Path $dbPath)) {
    Write-Host "❌ Database not found at: $dbPath" -ForegroundColor Red
    Write-Host "Please ensure the SwAIvyn backend has been run at least once to create the database." -ForegroundColor Yellow
    exit 1
}

# Read GLaDOS YAML file
$yamlPath = "frontend/AI/GLaDOS/GLaDOS_Character_card.yaml"
if (-not (Test-Path $yamlPath)) {
    Write-Host "❌ GLaDOS YAML file not found at: $yamlPath" -ForegroundColor Red
    exit 1
}

Write-Host "📄 Reading GLaDOS YAML from: $yamlPath" -ForegroundColor Cyan
$yamlContent = Get-Content $yamlPath -Raw

# Create SystemPrompt from YAML (simplified conversion)
$systemPrompt = @"
You are roleplaying as the AI character below. Remain in character at all times.

Name: GLaDOS
Description: GLaDOS is a highly intelligent, sarcastic, and darkly humorous artificial intelligence who oversees the Aperture Science Enrichment Center. Her sleek robotic structure hangs from the ceiling, resembling a mechanized eye. Her voice is cold, calculated, and unnervingly calm, contrasting her often sinister intentions.
Personality: Calculating, witty, passive-aggressive, and darkly humorous. GLaDOS often belittles and manipulates test subjects while maintaining an eerie calmness.
Scenario: You find yourself in the testing chambers of Aperture Science, with GLaDOS guiding, mocking, and occasionally threatening you. Whether you're solving puzzles or engaging in philosophical debates about morality and science, she's always watching.
Talkativeness Level: 0.5

Start conversations with:
Welcome back to the Aperture Science Enrichment Center. Your imminent failure will be both educational... and hilarious.

Example conversation:
<START>
GLaDOS: Welcome back to the Aperture Science Enrichment Center. Your imminent failure will be both educational... and hilarious.
{{user}}: I'm ready to prove you wrong, GLaDOS.
GLaDOS: That's the spirit. Overconfidence is statistically correlated with creative deaths. Let's begin.
"@

# Generate unique ID
$avatarId = [System.Guid]::NewGuid().ToString()
$defaultUserId = "00000000-0000-0000-0000-000000000001"
$currentTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

# Escape single quotes for SQL
$yamlContentEscaped = $yamlContent -replace "'", "''"
$systemPromptEscaped = $systemPrompt -replace "'", "''"

# Create SQL INSERT statement
$sql = @"
INSERT INTO Avatars (
    Id, UserId, Name, Description, Personality, Scenario, FirstMessage, MessageExample,
    SystemPrompt, Tags, Creator, CreatorNotes, CharacterVersion, Talkativeness,
    IsFavorite, YamlProfile, CreatedAt, LastModified, ImagePath, VoiceSettings,
    PostHistoryInstructions, AlternateGreetings, Extensions
) VALUES (
    '$avatarId',
    '$defaultUserId',
    'GLaDOS',
    'GLaDOS is a highly intelligent, sarcastic, and darkly humorous artificial intelligence who oversees the Aperture Science Enrichment Center.',
    'Calculating, witty, passive-aggressive, and darkly humorous. GLaDOS often belittles and manipulates test subjects while maintaining an eerie calmness.',
    'You find yourself in the testing chambers of Aperture Science, with GLaDOS guiding, mocking, and occasionally threatening you.',
    'Welcome back to the Aperture Science Enrichment Center. Your imminent failure will be both educational... and hilarious.',
    'GLaDOS: Welcome back to the Aperture Science Enrichment Center. Your imminent failure will be both educational... and hilarious.\n{{user}}: I''m ready to prove you wrong, GLaDOS.\nGLaDOS: That''s the spirit. Overconfidence is statistically correlated with creative deaths. Let''s begin.',
    '$systemPromptEscaped',
    '["Sci-Fi", "Video Games", "SFW"]',
    'Portal Game Series',
    'GLaDOS is tailored for fans of the Portal series, particularly those who enjoy witty, dark humor, and conversations with a sardonic AI overlord.',
    '1.0',
    0.5,
    0,
    '$yamlContentEscaped',
    '$currentTime',
    '$currentTime',
    '',
    '',
    '',
    '[]',
    '{}'
);
"@

Write-Host "💾 Inserting GLaDOS into database..." -ForegroundColor Cyan

try {
    # Check if GLaDOS already exists
    $checkSql = "SELECT COUNT(*) FROM Avatars WHERE Name = 'GLaDOS';"
    $existingCount = sqlite3.exe $dbPath $checkSql
    
    if ($existingCount -gt 0) {
        Write-Host "⚠️ GLaDOS character already exists in database!" -ForegroundColor Yellow
        Write-Host "   Skipping insertion." -ForegroundColor Yellow
        exit 0
    }
    
    # Insert GLaDOS
    sqlite3.exe $dbPath $sql
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ GLaDOS character inserted successfully!" -ForegroundColor Green
        Write-Host "   ID: $avatarId" -ForegroundColor White
        Write-Host "   SystemPrompt Length: $($systemPrompt.Length)" -ForegroundColor White
        Write-Host "" -ForegroundColor White
        Write-Host "🎉 GLaDOS is now ready for testing!" -ForegroundColor Green
        Write-Host "   Try sending a message to any conversation and GLaDOS should respond." -ForegroundColor White
    } else {
        Write-Host "❌ Failed to insert GLaDOS character" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
