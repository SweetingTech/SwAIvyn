try {
    $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method GET
    Write-Host "Available Ollama models:"
    foreach ($model in $response.models) {
        Write-Host "- $($model.name)"
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)"
}
