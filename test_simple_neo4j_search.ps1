# Test simple Neo4j search without vector search
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

Write-Host "Testing simple Neo4j search for memories..."

# Test 1: Get all memories
Write-Host "=== Test 1: Get all memories ==="
$body1 = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) RETURN m.id, m.content, m.userId, m.category LIMIT 10"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $response1 = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $body1
    Write-Host "All memories:"
    if ($response1.results -and $response1.results[0].data) {
        foreach ($result in $response1.results[0].data) {
            $row = $result.row
            Write-Host "  ID: $($row[0])"
            Write-Host "  Content: $($row[1])"
            Write-Host "  User ID: $($row[2])"
            Write-Host "  Category: $($row[3])"
            Write-Host "  ---"
        }
    } else {
        Write-Host "  No memories found"
    }
} catch {
    Write-Host "Error in Test 1: $($_.Exception.Message)"
}

Write-Host ""

# Test 2: Search by user ID
Write-Host "=== Test 2: Search by user ID ==="
$userId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"
$body2 = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) WHERE m.userId = `$userId RETURN m.id, m.content, m.userId, m.category"
            parameters = @{
                userId = $userId
            }
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $response2 = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $body2
    Write-Host "Memories for user ${userId}:"
    if ($response2.results -and $response2.results[0].data) {
        foreach ($result in $response2.results[0].data) {
            $row = $result.row
            Write-Host "  ID: $($row[0])"
            Write-Host "  Content: $($row[1])"
            Write-Host "  User ID: $($row[2])"
            Write-Host "  Category: $($row[3])"
            Write-Host "  ---"
        }
    } else {
        Write-Host "  No memories found for this user"
    }
} catch {
    Write-Host "Error in Test 2: $($_.Exception.Message)"
}

Write-Host ""

# Test 3: Search by content
Write-Host "=== Test 3: Search by content containing 'dog' ==="
$body3 = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) WHERE m.content CONTAINS 'dog' RETURN m.id, m.content, m.userId, m.category"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $response3 = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $body3
    Write-Host "Memories containing 'dog':"
    if ($response3.results -and $response3.results[0].data) {
        foreach ($result in $response3.results[0].data) {
            $row = $result.row
            Write-Host "  ID: $($row[0])"
            Write-Host "  Content: $($row[1])"
            Write-Host "  User ID: $($row[2])"
            Write-Host "  Category: $($row[3])"
            Write-Host "  ---"
        }
    } else {
        Write-Host "  No memories found containing 'dog'"
    }
} catch {
    Write-Host "Error in Test 3: $($_.Exception.Message)"
}
