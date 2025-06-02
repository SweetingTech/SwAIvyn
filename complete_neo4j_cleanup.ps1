# Complete Neo4j cleanup - delete ALL memories and start fresh
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

Write-Host "Performing complete Neo4j cleanup..."

# Delete ALL memories
$deleteAllBody = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) DELETE m RETURN count(*) as deletedCount"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $deleteResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $deleteAllBody
    
    if ($deleteResponse.results -and $deleteResponse.results[0].data) {
        $deletedCount = $deleteResponse.results[0].data[0].row[0]
        Write-Host "Deleted $deletedCount memories from Neo4j"
    }
} catch {
    Write-Host "Error deleting memories: $($_.Exception.Message)"
}

# Verify cleanup
$verifyBody = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) RETURN count(m) as memoryCount"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $verifyResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $verifyBody
    
    if ($verifyResponse.results -and $verifyResponse.results[0].data) {
        $memoryCount = $verifyResponse.results[0].data[0].row[0]
        Write-Host "Remaining memories in Neo4j: $memoryCount"
    }
} catch {
    Write-Host "Error verifying cleanup: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "Neo4j completely cleaned! Now create a fresh memory to test."
