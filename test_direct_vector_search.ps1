# Test Neo4j vector search directly with a simple query
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

Write-Host "Testing Neo4j vector search directly..."

# First, get an embedding for "cat"
Write-Host "=== Getting embedding for 'cat' ==="
$embeddingBody = @{
    model = "all-minilm"
    prompt = "cat"
} | ConvertTo-Json

try {
    $embeddingResponse = Invoke-RestMethod -Uri 'http://localhost:11434/api/embeddings' -Method POST -Headers @{'Content-Type' = 'application/json'} -Body $embeddingBody
    Write-Host "Embedding generated successfully. Length: $($embeddingResponse.embedding.Length)"
    
    # Test 1: Simple vector search without user filter
    Write-Host ""
    Write-Host "=== Test 1: Vector search without user filter ==="
    $queryVector = $embeddingResponse.embedding
    
    $neo4jBody1 = @{
        statements = @(
            @{
                statement = "CALL db.index.vector.queryNodes('memory_embeddings', 5, `$queryVector) YIELD node, score RETURN node.id as id, node.content as content, score ORDER BY score DESC"
                parameters = @{
                    queryVector = $queryVector
                }
            }
        )
    } | ConvertTo-Json -Depth 4
    
    $neo4jResponse1 = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $neo4jBody1
    
    Write-Host "Vector search results (no user filter):"
    if ($neo4jResponse1.results -and $neo4jResponse1.results[0].data) {
        $results = $neo4jResponse1.results[0].data
        Write-Host "Found $($results.Count) results:"
        
        foreach ($result in $results) {
            $row = $result.row
            Write-Host "  ID: $($row[0])"
            Write-Host "  Content: $($row[1])"
            Write-Host "  Score: $($row[2])"
            Write-Host "  ---"
        }
    } else {
        Write-Host "❌ No results found"
    }
    
    # Test 2: Vector search with user filter
    Write-Host ""
    Write-Host "=== Test 2: Vector search with user filter ==="
    $userId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"
    
    $neo4jBody2 = @{
        statements = @(
            @{
                statement = "CALL db.index.vector.queryNodes('memory_embeddings', 5, `$queryVector) YIELD node, score WHERE node.userId = `$userId RETURN node.id as id, node.content as content, node.userId as userId, score ORDER BY score DESC"
                parameters = @{
                    queryVector = $queryVector
                    userId = $userId
                }
            }
        )
    } | ConvertTo-Json -Depth 4
    
    $neo4jResponse2 = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $neo4jBody2
    
    Write-Host "Vector search results (with user filter):"
    if ($neo4jResponse2.results -and $neo4jResponse2.results[0].data) {
        $results = $neo4jResponse2.results[0].data
        Write-Host "Found $($results.Count) results:"
        
        foreach ($result in $results) {
            $row = $result.row
            Write-Host "  ID: $($row[0])"
            Write-Host "  Content: $($row[1])"
            Write-Host "  User ID: $($row[2])"
            Write-Host "  Score: $($row[3])"
            Write-Host "  ---"
        }
    } else {
        Write-Host "❌ No results found with user filter"
    }
    
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
