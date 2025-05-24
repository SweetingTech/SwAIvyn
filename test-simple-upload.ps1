# Simple test to debug character upload
$baseUrl = "http://localhost:5000"
$adminUserId = "00000000-0000-0000-0000-000000000001"

# Simple YAML content with all required fields
$simpleYaml = @"
name: Test Character
description: A simple test character
personality: Friendly and helpful
scenario: You are a helpful assistant
first_message: Hello! How can I help you today?
mes_example: |
  User: Hi
  Assistant: Hello there!
creator_notes: Test character for debugging
tags:
  - Test
  - Debug
talkativeness: 0.5
"@

Write-Host "Testing simple character upload..." -ForegroundColor Green

$uploadBody = @{
    userId = $adminUserId
    yamlProfile = $simpleYaml
} | ConvertTo-Json -Depth 10

Write-Host "Request body:" -ForegroundColor Yellow
Write-Host $uploadBody

try {
    $response = Invoke-WebRequest -Uri "$baseUrl/api/character/yaml" -Method POST -Body $uploadBody -ContentType "application/json" -UseBasicParsing
    Write-Host "Success! Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Response: $($response.Content)" -ForegroundColor Cyan
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response body: $responseBody" -ForegroundColor Yellow
    }
}
