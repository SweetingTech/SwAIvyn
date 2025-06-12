"""
Google Workspace API Service for SwAIvyn
Provides Gmail, Calendar, and Drive integration via FastAPI endpoints.
"""

import os
import io
import json
import logging
from datetime import datetime, timedelta
from pathlib import Path
from typing import Optional, List, Dict, Any

from fastapi import FastAPI, HTTPException, Form, File, UploadFile
from fastapi.responses import JSONResponse
from fastapi.middleware.cors import CORSMiddleware

# Google API imports
from google.auth.transport.requests import Request
from google.oauth2.credentials import Credentials
from google_auth_oauthlib.flow import InstalledAppFlow
from googleapiclient.discovery import build
from googleapiclient.errors import HttpError
from googleapiclient.http import MediaIoBaseDownload, MediaFileUpload

# Setup logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(title="Google Workspace API Service", version="1.0.0")

# CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure for production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Google API Scopes
SCOPES = [
    'https://www.googleapis.com/auth/gmail.readonly',
    'https://www.googleapis.com/auth/gmail.send',
    'https://www.googleapis.com/auth/gmail.compose',
    'https://www.googleapis.com/auth/calendar',
    'https://www.googleapis.com/auth/drive.file',
    'https://www.googleapis.com/auth/drive.readonly'
]

# Global credentials storage (keyed by user_id)
user_credentials: Dict[str, Credentials] = {}

class GoogleWorkspaceService:
    def __init__(self):
        self.services: Dict[str, Dict[str, Any]] = {}  # user_id -> services

    def _get_credentials(self, user_id: str = "default") -> Optional[Credentials]:
        """Get credentials for a specific user"""
        return user_credentials.get(user_id)

    def _get_services(self, user_id: str = "default") -> Dict[str, Any]:
        """Get or create Google API services for a user"""
        if user_id not in self.services:
            creds = self._get_credentials(user_id)
            if not creds:
                raise HTTPException(status_code=401, detail="No credentials found. Please authenticate first.")
            
            try:
                self.services[user_id] = {
                    'gmail': build('gmail', 'v1', credentials=creds),
                    'calendar': build('calendar', 'v3', credentials=creds),
                    'drive': build('drive', 'v3', credentials=creds)
                }
            except Exception as e:
                logger.error(f"Failed to build services for user {user_id}: {e}")
                raise HTTPException(status_code=500, detail=f"Failed to initialize Google services: {str(e)}")
        
        return self.services[user_id]

    def set_credentials(self, user_id: str, credentials_data: dict):
        """Set credentials for a user from uploaded credentials JSON"""
        try:
            # Create credentials from the provided data
            creds = Credentials(
                token=credentials_data.get('token'),
                refresh_token=credentials_data.get('refresh_token'),
                token_uri=credentials_data.get('token_uri'),
                client_id=credentials_data.get('client_id'),
                client_secret=credentials_data.get('client_secret'),
                scopes=SCOPES
            )
            
            user_credentials[user_id] = creds
            
            # Clear existing services to force rebuild with new credentials
            if user_id in self.services:
                del self.services[user_id]
                
            logger.info(f"Credentials set for user {user_id}")
            return True
        except Exception as e:
            logger.error(f"Failed to set credentials for user {user_id}: {e}")
            return False

    def remove_credentials(self, user_id: str):
        """Remove credentials for a user"""
        if user_id in user_credentials:
            del user_credentials[user_id]
        if user_id in self.services:
            del self.services[user_id]

    # Gmail Methods
    def get_gmail_messages(self, user_id: str, query: str = '', max_results: int = 10) -> List[Dict]:
        """Get Gmail messages based on query"""
        try:
            services = self._get_services(user_id)
            gmail_service = services['gmail']
            
            results = gmail_service.users().messages().list(
                userId='me', q=query, maxResults=max_results
            ).execute()
            messages = results.get('messages', [])
            
            detailed_messages = []
            for message in messages:
                msg = gmail_service.users().messages().get(
                    userId='me', id=message['id']
                ).execute()
                detailed_messages.append(msg)
            
            return detailed_messages
        except HttpError as error:
            logger.error(f"Gmail API error: {error}")
            raise HTTPException(status_code=500, detail=str(error))

    def send_gmail_message(self, user_id: str, to: str, subject: str, body: str, html: bool = False) -> Dict:
        """Send Gmail message"""
        try:
            from email.mime.text import MIMEText
            from email.mime.multipart import MIMEMultipart
            import base64

            services = self._get_services(user_id)
            gmail_service = services['gmail']

            message = MIMEMultipart() if html else MIMEText(body)
            message['to'] = to
            message['subject'] = subject
            
            if html:
                message.attach(MIMEText(body, 'html'))

            raw_message = base64.urlsafe_b64encode(message.as_bytes()).decode('utf-8')
            
            result = gmail_service.users().messages().send(
                userId='me',
                body={'raw': raw_message}
            ).execute()
            
            return result
        except HttpError as error:
            logger.error(f"Gmail send error: {error}")
            raise HTTPException(status_code=500, detail=str(error))

    # Calendar Methods
    def get_calendar_events(self, user_id: str, time_min: Optional[str] = None, max_results: int = 10) -> List[Dict]:
        """Get calendar events"""
        try:
            services = self._get_services(user_id)
            calendar_service = services['calendar']
            
            if not time_min:
                time_min = datetime.utcnow().isoformat() + 'Z'
            
            events_result = calendar_service.events().list(
                calendarId='primary',
                timeMin=time_min,
                maxResults=max_results,
                singleEvents=True,
                orderBy='startTime'
            ).execute()
            
            return events_result.get('items', [])
        except HttpError as error:
            logger.error(f"Calendar API error: {error}")
            raise HTTPException(status_code=500, detail=str(error))

    def create_calendar_event(self, user_id: str, summary: str, start_time: str, end_time: str, 
                            description: str = '', location: str = '') -> Dict:
        """Create calendar event"""
        try:
            services = self._get_services(user_id)
            calendar_service = services['calendar']
            
            event = {
                'summary': summary,
                'location': location,
                'description': description,
                'start': {
                    'dateTime': start_time,
                    'timeZone': 'UTC',
                },
                'end': {
                    'dateTime': end_time,
                    'timeZone': 'UTC',
                },
            }

            event = calendar_service.events().insert(
                calendarId='primary', body=event
            ).execute()
            
            return event
        except HttpError as error:
            logger.error(f"Calendar create error: {error}")
            raise HTTPException(status_code=500, detail=str(error))

    # Drive Methods
    def list_drive_files(self, user_id: str, folder_id: Optional[str] = None, max_results: int = 10) -> List[Dict]:
        """List Drive files"""
        try:
            services = self._get_services(user_id)
            drive_service = services['drive']
            
            query = f"'{folder_id}' in parents" if folder_id else None
            
            results = drive_service.files().list(
                pageSize=max_results,
                q=query,
                fields="nextPageToken, files(id, name, mimeType, modifiedTime, size)"
            ).execute()
            
            return results.get('files', [])
        except HttpError as error:
            logger.error(f"Drive API error: {error}")
            raise HTTPException(status_code=500, detail=str(error))

    def upload_drive_file(self, user_id: str, file_path: str, folder_id: Optional[str] = None) -> Dict:
        """Upload file to Drive"""
        try:
            services = self._get_services(user_id)
            drive_service = services['drive']
            
            file_metadata = {
                'name': Path(file_path).name
            }
            if folder_id:
                file_metadata['parents'] = [folder_id]

            media = MediaFileUpload(file_path)
            file = drive_service.files().create(
                body=file_metadata,
                media_body=media,
                fields='id'
            ).execute()
            
            return file
        except HttpError as error:
            logger.error(f"Drive upload error: {error}")
            raise HTTPException(status_code=500, detail=str(error))

