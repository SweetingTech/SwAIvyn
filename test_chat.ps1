$body = @{
    conversationId = "00000000-0000-0000-0000-000000000001"
    userId = "b6c84268-f928-41c2-adf2-ae5c1bb2e3f0"
    message = "What is my dog name?"
    characterId = "22960f0f-4f0c-4766-92d3-93d078331f3b"
} | ConvertTo-Json

$headers = @{
    "Content-Type" = "application/json"
}

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/conversation/chat" -Method POST -Body $body -Headers $headers
    Write-Host "Success: $($response | ConvertTo-Json -Depth 3)"
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    Write-Host "Response: $($_.Exception.Response)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
