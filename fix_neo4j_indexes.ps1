# Script to drop and recreate Neo4j vector indexes with correct dimensions

# Drop existing vector indexes
$dropQueries = @(
    "DROP INDEX memory_embedding_vector IF EXISTS",
    "DROP INDEX memory_embeddings IF EXISTS"
)

$headers = @{
    "Content-Type" = "application/json"
    "Authorization" = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("neo4j:password"))
}

Write-Host "Dropping existing vector indexes..."

foreach ($query in $dropQueries) {
    $body = @{
        statements = @(
            @{
                statement = $query
            }
        )
    } | ConvertTo-Json -Depth 3

    try {
        $response = Invoke-RestMethod -Uri "http://localhost:7474/db/neo4j/tx/commit" -Method POST -Body $body -Headers $headers
        Write-Host "✓ Executed: $query"
    } catch {
        Write-Host "⚠ Failed to execute: $query - $($_.Exception.Message)"
    }
}

Write-Host "Vector indexes dropped. The application will recreate them with correct dimensions on next startup."
Write-Host "Please restart the application to recreate the indexes with 768 dimensions."
