# Test to load characters from filesystem
$baseUrl = "http://localhost:5000"

Write-Host "Loading characters from filesystem..." -ForegroundColor Green

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/character/load-from-filesystem" -Method GET
    Write-Host "Success: $($response.message)" -ForegroundColor Green
} catch {
    Write-Host "Error loading from filesystem: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response body: $responseBody" -ForegroundColor Yellow
    }
}

# Now list characters again
Write-Host "`nListing characters after filesystem load..." -ForegroundColor Green
try {
    $adminUserId = "00000000-0000-0000-0000-000000000001"
    $characters = Invoke-RestMethod -Uri "$baseUrl/api/character/user/$adminUserId" -Method GET
    Write-Host "Found $($characters.Count) characters:" -ForegroundColor Green

    foreach ($char in $characters) {
        Write-Host "  - Name: '$($char.name)'" -ForegroundColor Cyan
        Write-Host "    ID: $($char.id)" -ForegroundColor Gray
        Write-Host "    SystemPrompt Length: $($char.systemPrompt.Length)" -ForegroundColor Gray
        Write-Host ""
    }
} catch {
    Write-Host "Error listing characters: $($_.Exception.Message)" -ForegroundColor Red
}
