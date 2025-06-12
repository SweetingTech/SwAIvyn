# Google Workspace API Setup Guide

## Prerequisites

1. **Google Cloud Project Setup**
   - Go to [Google Cloud Console](https://console.cloud.google.com/)
   - Create a new project or select existing one
   - Enable the following APIs:
     - Gmail API
     - Google Calendar API
     - Google Drive API

2. **OAuth 2.0 Credentials**
   - Go to "Credentials" in the Google Cloud Console
   - Click "Create Credentials" → "OAuth client ID"
   - Choose "Desktop application"
   - Download the JSON file and rename it to `credentials.json`
   - Place it in the same directory as `google_workspace_api.py`

## Installation

1. **Install Dependencies**
   ```bash
   cd services/google_workspace
   pip install -r requirements.txt
   ```

2. **Run the Service**
   ```bash
   python google_workspace_api.py --host 127.0.0.1 --port 8082
   ```

3. **First Run Authentication**
   - On first run, a browser will open for OAuth authentication
   - Grant the requested permissions
   - A `token.json` file will be created for future use

## API Endpoints

### Gmail Endpoints

- **GET** `/gmail/messages?query=&max_results=10`
  - Get Gmail messages
  - Query examples: `is:unread`, `from:example@gmail.com`, `subject:meeting`

- **POST** `/gmail/send`
  - Send Gmail message
  - Form data: `to`, `subject`, `body`, `html` (boolean)

### Calendar Endpoints

- **GET** `/calendar/events?time_min=&max_results=10`
  - Get calendar events
  - `time_min` format: ISO 8601 (e.g., `2024-01-01T00:00:00Z`)

- **POST** `/calendar/events`
  - Create calendar event
  - Form data: `summary`, `start_time`, `end_time`, `description`, `location`

### Drive Endpoints

- **GET** `/drive/files?folder_id=&max_results=10`
  - List Drive files
  - `folder_id` is optional (lists root if not provided)

- **POST** `/drive/upload`
  - Upload file to Drive
  - Form data: `file` (file upload), `folder_id` (optional)

### Health Check

- **GET** `/health`
  - Service health check

## Example Usage

### Python Client Example
```python
import requests

# Send Gmail
response = requests.post('http://localhost:8082/gmail/send', data={
    'to': 'recipient@example.com',
    'subject': 'Test from SwAIvyn',
    'body': 'Hello from the Google Workspace integration!',
    'html': False
})

# Get Calendar Events
response = requests.get('http://localhost:8082/calendar/events?max_results=5')
events = response.json()['events']

# Create Calendar Event
response = requests.post('http://localhost:8082/calendar/events', data={
    'summary': 'SwAIvyn Meeting',
    'start_time': '2024-01-15T10:00:00Z',
    'end_time': '2024-01-15T11:00:00Z',
    'description': 'Discuss project progress',
    'location': 'Conference Room A'
})
```

### JavaScript/Frontend Example
```javascript
// Send Gmail
const sendEmail = async () => {
    const formData = new FormData();
    formData.append('to', 'recipient@example.com');
    formData.append('subject', 'Test Email');
    formData.append('body', 'Hello from SwAIvyn!');
    
    const response = await fetch('http://localhost:8082/gmail/send', {
        method: 'POST',
        body: formData
    });
    
    return response.json();
};

// Get Calendar Events
const getEvents = async () => {
    const response = await fetch('http://localhost:8082/calendar/events?max_results=10');
    return response.json();
};
```

## Security Notes

1. **Production Configuration**
   - Change CORS settings in production
   - Use environment variables for sensitive data
   - Implement proper authentication/authorization

2. **Token Management**
   - `token.json` contains sensitive credentials
   - Add to `.gitignore`
   - Consider encryption for production

3. **Scopes**
   - Current scopes provide read/write access
   - Reduce scopes if only read access needed
   - Review Google's scope documentation

## Integration with SwAIvyn

This service can be integrated as a hosted service in your C# backend similar to the Fish Speech service. The API endpoints can be called from:

1. **Backend Controllers** - For server-side integration
2. **Frontend JavaScript** - For client-side functionality
3. **AI Agents** - For automated email/calendar/drive operations

## Troubleshooting

1. **Authentication Errors**
   - Ensure `credentials.json` is valid
   - Check API enablement in Google Cloud Console
   - Verify OAuth consent screen configuration

2. **Permission Errors**
   - Review requested scopes
   - Re-authenticate if scope changes
   - Check Google Workspace admin settings

3. **Rate Limiting**
   - Google APIs have usage quotas
   - Implement exponential backoff
   - Monitor usage in Google Cloud Console
