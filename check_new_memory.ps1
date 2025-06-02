# Check if the new memory was stored correctly in Neo4j
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

Write-Host "Checking if new memory was stored correctly..."

$memoryId = "9f344fe0-069a-4add-9e8d-e28b49fbe3b0"

$body = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) WHERE m.id = `$memoryId RETURN m.id, m.content, m.userId, m.embedding IS NOT NULL as hasEmbedding, size(m.embedding) as embeddingSize"
            parameters = @{
                memoryId = $memoryId
            }
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $body
    Write-Host "New memory details:"
    Write-Host "=================="
    
    if ($response.results -and $response.results[0].data) {
        $result = $response.results[0].data[0]
        $row = $result.row
        Write-Host "ID: $($row[0])"
        Write-Host "Content: $($row[1])"
        Write-Host "User ID: $($row[2])"
        Write-Host "Has Embedding: $($row[3])"
        if ($row[3]) {
            Write-Host "Embedding Size: $($row[4])"
        }
    } else {
        Write-Host "❌ Memory not found in Neo4j!"
    }
    
} catch {
    Write-Host "Error: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "Checking all memories with 'cat' in content..."

$body2 = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) WHERE m.content CONTAINS 'cat' RETURN m.id, m.content, m.userId, m.embedding IS NOT NULL as hasEmbedding, size(m.embedding) as embeddingSize"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $response2 = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $body2
    Write-Host "Memories containing 'cat':"
    Write-Host "========================="
    
    if ($response2.results -and $response2.results[0].data) {
        foreach ($result in $response2.results[0].data) {
            $row = $result.row
            Write-Host "ID: $($row[0])"
            Write-Host "  Content: $($row[1])"
            Write-Host "  User ID: $($row[2])"
            Write-Host "  Has Embedding: $($row[3])"
            if ($row[3]) {
                Write-Host "  Embedding Size: $($row[4])"
            }
            Write-Host "  ---"
        }
    } else {
        Write-Host "No memories found containing 'cat'"
    }
    
} catch {
    Write-Host "Error: $($_.Exception.Message)"
}
