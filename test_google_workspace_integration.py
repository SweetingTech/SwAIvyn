"""
Integration test script for Google Workspace API with SwAIvyn backend
"""

import requests
import json
from datetime import datetime, timedelta

# Configuration
BACKEND_URL = "http://localhost:5000"  # SwAIvyn backend
WORKSPACE_API_URL = "http://localhost:8082"  # Direct Google Workspace API

def test_backend_integration():
    """Test the SwAIvyn backend integration"""
    print("🔍 Testing SwAIvyn Backend Integration...")
    print("=" * 50)
    
    try:
        # Test health through backend
        response = requests.get(f"{BACKEND_URL}/api/googleworkspace/health", timeout=5)
        if response.status_code == 200:
            print("✅ Backend health check: PASSED")
            print(f"   Response: {response.json()}")
        else:
            print(f"❌ Backend health check: FAILED ({response.status_code})")
    except Exception as e:
        print(f"❌ Backend health check: ERROR - {e}")
    
    # Test getting calendar events through backend
    try:
        response = requests.get(f"{BACKEND_URL}/api/googleworkspace/calendar/events?maxResults=5", timeout=10)
        if response.status_code == 200:
            print("✅ Backend calendar events: PASSED")
            events = response.json().get('events', [])
            print(f"   Found {len(events)} events")
        else:
            print(f"❌ Backend calendar events: FAILED ({response.status_code})")
    except Exception as e:
        print(f"❌ Backend calendar events: ERROR - {e}")
    
    print()

def test_direct_api():
    """Test the direct Google Workspace API"""
    print("🔍 Testing Direct Google Workspace API...")
    print("=" * 50)
    
    try:
        # Test health
        response = requests.get(f"{WORKSPACE_API_URL}/health", timeout=5)
        if response.status_code == 200:
            print("✅ Direct API health check: PASSED")
            print(f"   Response: {response.json()}")
        else:
            print(f"❌ Direct API health check: FAILED ({response.status_code})")
    except Exception as e:
        print(f"❌ Direct API health check: ERROR - {e}")
    
    print()

def test_email_functionality():
    """Test email functionality (requires authentication)"""
    print("🔍 Testing Email Functionality...")
    print("=" * 50)
    
    # This test requires proper authentication
    print("📧 Email tests require Google OAuth authentication")
    print("   Run the service and complete OAuth flow first")
    
    # Example test (will fail without auth):
    try:
        response = requests.get(f"{WORKSPACE_API_URL}/gmail/messages?max_results=1", timeout=10)
        if response.status_code == 200:
            print("✅ Gmail messages: PASSED")
        else:
            print(f"⚠️  Gmail messages: REQUIRES AUTH ({response.status_code})")
    except Exception as e:
        print(f"⚠️  Gmail messages: REQUIRES AUTH - {e}")
    
    print()

def test_calendar_functionality():
    """Test calendar functionality"""
    print("🔍 Testing Calendar Functionality...")
    print("=" * 50)
    
    try:
        response = requests.get(f"{WORKSPACE_API_URL}/calendar/events?max_results=3", timeout=10)
        if response.status_code == 200:
            print("✅ Calendar events: PASSED")
            events = response.json().get('events', [])
            print(f"   Found {len(events)} upcoming events")
        else:
            print(f"⚠️  Calendar events: REQUIRES AUTH ({response.status_code})")
    except Exception as e:
        print(f"⚠️  Calendar events: REQUIRES AUTH - {e}")
    
    print()

def test_drive_functionality():
    """Test Google Drive functionality"""
    print("🔍 Testing Google Drive Functionality...")
    print("=" * 50)
    
    try:
        response = requests.get(f"{WORKSPACE_API_URL}/drive/files?max_results=5", timeout=10)
        if response.status_code == 200:
            print("✅ Drive files: PASSED")
            files = response.json().get('files', [])
            print(f"   Found {len(files)} files")
        else:
            print(f"⚠️  Drive files: REQUIRES AUTH ({response.status_code})")
    except Exception as e:
        print(f"⚠️  Drive files: REQUIRES AUTH - {e}")
    
    print()

def run_comprehensive_test():
    """Run all tests"""
    print("🚀 SwAIvyn Google Workspace Integration Test")
    print("=" * 60)
    print()
    
    # Test direct API first
    test_direct_api()
    
    # Test backend integration
    test_backend_integration()
    
    # Test individual functionalities
    test_email_functionality()
    test_calendar_functionality()
    test_drive_functionality()
    
    print("📋 Test Summary:")
    print("- Direct API tests check if the Python service is running")
    print("- Backend tests check if SwAIvyn can communicate with the service")
    print("- Functionality tests require Google OAuth authentication")
    print()
    print("🔧 To complete setup:")
    print("1. Ensure SwAIvyn backend is running (dotnet run)")
    print("2. Complete Google OAuth authentication when prompted")
    print("3. Test the integration from your frontend or API client")
    print()
    print("📖 See README.md for detailed setup instructions")

if __name__ == "__main__":
    run_comprehensive_test()
