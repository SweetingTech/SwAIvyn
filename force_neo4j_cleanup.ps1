# Force Neo4j cleanup with multiple approaches
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

Write-Host "Force cleaning Neo4j..."

# Try different deletion approaches
$approaches = @(
    "MATCH (n) DETACH DELETE n",
    "MATCH (m:Memory) DETACH DELETE m",
    "MATCH (m:Memory) DELETE m"
)

foreach ($query in $approaches) {
    Write-Host "Trying: $query"
    
    $body = @{
        statements = @(
            @{
                statement = $query
            }
        )
    } | ConvertTo-Json -Depth 3

    try {
        $response = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $body
        Write-Host "  Success"
        
        # Check count after each attempt
        $countBody = @{
            statements = @(
                @{
                    statement = "MATCH (m:Memory) RETURN count(m) as count"
                }
            )
        } | ConvertTo-Json -Depth 3
        
        $countResponse = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $countBody
        if ($countResponse.results -and $countResponse.results[0].data) {
            $count = $countResponse.results[0].data[0].row[0]
            Write-Host "  Remaining memories: $count"
            
            if ($count -eq 0) {
                Write-Host "  SUCCESS! All memories deleted."
                break
            }
        }
    } catch {
        Write-Host "  Error: $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "Cleanup completed. Now test with a fresh memory."
