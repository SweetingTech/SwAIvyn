# Test to list existing characters
$baseUrl = "http://localhost:5000"
$adminUserId = "00000000-0000-0000-0000-000000000001"

Write-Host "Listing existing characters..." -ForegroundColor Green

try {
    $characters = Invoke-RestMethod -Uri "$baseUrl/api/character/user/$adminUserId" -Method GET
    Write-Host "Found $($characters.Count) characters:" -ForegroundColor Green
    foreach ($char in $characters) {
        Write-Host "  - Name: $($char.name)" -ForegroundColor Cyan
        Write-Host "    ID: $($char.id)" -ForegroundColor Gray
        Write-Host "    Description: $($char.description)" -ForegroundColor Gray
        Write-Host "    SystemPrompt Length: $($char.systemPrompt.Length)" -ForegroundColor Gray
        Write-Host ""
    }
} catch {
    Write-Host "Error listing characters: $($_.Exception.Message)" -ForegroundColor Red
}
