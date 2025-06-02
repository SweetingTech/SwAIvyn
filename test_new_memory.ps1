# Test creating a new memory with current embedding model and searching for it
Write-Host "Testing new memory creation and search..."

$userId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"

# Create a new memory
Write-Host "=== Creating new memory ==="
$createBody = @{
    content = "My cat name is Whiskers"
    category = "Personal"
} | ConvertTo-Json

try {
    $createResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/memory?userId=$userId" -Method POST -Headers @{'Content-Type' = 'application/json'} -Body $createBody
    Write-Host "Memory created successfully"
    Write-Host "Memory ID: $($createResponse.id)"
} catch {
    Write-Host "Error creating memory: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}

# Wait a moment for the memory to be indexed
Start-Sleep -Seconds 2

# Search for the new memory
Write-Host ""
Write-Host "=== Searching for new memory ==="
try {
    $searchResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/search?userId=$userId&query=cat&maxResults=5" -Method GET
    Write-Host "Found $($searchResponse.Count) results for 'cat'"
    $searchResponse | ForEach-Object {
        Write-Host "  Content: $($_.Memory.content)"
        Write-Host "  Similarity: $($_.Similarity)"
        Write-Host "  ID: $($_.Memory.id)"
        Write-Host "  ---"
    }
} catch {
    Write-Host "Error searching for 'cat': $($_.Exception.Message)"
}

# Also search for "Whiskers"
Write-Host ""
Write-Host "=== Searching for 'Whiskers' ==="
try {
    $searchResponse2 = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/search?userId=$userId&query=Whiskers&maxResults=5" -Method GET
    Write-Host "Found $($searchResponse2.Count) results for 'Whiskers'"
    $searchResponse2 | ForEach-Object {
        Write-Host "  Content: $($_.Memory.content)"
        Write-Host "  Similarity: $($_.Similarity)"
        Write-Host "  ID: $($_.Memory.id)"
        Write-Host "  ---"
    }
} catch {
    Write-Host "Error searching for 'Whiskers': $($_.Exception.Message)"
}
