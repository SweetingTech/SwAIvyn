# Debug the user ID mismatch between Neo4j and SQLite
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

Write-Host "🔍 Debugging user ID mismatch..."
Write-Host ""

# Check what's in Neo4j
Write-Host "=== Neo4j Memories ==="
$neo4jBody = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) RETURN m.id, m.content, m.userId ORDER BY m.id LIMIT 10"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $neo4jResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $neo4jBody
    
    if ($neo4jResponse.results -and $neo4jResponse.results[0].data) {
        Write-Host "Neo4j memories:"
        foreach ($result in $neo4jResponse.results[0].data) {
            $row = $result.row
            Write-Host "  ID: $($row[0])"
            Write-Host "  Content: $($row[1])"
            Write-Host "  User ID: $($row[2])"
            Write-Host "  ---"
        }
    } else {
        Write-Host "No memories found in Neo4j"
    }
} catch {
    Write-Host "Error checking Neo4j: $($_.Exception.Message)"
}

Write-Host ""

# Check what's in SQLite
Write-Host "=== SQLite Memories ==="
$currentUserId = "42dfa1c0-c093-4f58-bb3e-cc83bbd6d249"  # Current user ID
$oldUserId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"      # Old user ID

Write-Host "Checking for current user ID: $currentUserId"
try {
    $sqliteResponse1 = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/user/$currentUserId" -Method GET
    Write-Host "SQLite memories for current user: $($sqliteResponse1.Count)"
    
    if ($sqliteResponse1.Count -gt 0) {
        foreach ($memory in $sqliteResponse1) {
            Write-Host "  ID: $($memory.id)"
            Write-Host "  Content: $($memory.content)"
            Write-Host "  User ID: $($memory.userId)"
            Write-Host "  ---"
        }
    }
} catch {
    Write-Host "Error checking SQLite for current user: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "Checking for old user ID: $oldUserId"
try {
    $sqliteResponse2 = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/user/$oldUserId" -Method GET
    Write-Host "SQLite memories for old user: $($sqliteResponse2.Count)"
    
    if ($sqliteResponse2.Count -gt 0) {
        foreach ($memory in $sqliteResponse2) {
            Write-Host "  ID: $($memory.id)"
            Write-Host "  Content: $($memory.content)"
            Write-Host "  User ID: $($memory.userId)"
            Write-Host "  ---"
        }
    }
} catch {
    Write-Host "Error checking SQLite for old user: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "DIAGNOSIS:"
Write-Host "The vector search is working in Neo4j, but there is a user ID mismatch"
Write-Host "between Neo4j (old user ID) and SQLite (new user ID)."
Write-Host "This causes the final memory lookup to fail even though vector search succeeds."
