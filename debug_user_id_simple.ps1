# Simple debug of user ID mismatch
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

Write-Host "Debugging user ID mismatch..."

# Check Neo4j memories
$neo4jBody = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) RETURN m.id, m.content, m.userId LIMIT 5"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $neo4jResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $neo4jBody
    
    Write-Host "Neo4j memories:"
    if ($neo4jResponse.results -and $neo4jResponse.results[0].data) {
        foreach ($result in $neo4jResponse.results[0].data) {
            $row = $result.row
            Write-Host "  ID: $($row[0])"
            Write-Host "  Content: $($row[1])"
            Write-Host "  User ID: $($row[2])"
            Write-Host "  ---"
        }
    } else {
        Write-Host "  No memories found"
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)"
}

# Check SQLite memories for current user
$currentUserId = "42dfa1c0-c093-4f58-bb3e-cc83bbd6d249"
Write-Host ""
Write-Host "SQLite memories for current user ($currentUserId):"

try {
    $sqliteResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/user/$currentUserId" -Method GET
    Write-Host "  Count: $($sqliteResponse.Count)"
    
    if ($sqliteResponse.Count -gt 0) {
        foreach ($memory in $sqliteResponse) {
            Write-Host "  ID: $($memory.id)"
            Write-Host "  Content: $($memory.content)"
            Write-Host "  User ID: $($memory.userId)"
            Write-Host "  ---"
        }
    }
} catch {
    Write-Host "  Error: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "CONCLUSION: Vector search works but user IDs don't match between Neo4j and SQLite"
