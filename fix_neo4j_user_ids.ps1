# Fix Neo4j user IDs to match current SQLite user
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

$currentUserId = "42dfa1c0-c093-4f58-bb3e-cc83bbd6d249"
$oldUserId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"

Write-Host "Fixing Neo4j user IDs to match current user: $currentUserId"
Write-Host ""

# Step 1: Delete all memories with old user ID or empty user ID
Write-Host "=== Step 1: Cleaning up old memories ==="
$deleteBody = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) WHERE m.userId = `$oldUserId OR m.userId IS NULL OR m.userId = '' DELETE m RETURN count(*) as deletedCount"
            parameters = @{
                oldUserId = $oldUserId
            }
        }
    )
} | ConvertTo-Json -Depth 4

try {
    $deleteResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $deleteBody
    
    if ($deleteResponse.results -and $deleteResponse.results[0].data) {
        $deletedCount = $deleteResponse.results[0].data[0].row[0]
        Write-Host "Deleted $deletedCount old memories from Neo4j"
    }
} catch {
    Write-Host "Error deleting old memories: $($_.Exception.Message)"
}

# Step 2: Verify remaining memories
Write-Host ""
Write-Host "=== Step 2: Verifying remaining memories ==="
$verifyBody = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) RETURN m.id, m.content, m.userId"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $verifyResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $verifyBody
    
    Write-Host "Remaining memories in Neo4j:"
    if ($verifyResponse.results -and $verifyResponse.results[0].data) {
        foreach ($result in $verifyResponse.results[0].data) {
            $row = $result.row
            Write-Host "  ID: $($row[0])"
            Write-Host "  Content: $($row[1])"
            Write-Host "  User ID: $($row[2])"
            Write-Host "  ---"
        }
    } else {
        Write-Host "  No memories remaining"
    }
} catch {
    Write-Host "Error verifying memories: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "Neo4j cleanup completed!"
Write-Host "Now test the memory search again with the correct user ID."
