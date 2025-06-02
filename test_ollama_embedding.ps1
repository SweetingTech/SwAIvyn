$headers = @{
    'Content-Type' = 'application/json'
}

$body = @{
    model = "all-minilm:latest"
    prompt = "My dog name is Cujo"
} | ConvertTo-Json

try {
    Write-Host "Testing with body: $body"
    $response = Invoke-RestMethod -Uri 'http://localhost:11434/api/embeddings' -Method POST -Headers $headers -Body $body
    Write-Host "Embedding API Response:"
    Write-Host "Model: $($response.model)"
    Write-Host "Embedding length: $($response.embedding.Length)"
    Write-Host "First 10 values: $($response.embedding[0..9] -join ', ')"
    Write-Host "All zeros check: $(($response.embedding | Where-Object { $_ -ne 0 }).Count) non-zero values"
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    Write-Host "Response: $($_.Exception.Response)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
