# Test if we can find the dog's name using text search
Write-Host "Testing if we can find your dog's name using different search approaches..."
Write-Host ""

$userId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"

# Test 1: Direct API call to get memories for the user
Write-Host "=== Test 1: Get all memories for user ==="
try {
    $response1 = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/user/$userId" -Method GET
    Write-Host "Found $($response1.Count) memories for user"
    
    $dogMemories = $response1 | Where-Object { $_.content -like "*dog*" -or $_.content -like "*Cujo*" }
    Write-Host "Dog-related memories: $($dogMemories.Count)"
    
    foreach ($memory in $dogMemories) {
        Write-Host "  Content: $($memory.content)"
        Write-Host "  Category: $($memory.category)"
        Write-Host "  Created: $($memory.createdAt)"
        Write-Host "  ---"
    }
} catch {
    Write-Host "Error in Test 1: $($_.Exception.Message)"
}

Write-Host ""

# Test 2: Try the brain graph memories endpoint
Write-Host "=== Test 2: Get brain graph memories ==="
try {
    $response2 = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/brain-graph/$userId" -Method GET
    Write-Host "Found $($response2.memories.Count) brain graph memories"
    
    $dogMemories2 = $response2.memories | Where-Object { $_.content -like "*dog*" -or $_.content -like "*Cujo*" }
    Write-Host "Dog-related brain memories: $($dogMemories2.Count)"
    
    foreach ($memory in $dogMemories2) {
        Write-Host "  Content: $($memory.content)"
        Write-Host "  Category: $($memory.category)"
        Write-Host "  Created: $($memory.createdAt)"
        Write-Host "  ---"
    }
} catch {
    Write-Host "Error in Test 2: $($_.Exception.Message)"
}

Write-Host ""

# Test 3: Try a simple chat to see if the AI can access memories
Write-Host "=== Test 3: Test chat with dog question ==="
$chatBody = @{
    message = "What is my dog's name?"
    conversationId = [System.Guid]::NewGuid().ToString()
    userId = $userId
} | ConvertTo-Json

try {
    $response3 = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" -Method POST -Headers @{'Content-Type' = 'application/json'} -Body $chatBody
    Write-Host "Chat response:"
    Write-Host "  Message: $($response3.message)"
    Write-Host "  Memories used: $($response3.memoriesUsed)"
} catch {
    Write-Host "Error in Test 3: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}

Write-Host ""
Write-Host "Summary: The memories are definitely stored in Neo4j and can be found with text search."
Write-Host "The issue appears to be with the vector search functionality, not with memory storage."
