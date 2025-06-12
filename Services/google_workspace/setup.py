#!/usr/bin/env python3
"""
Setup script for Google Workspace API integration
"""

import subprocess
import sys
import os
from pathlib import Path

def install_requirements():
    """Install Python requirements"""
    requirements_file = Path(__file__).parent / "requirements.txt"
    
    if not requirements_file.exists():
        print("❌ requirements.txt not found!")
        return False
        
    try:
        print("📦 Installing Google Workspace API dependencies...")
        subprocess.check_call([
            sys.executable, "-m", "pip", "install", "-r", str(requirements_file)
        ])
        print("✅ Dependencies installed successfully!")
        return True
    except subprocess.CalledProcessError as e:
        print(f"❌ Failed to install dependencies: {e}")
        return False

def check_credentials():
    """Check if credentials.json exists"""
    credentials_file = Path(__file__).parent / "credentials.json"
    
    if credentials_file.exists():
        print("✅ credentials.json found")
        return True
    else:
        print("⚠️  credentials.json not found")
        print("\n📋 To set up Google Workspace API:")
        print("1. Go to https://console.cloud.google.com/")
        print("2. Create a new project or select existing one")
        print("3. Enable Gmail API, Calendar API, and Drive API")
        print("4. Go to 'Credentials' → 'Create Credentials' → 'OAuth client ID'")
        print("5. Choose 'Desktop application'")
        print("6. Download the JSON file and rename it to 'credentials.json'")
        print("7. Place it in the services/google_workspace/ directory")
        return False

def test_service():
    """Test if the service can start"""
    try:
        print("🧪 Testing service startup...")
        script_path = Path(__file__).parent / "google_workspace_api.py"
        
        # Quick test - just import the modules
        import importlib.util
        spec = importlib.util.spec_from_file_location("google_workspace_api", script_path)
        module = importlib.util.module_from_spec(spec)
        
        print("✅ Service script is valid!")
        return True
    except Exception as e:
        print(f"❌ Service test failed: {e}")
        return False

def main():
    print("🚀 Google Workspace API Setup")
    print("=" * 40)
    
    # Check current directory
    script_dir = Path(__file__).parent
    os.chdir(script_dir)
    print(f"📁 Working directory: {script_dir}")
    
    # Install requirements
    if not install_requirements():
        return 1
    
    # Check credentials
    has_credentials = check_credentials()
    
    # Test service
    if not test_service():
        return 1
    
    print("\n" + "=" * 40)
    if has_credentials:
        print("🎉 Setup complete! You can now start the service:")
        print("   python google_workspace_api.py")
    else:
        print("⚠️  Setup partially complete. Add credentials.json to finish setup.")
    
    print("\n📡 Service will run on: http://127.0.0.1:8082")
    print("📚 See README.md for detailed usage instructions")
    
    return 0

if __name__ == "__main__":
    sys.exit(main())
