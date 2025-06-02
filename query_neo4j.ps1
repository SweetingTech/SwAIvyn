$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

$body = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) WHERE m.id = 'f91b6b6a-847f-44d2-842e-c6706601134b' RETURN m.id, m.content, m.userId, size(m.embedding) as embeddingSize, m.embedding[0..5] as embeddingSample"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $body
    Write-Host "Query Results:"
    $response | ConvertTo-Json -Depth 5
} catch {
    Write-Host "Error: $($_.Exception.Message)"
}
