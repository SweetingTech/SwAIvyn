# Test if embeddings are stored in Neo4j
$body = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory {id: 'b1e7586d-4966-420a-a1b3-b9c7c7fbcf31'}) RETURN m.id, m.content, m.embedding IS NOT NULL as hasEmbedding, size(m.embedding) as embeddingSize"
        }
    )
} | ConvertTo-Json -Depth 3

$headers = @{
    "Content-Type" = "application/json"
    "Authorization" = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("neo4j:password"))
}

try {
    $response = Invoke-RestMethod -Uri "http://localhost:7474/db/neo4j/tx/commit" -Method POST -Body $body -Headers $headers
    Write-Host "Neo4j Embedding Check Response:"
    $response | ConvertTo-Json -Depth 5
    
    if ($response.results -and $response.results[0].data) {
        Write-Host "Memory embedding details:"
        $response.results[0].data | ForEach-Object {
            Write-Host "ID: $($_.row[0])"
            Write-Host "Content: $($_.row[1])"
            Write-Host "Has Embedding: $($_.row[2])"
            Write-Host "Embedding Size: $($_.row[3])"
            Write-Host "---"
        }
    } else {
        Write-Host "Memory not found or no embedding data"
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