# Initialize service
workspace_service = GoogleWorkspaceService()

# FastAPI Endpoints

@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {"status": "healthy", "services": ["gmail", "calendar", "drive"]}

@app.post("/auth/credentials")
async def upload_credentials(user_id: str = Form(...), credentials: str = Form(...)):
    """Upload Google credentials for a user"""
    try:
        credentials_data = json.loads(credentials)
        success = workspace_service.set_credentials(user_id, credentials_data)
        
        if success:
            return {"status": "success", "message": "Credentials uploaded successfully"}
        else:
            raise HTTPException(status_code=400, detail="Failed to set credentials")
    except json.JSONDecodeError:
        raise HTTPException(status_code=400, detail="Invalid JSON in credentials")
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.delete("/auth/credentials/{user_id}")
async def remove_credentials(user_id: str):
    """Remove credentials for a user"""
    workspace_service.remove_credentials(user_id)
    return {"status": "success", "message": "Credentials removed"}

@app.get("/auth/status/{user_id}")
async def get_auth_status(user_id: str):
    """Check authentication status for a user"""
    creds = workspace_service._get_credentials(user_id)
    return {
        "authenticated": creds is not None,
        "valid": creds.valid if creds else False,
        "expired": creds.expired if creds else None
    }

# Gmail Endpoints
@app.get("/gmail/messages")
async def get_messages(user_id: str = Form(...), query: str = '', max_results: int = 10):
    """Get Gmail messages"""
    try:
        messages = workspace_service.get_gmail_messages(user_id, query, max_results)
        return {"messages": messages}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/gmail/send")
