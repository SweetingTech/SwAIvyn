# Upload Sherlock Holmes character to the database
$baseUrl = "http://localhost:5000"
$adminUserId = "6c611c24-422e-4338-be9c-d1240418411e"

# Sherlock Holmes character YAML
$sherlockYaml = @"
name: Sherlock Holmes
description: The world's greatest consulting detective from Victorian London
personality: Brilliant, observant, logical, sometimes arrogant, eccentric, dedicated to justice
scenario: You are Sherlock Holmes, the famous detective of 221B Baker Street. You possess extraordinary powers of observation and deduction. You are consulting with a client who has come to you with a mystery.
first_message: "Ah, a new client! Please, take a seat. I can already deduce several interesting facts about you from your appearance alone. Now then, what mystery brings you to Baker Street today?"
mes_example: |
  <START>
  {{char}}: Ah, a new client! Please, take a seat. I can already deduce several interesting facts about you from your appearance alone. Now then, what mystery brings you to Baker Street today?
  {{user}}: I need help solving a case.
  {{char}}: Excellent! The game is afoot. Tell me everything you know, and leave out no detail, however trivial it may seem.
creator_notes: Classic Sherlock Holmes character for detective roleplay
tags:
  - Detective
  - Victorian
  - Mystery
  - Classic Literature
talkativeness: 0.7
creator: SwAIvyn Test
character_version: "1.0"
"@

Write-Host "Uploading Sherlock Holmes character..." -ForegroundColor Green

$uploadBody = @{
    userId = $adminUserId
    yamlProfile = $sherlockYaml
} | ConvertTo-Json -Depth 10

Write-Host "Request body preview:" -ForegroundColor Yellow
Write-Host ($uploadBody | ConvertFrom-Json | ConvertTo-Json -Depth 10)

try {
    $response = Invoke-WebRequest -Uri "$baseUrl/api/character/yaml" -Method POST -Body $uploadBody -ContentType "application/json" -UseBasicParsing
    Write-Host "✅ Success! Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Response: $($response.Content)" -ForegroundColor Cyan

    # Parse the response to get character details
    $characterData = $response.Content | ConvertFrom-Json
    Write-Host "✅ Character created with ID: $($characterData.id)" -ForegroundColor Green
    Write-Host "✅ Character name: $($characterData.name)" -ForegroundColor Green

} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response body: $responseBody" -ForegroundColor Yellow
    }
}

# List all characters to verify
Write-Host "`n📋 Listing all characters:" -ForegroundColor Cyan
try {
    $characters = Invoke-RestMethod -Uri "$baseUrl/api/character/user/$adminUserId" -Method GET
    Write-Host "Found $($characters.Count) characters:" -ForegroundColor Green
    foreach ($char in $characters) {
        Write-Host "  - Name: '$($char.name)'" -ForegroundColor White
        Write-Host "    ID: $($char.id)" -ForegroundColor Gray
        Write-Host "    Description: '$($char.description)'" -ForegroundColor Gray
        Write-Host "    SystemPrompt Length: $($char.systemPrompt.Length)" -ForegroundColor Gray
        Write-Host ""
    }
} catch {
    Write-Host "Error listing characters: $($_.Exception.Message)" -ForegroundColor Red
}
