$body = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) WHERE m.id = '79927fd1-656f-4e74-a9b6-88082a5f6414' RETURN m.id, m.content, m.userId, size(m.embedding) as embeddingSize"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod -Uri "http://localhost:7474/db/neo4j/tx/commit" -Method POST -Headers @{"Content-Type"="application/json"} -Body $body
    Write-Host "Neo4j Query Results:"
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "Error querying Neo4j: $($_.Exception.Message)"
}
