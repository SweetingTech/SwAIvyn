# Test script to upload Sherlock character and test character switching
$baseUrl = "http://localhost:5000"
$adminUserId = "00000000-0000-0000-0000-000000000001"

# Read the Sherlock YAML file
$sherlockYaml = Get-Content "test-character-sherlock.yaml" -Raw

Write-Host "Testing Character Upload and Switching..." -ForegroundColor Green

# 1. Upload Sherlock character via YAML endpoint
Write-Host "`n1. Uploading Sherlock character..." -ForegroundColor Yellow
$uploadBody = @{
    userId = $adminUserId
    yamlProfile = $sherlockYaml
} | ConvertTo-Json

try {
    $uploadResponse = Invoke-RestMethod -Uri "$baseUrl/api/character/yaml" -Method POST -Body $uploadBody -ContentType "application/json"
    Write-Host "✅ Sherlock character uploaded successfully!" -ForegroundColor Green
    Write-Host "Character ID: $($uploadResponse.id)" -ForegroundColor Cyan
    $sherlockId = $uploadResponse.id
} catch {
    Write-Host "❌ Failed to upload Sherlock character: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 2. List all characters to verify both GLaDOS and Sherlock exist
Write-Host "`n2. Listing all characters..." -ForegroundColor Yellow
try {
    $characters = Invoke-RestMethod -Uri "$baseUrl/api/character/user/$adminUserId" -Method GET
    Write-Host "✅ Found $($characters.Count) characters:" -ForegroundColor Green
    foreach ($char in $characters) {
        Write-Host "  - $($char.name) (ID: $($char.id))" -ForegroundColor Cyan
    }
} catch {
    Write-Host "❌ Failed to list characters: $($_.Exception.Message)" -ForegroundColor Red
}

# 3. Create a new conversation
Write-Host "`n3. Creating new conversation..." -ForegroundColor Yellow
$newConversationId = [System.Guid]::NewGuid()
$createConvBody = @{
    conversationId = $newConversationId
    userId = $adminUserId
    title = "Character Switching Test"
} | ConvertTo-Json

try {
    $convResponse = Invoke-RestMethod -Uri "$baseUrl/api/conversation" -Method POST -Body $createConvBody -ContentType "application/json"
    Write-Host "✅ Conversation created: $newConversationId" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to create conversation: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 4. Test chat with Sherlock character
Write-Host "`n4. Testing chat with Sherlock character..." -ForegroundColor Yellow
$chatBody = @{
    conversationId = $newConversationId
    userId = $adminUserId
    message = "Hello, I have a mystery to solve!"
    characterId = $sherlockId
} | ConvertTo-Json

try {
    $chatResponse = Invoke-RestMethod -Uri "$baseUrl/api/conversation/chat" -Method POST -Body $chatBody -ContentType "application/json"
    Write-Host "✅ Sherlock responded:" -ForegroundColor Green
    Write-Host "$($chatResponse.response)" -ForegroundColor White
} catch {
    Write-Host "❌ Failed to chat with Sherlock: $($_.Exception.Message)" -ForegroundColor Red
}

# 5. Test chat without character (should use GLaDOS default)
Write-Host "`n5. Testing chat without character (should use GLaDOS default)..." -ForegroundColor Yellow
$defaultChatBody = @{
    conversationId = $newConversationId
    userId = $adminUserId
    message = "What test chamber am I in?"
} | ConvertTo-Json

try {
    $defaultChatResponse = Invoke-RestMethod -Uri "$baseUrl/api/conversation/chat" -Method POST -Body $defaultChatBody -ContentType "application/json"
    Write-Host "✅ Default character (GLaDOS) responded:" -ForegroundColor Green
    Write-Host "$($defaultChatResponse.response)" -ForegroundColor White
} catch {
    Write-Host "❌ Failed to chat with default character: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nCharacter switching test completed!" -ForegroundColor Green
