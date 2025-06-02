try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/memory?userId=b6c84268-f928-41c2-adf2-ae5c1bb2e3f0" -Method GET
    Write-Host "Success: Retrieved $($response.memories.Count) memories"
    $response.memories | ForEach-Object {
        Write-Host "Memory ID: $($_.id)"
        Write-Host "Content: $($_.content)"
        Write-Host "Category: $($_.category)"
        Write-Host "Created: $($_.createdAt)"
        Write-Host "---"
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response Body: $responseBody"
    }
}
