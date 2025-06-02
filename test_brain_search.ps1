# Test memory search functionality
Write-Host "Testing memory search for dog-related memories..."
Write-Host ""

# User ID from the memory we created
$userId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"

# Test 1: Search for "dog"
Write-Host "=== Test 1: Searching for 'dog' ==="
try {
    $response1 = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/search?userId=$userId&query=dog&maxResults=5" -Method GET
    Write-Host "Found $($response1.Count) results for 'dog'"
    $response1 | ForEach-Object {
        Write-Host "  Content: $($_.Memory.content)"
        Write-Host "  Similarity: $($_.Similarity)"
        Write-Host "  ID: $($_.Memory.id)"
        Write-Host "  ---"
    }
} catch {
    Write-Host "Error searching for 'dog': $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}

Write-Host ""

# Test 2: Search for "pet"
Write-Host "=== Test 2: Searching for 'pet' ==="
try {
    $response2 = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/search?userId=$userId&query=pet&maxResults=5" -Method GET
    Write-Host "Found $($response2.Count) results for 'pet'"
    $response2 | ForEach-Object {
        Write-Host "  Content: $($_.Memory.content)"
        Write-Host "  Similarity: $($_.Similarity)"
        Write-Host "  ID: $($_.Memory.id)"
        Write-Host "  ---"
    }
} catch {
    Write-Host "Error searching for 'pet': $($_.Exception.Message)"
}

Write-Host ""

# Test 3: Search for "Cujo"
Write-Host "=== Test 3: Searching for 'Cujo' ==="
try {
    $response3 = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/search?userId=$userId&query=Cujo&maxResults=5" -Method GET
    Write-Host "Found $($response3.Count) results for 'Cujo'"
    $response3 | ForEach-Object {
        Write-Host "  Content: $($_.Memory.content)"
        Write-Host "  Similarity: $($_.Similarity)"
        Write-Host "  ID: $($_.Memory.id)"
        Write-Host "  ---"
    }
} catch {
    Write-Host "Error searching for 'Cujo': $($_.Exception.Message)"
}

Write-Host ""

# Test 4: Search for "name"
Write-Host "=== Test 4: Searching for 'name' ==="
try {
    $response4 = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/search?userId=$userId&query=name&maxResults=5" -Method GET
    Write-Host "Found $($response4.Count) results for 'name'"
    $response4 | ForEach-Object {
        Write-Host "  Content: $($_.Memory.content)"
        Write-Host "  Similarity: $($_.Similarity)"
        Write-Host "  ID: $($_.Memory.id)"
        Write-Host "  ---"
    }
} catch {
    Write-Host "Error searching for 'name': $($_.Exception.Message)"
}
