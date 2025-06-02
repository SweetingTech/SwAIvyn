# Check what indexes exist in Neo4j
$headers = @{
    'Content-Type' = 'application/json'
    'Authorization' = 'Basic bmVvNGo6cGFzc3dvcmQ='
}

Write-Host "Checking Neo4j indexes..."

$body = @{
    statements = @(
        @{
            statement = "SHOW INDEXES"
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $response = Invoke-RestMethod -Uri 'http://localhost:7474/db/neo4j/tx/commit' -Method POST -Headers $headers -Body $body
    Write-Host "Neo4j Indexes:"
    Write-Host "=============="
    
    if ($response.results -and $response.results[0].data) {
        $indexes = $response.results[0].data
        Write-Host "Found $($indexes.Count) indexes:"
        Write-Host ""
        
        foreach ($index in $indexes) {
            $row = $index.row
            Write-Host "Index Name: $($row[1])"
            Write-Host "Type: $($row[2])"
            Write-Host "Entity Type: $($row[3])"
            Write-Host "Labels/Types: $($row[4])"
            Write-Host "Properties: $($row[5])"
            Write-Host "State: $($row[0])"
            Write-Host "---"
        }
    } else {
        Write-Host "No indexes found"
    }
    
    Write-Host ""
    Write-Host "Raw response:"
    $response | ConvertTo-Json -Depth 5
    
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
