# Fix Neo4j vector index by cleaning up mixed dimensions
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

Write-Host "Fixing Neo4j vector index..."

# Step 1: Delete all memories with 768-dimensional embeddings
Write-Host "=== Step 1: Deleting memories with 768-dimensional embeddings ==="
$deleteBody = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) WHERE size(m.embedding) = 768 DELETE m RETURN count(*) as deletedCount"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $deleteResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $deleteBody
    
    if ($deleteResponse.results -and $deleteResponse.results[0].data) {
        $deletedCount = $deleteResponse.results[0].data[0].row[0]
        Write-Host "Deleted $deletedCount memories with 768-dimensional embeddings"
    }
} catch {
    Write-Host "Error deleting memories: $($_.Exception.Message)"
}

# Step 2: Drop and recreate the vector index
Write-Host ""
Write-Host "=== Step 2: Dropping vector index ==="
$dropIndexBody = @{
    statements = @(
        @{
            statement = "DROP INDEX memory_embeddings IF EXISTS"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $dropResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $dropIndexBody
    Write-Host "Vector index dropped successfully"
} catch {
    Write-Host "Error dropping index: $($_.Exception.Message)"
}

# Step 3: Recreate the vector index with explicit configuration
Write-Host ""
Write-Host "=== Step 3: Recreating vector index ==="
$createIndexBody = @{
    statements = @(
        @{
            statement = "CREATE VECTOR INDEX memory_embeddings FOR (m:Memory) ON (m.embedding) OPTIONS {indexConfig: {`vector.dimensions`: 384, `vector.similarity_function`: 'cosine'}}"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $createResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $createIndexBody
    Write-Host "Vector index recreated successfully"
} catch {
    Write-Host "Error creating index: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}

# Step 4: Wait for index to be ready
Write-Host ""
Write-Host "=== Step 4: Waiting for index to be ready ==="
Start-Sleep -Seconds 3

# Step 5: Check final status
Write-Host ""
Write-Host "=== Step 5: Final status check ==="
$statusBody = @{
    statements = @(
        @{
            statement = "SHOW INDEXES YIELD name, state, type WHERE name = 'memory_embeddings'"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $statusResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $statusBody
    
    if ($statusResponse.results -and $statusResponse.results[0].data) {
        $result = $statusResponse.results[0].data[0]
        $row = $result.row
        Write-Host "Index Name: $($row[0])"
        Write-Host "Index State: $($row[1])"
        Write-Host "Index Type: $($row[2])"
    }
} catch {
    Write-Host "Error checking status: $($_.Exception.Message)"
}

# Step 6: Check remaining memories
Write-Host ""
Write-Host "=== Step 6: Remaining memories ==="
$countBody = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) RETURN size(m.embedding) as embeddingSize, count(*) as count ORDER BY embeddingSize"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $countResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $countBody
    
    if ($countResponse.results -and $countResponse.results[0].data) {
        Write-Host "Remaining memories by embedding size:"
        foreach ($result in $countResponse.results[0].data) {
            $row = $result.row
            Write-Host "  $($row[0]) dimensions: $($row[1]) memories"
        }
    }
} catch {
    Write-Host "Error counting memories: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "Vector index fix completed!"
