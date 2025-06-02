# Check if memories have embeddings
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

Write-Host "Checking if memories have embeddings..."

# Check a specific memory that we know exists
$memoryId = "f91b6b6a-847f-44d2-842e-c6706601134b"

$body = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) WHERE m.id = `$memoryId RETURN m.id, m.content, m.userId, m.embedding, size(m.embedding) as embeddingSize"
            parameters = @{
                memoryId = $memoryId
            }
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $body
    Write-Host "Memory details:"
    Write-Host "=============="
    
    if ($response.results -and $response.results[0].data) {
        $result = $response.results[0].data[0]
        $row = $result.row
        Write-Host "ID: $($row[0])"
        Write-Host "Content: $($row[1])"
        Write-Host "User ID: $($row[2])"
        Write-Host "Has Embedding: $($row[3] -ne $null)"
        if ($row[3] -ne $null) {
            Write-Host "Embedding Size: $($row[4])"
            Write-Host "First few embedding values: $($row[3][0..4] -join ', ')"
        } else {
            Write-Host "❌ NO EMBEDDING FOUND!"
        }
    } else {
        Write-Host "Memory not found"
    }
    
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}

Write-Host ""
Write-Host "Checking all memories for embeddings..."

$body2 = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) WHERE m.userId = `$userId RETURN m.id, m.content, m.embedding IS NOT NULL as hasEmbedding, size(m.embedding) as embeddingSize ORDER BY m.id LIMIT 10"
            parameters = @{
                userId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"
            }
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $response2 = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $body2
    Write-Host "Memory embedding status:"
    Write-Host "======================="
    
    if ($response2.results -and $response2.results[0].data) {
        foreach ($result in $response2.results[0].data) {
            $row = $result.row
            Write-Host "ID: $($row[0])"
            Write-Host "  Content: $($row[1].Substring(0, [Math]::Min(50, $row[1].Length)))..."
            Write-Host "  Has Embedding: $($row[2])"
            if ($row[2]) {
                Write-Host "  Embedding Size: $($row[3])"
            }
            Write-Host "  ---"
        }
    } else {
        Write-Host "No memories found"
    }
    
} catch {
    Write-Host "Error: $($_.Exception.Message)"
}
