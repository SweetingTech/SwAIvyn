# Test Neo4j vector search directly
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

# First, let's get the embedding for "dog" using the same API the application uses
Write-Host "Getting embedding for 'dog'..."
$embeddingBody = @{
    model = "nomic-embed-text"
    prompt = "dog"
} | ConvertTo-Json

try {
    $embeddingResponse = Invoke-RestMethod -Uri 'http://localhost:11434/api/embeddings' -Method POST -Headers @{'Content-Type' = 'application/json'} -Body $embeddingBody
    Write-Host "Embedding generated successfully. Length: $($embeddingResponse.embedding.Length)"
    
    # Now test the Neo4j vector search with this embedding
    $queryVector = $embeddingResponse.embedding
    $userId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"
    
    Write-Host "Testing Neo4j vector search..."
    
    $neo4jBody = @{
        statements = @(
            @{
                statement = "CALL db.index.vector.queryNodes('memory_embeddings', `$limit, `$queryVector) YIELD node, score WHERE node.userId = `$userId RETURN node.id as id, node.content as content, node.category as category, node.userId as userId, score ORDER BY score DESC"
                parameters = @{
                    queryVector = $queryVector
                    userId = $userId
                    limit = 5
                }
            }
        )
    } | ConvertTo-Json -Depth 4
    
    $neo4jResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $neo4jBody
    
    Write-Host "Neo4j Vector Search Results:"
    Write-Host "============================"
    
    if ($neo4jResponse.results -and $neo4jResponse.results[0].data) {
        $results = $neo4jResponse.results[0].data
        Write-Host "Found $($results.Count) results:"
        
        foreach ($result in $results) {
            $row = $result.row
            Write-Host "  ID: $($row[0])"
            Write-Host "  Content: $($row[1])"
            Write-Host "  Category: $($row[2])"
            Write-Host "  User ID: $($row[3])"
            Write-Host "  Score: $($row[4])"
            Write-Host "  ---"
        }
    } else {
        Write-Host "No results found"
    }
    
    Write-Host ""
    Write-Host "Raw Neo4j response:"
    $neo4jResponse | ConvertTo-Json -Depth 5
    
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
