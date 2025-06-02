$headers = @{
    "Content-Type" = "application/json"
    "Authorization" = "Basic bmVvNGo6cGFzc3dvcmQ="
}

$body = @{
    statements = @(
        @{
            statement = "SHOW INDEXES YIELD name, type, state WHERE name = 'memory_embeddings'"
            parameters = @{
                userId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"
            }
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod -Uri "http://localhost:7474/db/neo4j/tx/commit" -Method POST -Headers $headers -Body $body
    Write-Host "Neo4j Query Results:"
    Write-Host ($response | ConvertTo-Json -Depth 5)
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