async def send_message(
    user_id: str = Form(...),
    to: str = Form(...),
    subject: str = Form(...),
    body: str = Form(...),
    html: bool = Form(False)
):
    """Send Gmail message"""
    try:
        result = workspace_service.send_gmail_message(user_id, to, subject, body, html)
        return {"status": "sent", "message_id": result.get('id')}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

# Calendar Endpoints
@app.get("/calendar/events")
async def get_events(user_id: str = Form(...), time_min: Optional[str] = None, max_results: int = 10):
    """Get calendar events"""
    try:
        events = workspace_service.get_calendar_events(user_id, time_min, max_results)
        return {"events": events}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/calendar/events")
async def create_event(
    user_id: str = Form(...),
    summary: str = Form(...),
    start_time: str = Form(...),
    end_time: str = Form(...),
    description: str = Form(''),
    location: str = Form('')
):
    """Create calendar event"""
    try:
        event = workspace_service.create_calendar_event(
            user_id, summary, start_time, end_time, description, location
        )
        return {"status": "created", "event_id": event.get('id'), "event": event}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

# Drive Endpoints
@app.get("/drive/files")
async def list_files(user_id: str = Form(...), folder_id: Optional[str] = None, max_results: int = 10):
    """List Drive files"""
    try:
        files = workspace_service.list_drive_files(user_id, folder_id, max_results)
        return {"files": files}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/drive/upload")
async def upload_file(user_id: str = Form(...), file: UploadFile = File(...), folder_id: Optional[str] = Form(None)):
    """Upload file to Drive"""
    try:
        # Save uploaded file temporarily
        temp_path = f"temp_{file.filename}"
        with open(temp_path, "wb") as buffer:
            content = await file.read()
            buffer.write(content)
        
        # Upload to Drive
        result = workspace_service.upload_drive_file(user_id, temp_path, folder_id)
        
        # Clean up temp file
        os.remove(temp_path)
        
        return {"status": "uploaded", "file_id": result.get('id')}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

if __name__ == "__main__":
    import uvicorn
    import argparse
    
    parser = argparse.ArgumentParser(description="Google Workspace API Service")
    parser.add_argument("--host", type=str, default="127.0.0.1", help="Host to bind to")
    parser.add_argument("--port", type=int, default="8082", help="Port to bind to")
    
    args = parser.parse_args()
    
    print(f"Starting Google Workspace API service on {args.host}:{args.port}")
    print("Services: Gmail, Calendar, Drive")
    print("Note: Credentials must be uploaded per user via /auth/credentials endpoint")
    
    uvicorn.run(app, host=args.host, port=args.port)

    # Gmail Methods
    def get_gmail_messages(self, query: str = '', max_results: int = 10) -> List[Dict]:
        """Get Gmail messages based on query"""
        try:
            results = self.gmail_service.users().messages().list(
                userId='me', q=query, maxResults=max_results
            ).execute()
            messages = results.get('messages', [])
            
            detailed_messages = []
            for message in messages:
                msg = self.gmail_service.users().messages().get(
                    userId='me', id=message['id']
                ).execute()
                detailed_messages.append(msg)
            
            return detailed_messages
        except HttpError as error:
            logger.error(f"Gmail API error: {error}")
            raise HTTPException(status_code=500, detail=str(error))

    def send_gmail_message(self, to: str, subject: str, body: str, html: bool = False) -> Dict:
        """Send Gmail message"""
        try:
            from email.mime.text import MIMEText
            from email.mime.multipart import MIMEMultipart
            import base64

            message = MIMEMultipart() if html else MIMEText(body)
            message['to'] = to
            message['subject'] = subject
            
            if html:
                message.attach(MIMEText(body, 'html'))

            raw_message = base64.urlsafe_b64encode(message.as_bytes()).decode('utf-8')
            
            result = self.gmail_service.users().messages().send(
                userId='me',
                body={'raw': raw_message}
            ).execute()
            
            return result
        except HttpError as error:
            logger.error(f"Gmail send error: {error}")
            raise HTTPException(status_code=500, detail=str(error))

    # Calendar Methods
    def get_calendar_events(self, time_min: Optional[str] = None, max_results: int = 10) -> List[Dict]:
        """Get calendar events"""
        try:
            if not time_min:
                time_min = datetime.utcnow().isoformat() + 'Z'
            
            events_result = self.calendar_service.events().list(
                calendarId='primary',
                timeMin=time_min,
                maxResults=max_results,
                singleEvents=True,
                orderBy='startTime'
            ).execute()
            
            return events_result.get('items', [])
        except HttpError as error:
            logger.error(f"Calendar API error: {error}")
            raise HTTPException(status_code=500, detail=str(error))

    def create_calendar_event(self, summary: str, start_time: str, end_time: str, 
                            description: str = '', location: str = '') -> Dict:
        """Create calendar event"""
        try:
            event = {
                'summary': summary,
                'location': location,
                'description': description,
                'start': {
                    'dateTime': start_time,
                    'timeZone': 'UTC',
                },
                'end': {
                    'dateTime': end_time,
                    'timeZone': 'UTC',
                },
            }

            event = self.calendar_service.events().insert(
                calendarId='primary', body=event
            ).execute()
            
            return event
        except HttpError as error:
            logger.error(f"Calendar create error: {error}")
            raise HTTPException(status_code=500, detail=str(error))

    # Drive Methods
    def list_drive_files(self, folder_id: Optional[str] = None, max_results: int = 10) -> List[Dict]:
        """List Drive files"""
        try:
            query = f"'{folder_id}' in parents" if folder_id else None
            
            results = self.drive_service.files().list(
                pageSize=max_results,
                q=query,
                fields="nextPageToken, files(id, name, mimeType, modifiedTime, size)"
            ).execute()
            
            return results.get('files', [])
        except HttpError as error:
            logger.error(f"Drive API error: {error}")
            raise HTTPException(status_code=500, detail=str(error))

    def upload_drive_file(self, file_path: str, folder_id: Optional[str] = None) -> Dict:
        """Upload file to Drive"""
        try:
            file_metadata = {
                'name': Path(file_path).name
            }
            if folder_id:
                file_metadata['parents'] = [folder_id]

            media = MediaFileUpload(file_path)
            file = self.drive_service.files().create(
                body=file_metadata,
                media_body=media,
                fields='id'
            ).execute()
            
            return file
        except HttpError as error:
            logger.error(f"Drive upload error: {error}")
            raise HTTPException(status_code=500, detail=str(error))

