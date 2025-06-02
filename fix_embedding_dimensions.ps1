# Fix embedding dimension mismatch by removing memories with wrong dimensions
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

Write-Host "Fixing embedding dimension mismatch..."
Write-Host ""

# First, let's see how many memories have each dimension size
Write-Host "=== Current Memory Status ==="
$countBody = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) WHERE m.userId = `$userId RETURN size(m.embedding) as embeddingSize, count(*) as count ORDER BY embeddingSize"
            parameters = @{
                userId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"
            }
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $countResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $countBody
    
    if ($countResponse.results -and $countResponse.results[0].data) {
        foreach ($result in $countResponse.results[0].data) {
            $row = $result.row
            Write-Host "Embedding size $($row[0]): $($row[1]) memories"
        }
    }
} catch {
    Write-Host "Error counting memories: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "=== Deleting memories with 768-dimensional embeddings ==="

# Delete memories with 768-dimensional embeddings (wrong size)
$deleteBody = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) WHERE m.userId = `$userId AND size(m.embedding) = 768 DELETE m RETURN count(*) as deletedCount"
            parameters = @{
                userId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"
            }
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

Write-Host ""
Write-Host "=== Final Memory Status ==="

# Check final status
try {
    $finalResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $countBody
    
    if ($finalResponse.results -and $finalResponse.results[0].data) {
        foreach ($result in $finalResponse.results[0].data) {
            $row = $result.row
            Write-Host "Embedding size $($row[0]): $($row[1]) memories"
        }
    }
} catch {
    Write-Host "Error checking final status: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "Now all memories should have 384-dimensional embeddings compatible with the vector index!"
