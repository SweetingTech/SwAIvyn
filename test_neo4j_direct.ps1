# Test direct Neo4j query to see if memory was stored
$body = @{
    statements = @(
        @{
            statement = "MATCH (m:Memory) RETURN m.id, m.content, m.category, m.userId LIMIT 10"
        }
    )
} | ConvertTo-Json -Depth 3

$headers = @{
    "Content-Type" = "application/json"
    "Authorization" = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("neo4j:password"))
}

try {
    $response = Invoke-RestMethod -Uri "http://localhost:7474/db/neo4j/tx/commit" -Method POST -Body $body -Headers $headers
    Write-Host "Neo4j Response:"
    $response | ConvertTo-Json -Depth 5
    
    if ($response.results -and $response.results[0].data) {
        Write-Host "Found $($response.results[0].data.Count) memories in Neo4j:"
        $response.results[0].data | ForEach-Object {
            Write-Host "ID: $($_.row[0])"
            Write-Host "Content: $($_.row[1])"
            Write-Host "Category: $($_.row[2])"
            Write-Host "UserId: $($_.row[3])"
            Write-Host "---"
        }
    } else {
        Write-Host "No memories found in Neo4j"
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
