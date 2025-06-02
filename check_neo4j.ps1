# Check Neo4j for memories
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

$body = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) RETURN m.id, m.content, m.userId LIMIT 10"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $body
    Write-Host "Neo4j Response:"
    $response | ConvertTo-Json -Depth 5
} catch {
    Write-Host "Error: $($_.Exception.Message)"
}

# Also check indexes
$indexBody = @{
    statements = @(
        @{
            statement = "SHOW INDEXES"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $indexResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $indexBody
    Write-Host "`nIndexes:"
    $indexResponse | ConvertTo-Json -Depth 5
} catch {
    Write-Host "Index Error: $($_.Exception.Message)"
}
