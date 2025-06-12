# Google Workspace API Installation Script for SwAIvyn
# Run this script to install and set up the Google Workspace integration

Write-Host "Setting up Google Workspace API integration for SwAIvyn..." -ForegroundColor Green

# Check if Python is available
try {
    $pythonVersion = python --version 2>&1
    Write-Host "Found Python: $pythonVersion" -ForegroundColor Green
} catch {
    Write-Host "Error: Python not found. Please install Python first." -ForegroundColor Red
    exit 1
}

# Navigate to the Google Workspace service directory
$workspaceDir = "services\google_workspace"
if (!(Test-Path $workspaceDir)) {
    Write-Host "Creating Google Workspace service directory..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $workspaceDir -Force
}

Set-Location $workspaceDir

# Install Python dependencies
Write-Host "Installing Python dependencies..." -ForegroundColor Yellow
try {
    pip install -r requirements.txt
    Write-Host "Dependencies installed successfully!" -ForegroundColor Green
} catch {
    Write-Host "Error installing dependencies. Trying with --user flag..." -ForegroundColor Yellow
    pip install --user -r requirements.txt
}

# Check if credentials.json exists
if (!(Test-Path "credentials.json")) {
    Write-Host "" -ForegroundColor Yellow
    Write-Host "⚠️  IMPORTANT: Google Cloud Setup Required" -ForegroundColor Yellow
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host "1. Go to https://console.cloud.google.com/" -ForegroundColor White
    Write-Host "2. Create a new project or select existing one" -ForegroundColor White
    Write-Host "3. Enable the following APIs:" -ForegroundColor White
    Write-Host "   - Gmail API" -ForegroundColor Cyan
    Write-Host "   - Google Calendar API" -ForegroundColor Cyan
    Write-Host "   - Google Drive API" -ForegroundColor Cyan
    Write-Host "4. Go to 'Credentials' → 'Create Credentials' → 'OAuth client ID'" -ForegroundColor White
    Write-Host "5. Choose 'Desktop application'" -ForegroundColor White
    Write-Host "6. Download the JSON file and rename it to 'credentials.json'" -ForegroundColor White
    Write-Host "7. Place 'credentials.json' in this directory: $((Get-Location).Path)" -ForegroundColor White
    Write-Host "" -ForegroundColor Yellow
    Write-Host "The service will start without credentials, but authentication will be required for actual use." -ForegroundColor Yellow
} else {
    Write-Host "Found credentials.json - ready for authentication!" -ForegroundColor Green
}

# Test the installation
Write-Host "" -ForegroundColor Yellow
Write-Host "Testing Google Workspace API service..." -ForegroundColor Yellow

# Start the service in background for testing
$job = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    python google_workspace_api.py --host 127.0.0.1 --port 8082
}

# Wait a moment for the service to start
Start-Sleep -Seconds 3

# Test the health endpoint
try {
    $response = Invoke-RestMethod -Uri "http://127.0.0.1:8082/health" -Method Get -TimeoutSec 5
    Write-Host "✅ Google Workspace API service is running!" -ForegroundColor Green
    Write-Host "Health check response: $($response | ConvertTo-Json)" -ForegroundColor Cyan
} catch {
    Write-Host "⚠️  Service test failed - this is expected if dependencies aren't fully installed" -ForegroundColor Yellow
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Stop the test job
Stop-Job $job -ErrorAction SilentlyContinue
Remove-Job $job -ErrorAction SilentlyContinue

Write-Host "" -ForegroundColor Yellow
Write-Host "✅ Google Workspace API integration setup complete!" -ForegroundColor Green
Write-Host "" -ForegroundColor Yellow
Write-Host "Next steps:" -ForegroundColor White
Write-Host "1. Set up Google Cloud credentials (if not done already)" -ForegroundColor White
Write-Host "2. Start your SwAIvyn backend - the Google Workspace service will auto-start" -ForegroundColor White
Write-Host "3. Test the integration using the endpoints at http://localhost:5000/api/googleworkspace" -ForegroundColor White
Write-Host "" -ForegroundColor Yellow
Write-Host "Available endpoints:" -ForegroundColor White
Write-Host "- GET  /api/googleworkspace/health" -ForegroundColor Cyan
Write-Host "- POST /api/googleworkspace/gmail/send" -ForegroundColor Cyan
Write-Host "- GET  /api/googleworkspace/gmail/messages" -ForegroundColor Cyan
Write-Host "- POST /api/googleworkspace/calendar/events" -ForegroundColor Cyan
Write-Host "- GET  /api/googleworkspace/calendar/events" -ForegroundColor Cyan
Write-Host "- GET  /api/googleworkspace/drive/files" -ForegroundColor Cyan
Write-Host "- POST /api/googleworkspace/drive/upload" -ForegroundColor Cyan

# Return to original directory
Set-Location ..\..

Write-Host "" -ForegroundColor Green
Write-Host "Setup completed! 🎉" -ForegroundColor Green
