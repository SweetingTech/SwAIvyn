# Test memory creation with proper JSON format
$body = @{
    Content = "My dog is named Cujo and he loves to play fetch"
    Category = "Personal"
    IsShared = $false
} | ConvertTo-Json

$headers = @{
    "Content-Type" = "application/json"
}

Write-Host "Testing memory creation..."
Write-Host "Request body: $body"

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/memory" -Method POST -Body $body -Headers $headers
    Write-Host "✅ Success: Memory created"
    Write-Host "Memory ID: $($response.id)"
    Write-Host "Content: $($response.content)"
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
