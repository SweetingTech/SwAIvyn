"""
Simple test script for Google Workspace API integration
"""

import requests
import json
from datetime import datetime, timedelta

BASE_URL = "http://localhost:8082"

def test_health():
    """Test health endpoint"""
    response = requests.get(f"{BASE_URL}/health")
    print("Health Check:", response.json())

def test_gmail_messages():
    """Test Gmail message retrieval"""
    try:
        response = requests.get(f"{BASE_URL}/gmail/messages?max_results=5")
        if response.status_code == 200:
            data = response.json()
            print(f"Found {len(data['messages'])} Gmail messages")
        else:
            print(f"Gmail messages error: {response.status_code}")
    except Exception as e:
        print(f"Gmail test error: {e}")

def test_calendar_events():
    """Test Calendar events retrieval"""
    try:
        response = requests.get(f"{BASE_URL}/calendar/events?max_results=5")
        if response.status_code == 200:
            data = response.json()
            print(f"Found {len(data['events'])} calendar events")
            for event in data['events'][:3]:  # Show first 3
                print(f"  - {event.get('summary', 'No title')}")
        else:
            print(f"Calendar events error: {response.status_code}")
    except Exception as e:
        print(f"Calendar test error: {e}")

def test_drive_files():
    """Test Drive files listing"""
    try:
        response = requests.get(f"{BASE_URL}/drive/files?max_results=5")
        if response.status_code == 200:
            data = response.json()
            print(f"Found {len(data['files'])} Drive files")
            for file in data['files'][:3]:  # Show first 3
                print(f"  - {file.get('name')} ({file.get('mimeType')})")
        else:
            print(f"Drive files error: {response.status_code}")
    except Exception as e:
        print(f"Drive test error: {e}")

def test_send_email():
    """Test sending email (optional - uncomment to use)"""
    # Uncomment and modify to test email sending
    """
    try:
        data = {
            'to': 'douglas.j.sweeting@gmail.com',
            'subject': 'Test from SwAIvyn Google Workspace Integration',
            'body': 'This is a test email sent via the SwAIvyn Google Workspace API service.',
            'html': False
        }
        response = requests.post(f"{BASE_URL}/gmail/send", data=data)
        if response.status_code == 200:
            print("Email sent successfully!")
        else:
            print(f"Email send error: {response.status_code}")
    except Exception as e:
        print(f"Email send test error: {e}")
    """
    print("Email sending test disabled (uncomment in code to enable)")

def test_create_calendar_event():
    """Test creating calendar event (optional - uncomment to use)"""
    # Uncomment and modify to test calendar event creation
    """
    try:
        start_time = (datetime.utcnow() + timedelta(days=1)).isoformat() + 'Z'
        end_time = (datetime.utcnow() + timedelta(days=1, hours=1)).isoformat() + 'Z'
        
        data = {
            'summary': 'SwAIvyn Test Event',
            'start_time': start_time,
            'end_time': end_time,
            'description': 'Test event created by SwAIvyn Google Workspace integration',
            'location': 'Virtual'
        }
        response = requests.post(f"{BASE_URL}/calendar/events", data=data)
        if response.status_code == 200:
            print("Calendar event created successfully!")
        else:
            print(f"Calendar event creation error: {response.status_code}")
    except Exception as e:
        print(f"Calendar event test error: {e}")
    """
    print("Calendar event creation test disabled (uncomment in code to enable)")

if __name__ == "__main__":
    print("Testing Google Workspace API Integration...")
    print("=" * 50)
    
    test_health()
    print()
    
    test_gmail_messages()
    print()
    
    test_calendar_events()
    print()
    
    test_drive_files()
    print()
    
    test_send_email()
    print()
    
    test_create_calendar_event()
    print()
    
    print("Test completed!")
    print("\nNote: Some tests are disabled by default to prevent accidental actions.")
    print("Uncomment the relevant code sections to test email sending and event creation.")
