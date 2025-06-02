# Test current embedding service to see what size it returns
Write-Host "Testing current embedding service..."

$embeddingBody = @{
    model = "nomic-embed-text"
    prompt = "My dog name is Cujo"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri 'http://localhost:11434/api/embeddings' -Method POST -Headers @{'Content-Type' = 'application/json'} -Body $embeddingBody
    Write-Host "✅ Embedding generated successfully"
    Write-Host "Embedding dimensions: $($response.embedding.Length)"
    Write-Host "First few values: $($response.embedding[0..4] -join ', ')"
    
    if ($response.embedding.Length -eq 384) {
        Write-Host "✅ Correct size for Neo4j vector index (384)"
    } elseif ($response.embedding.Length -eq 768) {
        Write-Host "❌ Wrong size for Neo4j vector index (768 instead of 384)"
    } else {
        Write-Host "❓ Unexpected embedding size: $($response.embedding.Length)"
    }
    
} catch {
    Write-Host "❌ Error generating embedding: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