# Initialize service
workspace_service = GoogleWorkspaceService()

# FastAPI Endpoints

@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {"status": "healthy", "services": ["gmail", "calendar", "drive"]}

# Gmail Endpoints
@app.get("/gmail/messages")
async def get_messages(query: str = '', max_results: int = 10):
    """Get Gmail messages"""
    try:
        messages = workspace_service.get_gmail_messages(query, max_results)
        return {"messages": messages}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/gmail/send")
async def send_message(
    to: str = Form(...),
    subject: str = Form(...),
    body: str = Form(...),
    html: bool = Form(False)
):
    """Send Gmail message"""
    try:
        result = workspace_service.send_gmail_message(to, subject, body, html)
        return {"status": "sent", "message_id": result.get('id')}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

# Calendar Endpoints
@app.get("/calendar/events")
async def get_events(time_min: Optional[str] = None, max_results: int = 10):
    """Get calendar events"""
    try:
        events = workspace_service.get_calendar_events(time_min, max_results)
        return {"events": events}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/calendar/events")
async def create_event(
    summary: str = Form(...),
    start_time: str = Form(...),
    end_time: str = Form(...),
    description: str = Form(''),
    location: str = Form('')
):
    """Create calendar event"""
    try:
        event = workspace_service.create_calendar_event(
            summary, start_time, end_time, description, location
        )
        return {"status": "created", "event_id": event.get('id'), "event": event}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

# Drive Endpoints
@app.get("/drive/files")
async def list_files(folder_id: Optional[str] = None, max_results: int = 10):
    """List Drive files"""
    try:
        files = workspace_service.list_drive_files(folder_id, max_results)
        return {"files": files}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/drive/upload")
async def upload_file(file: UploadFile = File(...), folder_id: Optional[str] = Form(None)):
    """Upload file to Drive"""
    try:
        # Save uploaded file temporarily
        temp_path = f"temp_{file.filename}"
        with open(temp_path, "wb") as buffer:
            content = await file.read()
            buffer.write(content)
        
        # Upload to Drive
        result = workspace_service.upload_drive_file(temp_path, folder_id)
        
        # Clean up temp file
        os.remove(temp_path)
        
        return {"status": "uploaded", "file_id": result.get('id')}
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

if __name__ == "__main__":
    import uvicorn
    import argparse
    
    parser = argparse.ArgumentParser(description="Google Workspace API Service")
    parser.add_argument("--host", type=str, default="127.0.0.1", help="Host to bind to")
    parser.add_argument("--port", type=int, default="8082", help="Port to bind to")
    
    args = parser.parse_args()
    
    print(f"Starting Google Workspace API service on {args.host}:{args.port}")
    print("Services: Gmail, Calendar, Drive")
    
    uvicorn.run(app, host=args.host, port=args.port)
