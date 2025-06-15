#!/usr/bin/env python3
"""
Test script for Fish Speech TTS API
Tests voice cloning with Jazzy's voice
"""

import requests
import os
from pathlib import Path

def test_fish_speech_tts():
    """Test the Fish Speech TTS API with voice cloning."""
    
    # API endpoint
    api_url = "http://localhost:8081"
    
    # Test text
    text = "the quick brown fox jumps over the lazy dog"
    voice_name = "glados"
    
    # Output directory
    output_dir = Path("outputs")
    output_dir.mkdir(exist_ok=True)
    output_file = output_dir / f"test_jazzy_{text.replace(' ', '_')}.wav"
    
    print(f"🎤 Testing Fish Speech TTS API...")
    print(f"📝 Text: '{text}'")
    print(f"🎭 Voice: {voice_name}")
    print(f"💾 Output: {output_file}")
    
    try:
        # First, check if the API is running
        print("\n🔍 Checking API health...")
        health_response = requests.get(f"{api_url}/health", timeout=10)
        health_data = health_response.json()
        
        print(f"✅ API Status: {health_data['status']}")
        print(f"🎭 Voices loaded: {health_data['voices_loaded']}")
        print(f"🖥️  Device: {health_data['device']}")
        print(f"🤖 Models loaded: {health_data['models_loaded']}")
        
        if not health_data['models_loaded']:
            print("❌ Models not loaded! Please wait for the server to finish loading.")
            return False
        
        # Get available voices
        print("\n🎭 Getting available voices...")
        voices_response = requests.get(f"{api_url}/voices", timeout=10)
        voices_data = voices_response.json()
        available_voices = voices_data['voices']
        
        print(f"Available voices: {available_voices}")
        
        if voice_name not in available_voices:
            print(f"❌ Voice '{voice_name}' not found! Available: {available_voices}")
            return False
        
        # Make TTS request with voice cloning
        print(f"\n🎵 Generating speech with {voice_name}'s voice...")
        tts_data = {
            'text': text,
            'voice_name': voice_name
        }
        
        tts_response = requests.post(
            f"{api_url}/tts/clone", 
            data=tts_data, 
            timeout=120  # Give it time to generate
        )
        
        if tts_response.status_code == 200:
            # Save the audio file
            with open(output_file, 'wb') as f:
                f.write(tts_response.content)
            
            file_size = len(tts_response.content)
            print(f"✅ Success! Generated {file_size} bytes of audio")
            print(f"💾 Saved to: {output_file}")
            
            # Verify file exists and has content
            if output_file.exists() and output_file.stat().st_size > 0:
                print(f"📁 File verified: {output_file.stat().st_size} bytes")
                return True
            else:
                print("❌ File was not created properly")
                return False
        else:
            print(f"❌ TTS request failed: {tts_response.status_code}")
            print(f"Error: {tts_response.text}")
            return False
            
    except requests.exceptions.ConnectionError:
        print("❌ Cannot connect to Fish Speech API. Is the server running on localhost:8081?")
        return False
    except requests.exceptions.Timeout:
        print("❌ Request timed out. The model might still be loading or processing.")
        return False
    except Exception as e:
        print(f"❌ Unexpected error: {e}")
        return False

if __name__ == "__main__":
    success = test_fish_speech_tts()
    if success:
        print("\n🎉 Test completed successfully!")
    else:
        print("\n💥 Test failed!")
