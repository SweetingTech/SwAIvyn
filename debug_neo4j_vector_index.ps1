# Debug Neo4j vector index
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

Write-Host "Debugging Neo4j vector index..."

# Test 1: Check Neo4j version
Write-Host "=== Neo4j Version ==="
$versionBody = @{
    statements = @(
        @{
            statement = "CALL dbms.components() YIELD name, versions, edition RETURN name, versions[0] as version, edition"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $versionResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $versionBody
    
    if ($versionResponse.results -and $versionResponse.results[0].data) {
        foreach ($result in $versionResponse.results[0].data) {
            $row = $result.row
            Write-Host "$($row[0]): $($row[1]) ($($row[2]))"
        }
    }
} catch {
    Write-Host "Error getting version: $($_.Exception.Message)"
}

# Test 2: Check vector index details
Write-Host ""
Write-Host "=== Vector Index Details ==="
$indexBody = @{
    statements = @(
        @{
            statement = "SHOW INDEXES YIELD name, type, entityType, labelsOrTypes, properties, options WHERE name = 'memory_embeddings'"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $indexResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $indexBody
    
    if ($indexResponse.results -and $indexResponse.results[0].data) {
        $result = $indexResponse.results[0].data[0]
        $row = $result.row
        Write-Host "Name: $($row[0])"
        Write-Host "Type: $($row[1])"
        Write-Host "Entity Type: $($row[2])"
        Write-Host "Labels/Types: $($row[3])"
        Write-Host "Properties: $($row[4])"
        Write-Host "Options: $($row[5])"
    }
} catch {
    Write-Host "Error getting index details: $($_.Exception.Message)"
}

# Test 3: Check if memories have the embedding property
Write-Host ""
Write-Host "=== Memory Embedding Properties ==="
$embeddingCheckBody = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) WHERE m.embedding IS NOT NULL RETURN m.id, size(m.embedding) as embeddingSize, m.embedding[0..2] as firstThreeValues LIMIT 3"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $embeddingCheckResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $embeddingCheckBody
    
    if ($embeddingCheckResponse.results -and $embeddingCheckResponse.results[0].data) {
        Write-Host "Memories with embeddings:"
        foreach ($result in $embeddingCheckResponse.results[0].data) {
            $row = $result.row
            Write-Host "  ID: $($row[0])"
            Write-Host "  Embedding Size: $($row[1])"
            Write-Host "  First 3 values: $($row[2] -join ', ')"
            Write-Host "  ---"
        }
    } else {
        Write-Host "❌ No memories found with embeddings"
    }
} catch {
    Write-Host "Error checking embeddings: $($_.Exception.Message)"
}

# Test 4: Try alternative vector search syntax
Write-Host ""
Write-Host "=== Testing Alternative Vector Search Syntax ==="

# Create a simple test vector
$testVector = @(0.1, 0.2, 0.3) + @(0.0) * 381  # 384 dimensions total

$altSearchBody = @{
    statements = @(
        @{
            statement = "CALL db.index.vector.queryNodes('memory_embeddings', 1, `$testVector) YIELD node RETURN count(node) as nodeCount"
            parameters = @{
                testVector = $testVector
            }
        }
    )
} | ConvertTo-Json -Depth 4

try {
    $altSearchResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $altSearchBody
    
    if ($altSearchResponse.results -and $altSearchResponse.results[0].data) {
        $count = $altSearchResponse.results[0].data[0].row[0]
        Write-Host "Vector search returned $count nodes"
    }
} catch {
    Write-Host "Error with alternative search: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
