# Delete all memories from both Neo4j and SQLite to start fresh
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

Write-Host "Deleting all memories to start fresh..."
Write-Host ""

# Step 1: Delete all memories from Neo4j
Write-Host "=== Step 1: Deleting all memories from Neo4j ==="
$deleteNeo4jBody = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) DELETE m RETURN count(*) as deletedCount"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $deleteNeo4jResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $deleteNeo4jBody
    
    if ($deleteNeo4jResponse.results -and $deleteNeo4jResponse.results[0].data) {
        $deletedCount = $deleteNeo4jResponse.results[0].data[0].row[0]
        Write-Host "✅ Deleted $deletedCount memories from Neo4j"
    } else {
        Write-Host "✅ No memories found in Neo4j to delete"
    }
} catch {
    Write-Host "❌ Error deleting from Neo4j: $($_.Exception.Message)"
}

# Step 2: Delete all memories from SQLite via API
Write-Host ""
Write-Host "=== Step 2: Getting all memories from SQLite ==="
$userId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"

try {
    $sqliteMemories = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/user/$userId" -Method GET
    Write-Host "Found $($sqliteMemories.Count) memories in SQLite"
    
    if ($sqliteMemories.Count -gt 0) {
        Write-Host ""
        Write-Host "=== Step 3: Deleting memories from SQLite ==="
        
        $deletedCount = 0
        foreach ($memory in $sqliteMemories) {
            try {
                $deleteResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/$($memory.id)" -Method DELETE
                $deletedCount++
                Write-Host "  Deleted memory: $($memory.id)"
            } catch {
                Write-Host "  ❌ Failed to delete memory $($memory.id): $($_.Exception.Message)"
            }
        }
        Write-Host "✅ Deleted $deletedCount memories from SQLite"
    } else {
        Write-Host "✅ No memories found in SQLite to delete"
    }
} catch {
    Write-Host "❌ Error accessing SQLite memories: $($_.Exception.Message)"
}

# Step 3: Drop and recreate the vector index
Write-Host ""
Write-Host "=== Step 4: Recreating vector index ==="
$dropIndexBody = @{
    statements = @(
        @{
            statement = "DROP INDEX memory_embeddings IF EXISTS"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $dropResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $dropIndexBody
    Write-Host "✅ Vector index dropped"
} catch {
    Write-Host "❌ Error dropping index: $($_.Exception.Message)"
}

$createIndexBody = @{
    statements = @(
        @{
            statement = "CREATE VECTOR INDEX memory_embeddings FOR (m:Memory) ON (m.embedding) OPTIONS {indexConfig: {`vector.dimensions`: 384, `vector.similarity_function`: 'cosine'}}"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $createResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $createIndexBody
    Write-Host "✅ Vector index recreated with 384 dimensions"
} catch {
    Write-Host "❌ Error creating index: $($_.Exception.Message)"
}

# Step 4: Verify cleanup
Write-Host ""
Write-Host "=== Step 5: Verification ==="

# Check Neo4j
$countNeo4jBody = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) RETURN count(m) as memoryCount"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $countNeo4jResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $countNeo4jBody
    
    if ($countNeo4jResponse.results -and $countNeo4jResponse.results[0].data) {
        $neo4jCount = $countNeo4jResponse.results[0].data[0].row[0]
        Write-Host "Neo4j memories remaining: $neo4jCount"
    }
} catch {
    Write-Host "❌ Error checking Neo4j: $($_.Exception.Message)"
}

# Check SQLite
try {
    $sqliteMemoriesAfter = Invoke-RestMethod -Uri "http://localhost:5000/api/memory/user/$userId" -Method GET
    Write-Host "SQLite memories remaining: $($sqliteMemoriesAfter.Count)"
} catch {
    Write-Host "❌ Error checking SQLite: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "🎉 Memory cleanup completed! Ready to start fresh."
