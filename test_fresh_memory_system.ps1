# Test the memory system from scratch with fresh data
Write-Host "🧪 Testing fresh memory system..."
Write-Host ""

$userId = "42dfa1c0-c093-4f58-bb3e-cc83bbd6d249"  # Using the actual user ID from logs

# Step 1: Create a new memory
Write-Host "=== Step 1: Creating a new memory ==="
$createBody = @{
    content = "My dog's name is Cujo and he loves to play fetch"
    category = "Personal"
} | ConvertTo-Json

try {
    $createResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/memory?userId=$userId" -Method POST -Headers @{'Content-Type' = 'application/json'} -Body $createBody
    Write-Host "✅ Memory created successfully"
    Write-Host "   Memory ID: $($createResponse.id)"
} catch {
    Write-Host "❌ Error creating memory: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "   Response Body: $responseBody"
    }
    return
}

# Step 2: Wait for indexing
Write-Host ""
Write-Host "=== Step 2: Waiting for indexing ==="
Start-Sleep -Seconds 3
Write-Host "✅ Indexing wait completed"

# Step 3: Test vector search
Write-Host ""
Write-Host "=== Step 3: Testing vector search ==="

$searchQueries = @("dog", "Cujo", "pet", "fetch", "play")

foreach ($query in $searchQueries) {
    Write-Host "Searching for '$query'..."
    try {
        $searchResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/search?userId=$userId&query=$query&maxResults=5" -Method GET
        Write-Host "  Found $($searchResponse.Count) results"
        
        if ($searchResponse.Count -gt 0) {
            foreach ($result in $searchResponse) {
                Write-Host "    Content: $($result.Memory.content)"
                Write-Host "    Similarity: $($result.Similarity)"
                Write-Host "    ID: $($result.Memory.id)"
                Write-Host "    ---"
            }
        }
    } catch {
        Write-Host "  ❌ Error searching: $($_.Exception.Message)"
    }
    Write-Host ""
}

# Step 4: Test chat with memory retrieval
Write-Host "=== Step 4: Testing chat with memory retrieval ==="
$chatBody = @{
    message = "What is my dog's name?"
    conversationId = [System.Guid]::NewGuid().ToString()
    userId = $userId
} | ConvertTo-Json

try {
    $chatResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" -Method POST -Headers @{'Content-Type' = 'application/json'} -Body $chatBody
    Write-Host "✅ Chat response received"
    Write-Host "   Response: $($chatResponse.message)"
    if ($chatResponse.memoriesUsed) {
        Write-Host "   Memories used: $($chatResponse.memoriesUsed)"
    }
} catch {
    Write-Host "❌ Error with chat: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "   Response Body: $responseBody"
    }
}

Write-Host ""
Write-Host "🎯 Fresh memory system test completed!"
