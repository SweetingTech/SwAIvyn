# Test available embedding model
Write-Host "Testing available embedding model..."

$embeddingBody = @{
    model = "all-minilm"
    prompt = "My dog name is Cujo"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri 'http://localhost:11434/api/embeddings' -Method POST -Headers @{'Content-Type' = 'application/json'} -Body $embeddingBody
    Write-Host "Embedding generated successfully"
    Write-Host "Embedding dimensions: $($response.embedding.Length)"
    Write-Host "First few values: $($response.embedding[0..4] -join ', ')"
    
    if ($response.embedding.Length -eq 384) {
        Write-Host "Correct size for Neo4j vector index (384)"
    } elseif ($response.embedding.Length -eq 768) {
        Write-Host "Wrong size for Neo4j vector index (768 instead of 384)"
    } else {
        Write-Host "Unexpected embedding size: $($response.embedding.Length)"
    }
    
} catch {
    Write-Host "Error generating embedding: $($_.Exception.Message)"
}
