# Test to get detailed character information
$baseUrl = "http://localhost:5000"
$adminUserId = "00000000-0000-0000-0000-000000000001"

Write-Host "Getting detailed character information..." -ForegroundColor Green

try {
    $characters = Invoke-RestMethod -Uri "$baseUrl/api/character/user/$adminUserId" -Method GET
    Write-Host "Found $($characters.Count) characters:" -ForegroundColor Green
    
    foreach ($char in $characters) {
        Write-Host "=== Character Details ===" -ForegroundColor Yellow
        Write-Host "ID: $($char.id)" -ForegroundColor Cyan
        Write-Host "Name: '$($char.name)'" -ForegroundColor Cyan
        Write-Host "Description: '$($char.description)'" -ForegroundColor Cyan
        Write-Host "Personality: '$($char.personality)'" -ForegroundColor Cyan
        Write-Host "ImagePath: '$($char.imagePath)'" -ForegroundColor Cyan
        Write-Host "VoiceSettings: '$($char.voiceSettings)'" -ForegroundColor Cyan
        Write-Host "SystemPrompt: '$($char.systemPrompt)'" -ForegroundColor Cyan
        Write-Host "YamlProfile: '$($char.yamlProfile)'" -ForegroundColor Cyan
        Write-Host "CreatedAt: $($char.createdAt)" -ForegroundColor Cyan
        Write-Host "UserId: $($char.userId)" -ForegroundColor Cyan
        Write-Host ""
    }
} catch {
    Write-Host "Error getting character details: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response body: $responseBody" -ForegroundColor Yellow
    }
}
