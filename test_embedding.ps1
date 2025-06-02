$body = @{
    model = "all-minilm"
    prompt = "My dog name is Cujo"
} | ConvertTo-Json

$headers = @{
    "Content-Type" = "application/json"
}

try {
    Write-Host "Testing embedding API..."
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/embeddings" -Method POST -Body $body -Headers $headers
    Write-Host "Embedding API works! Embedding dimensions: $($response.embedding.Length)"
    Write-Host "First 5 values: $($response.embedding[0..4] -join ', ')"
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
