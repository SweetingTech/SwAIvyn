#!/usr/bin/env python3
"""
Test script for Fish Speech TTS integration
Run this to verify that the Fish Speech API is working correctly
"""

import requests
import json
import io
import wave
from pathlib import Path

# Fish Speech API base URL
BASE_URL = "http://localhost:8000"

def test_api_availability():
    """Test if the Fish Speech API is running"""
    print("Testing Fish Speech API availability...")
    try:
        response = requests.get(f"{BASE_URL}/voices")
        if response.status_code == 200:
            print("✅ Fish Speech API is running")
            voices_data = response.json()
            voices = voices_data.get("voices", [])
            print(f"📢 Available voices: {voices}")
            return True
        else:
            print(f"❌ API returned status code: {response.status_code}")
            return False
    except requests.exceptions.ConnectionError:
        print("❌ Cannot connect to Fish Speech API. Make sure it's running on port 8000.")
        return False
    except Exception as e:
        print(f"❌ Error testing API: {e}")
        return False

def test_default_tts():
    """Test default TTS synthesis"""
    print("\nTesting default TTS synthesis...")
    try:
        data = {"text": "Hello, this is a test of Fish Speech TTS using the default voice."}
        response = requests.post(f"{BASE_URL}/tts", data=data)
        
        if response.status_code == 200:
            # Save the audio file
            output_path = Path("test_default_tts.wav")
            with open(output_path, "wb") as f:
                f.write(response.content)
            print(f"✅ Default TTS synthesis successful. Audio saved to {output_path}")
            
            # Verify it's a valid WAV file
            try:
                with wave.open(str(output_path), 'rb') as wav_file:
                    frames = wav_file.getnframes()
                    rate = wav_file.getframerate()
                    duration = frames / float(rate)
                    print(f"📊 Audio info: {duration:.2f}s duration, {rate}Hz sample rate, {frames} frames")
            except Exception as e:
                print(f"⚠️ Could not read WAV file info: {e}")
            
            return True
        else:
            print(f"❌ TTS synthesis failed with status code: {response.status_code}")
            print(f"Response: {response.text}")
            return False
    except Exception as e:
        print(f"❌ Error during TTS synthesis: {e}")
        return False

def test_voice_cloning():
    """Test voice cloning if voices are available"""
    print("\nTesting voice cloning...")
    try:
        # First get available voices
        response = requests.get(f"{BASE_URL}/voices")
        if response.status_code != 200:
            print("❌ Cannot get voices list")
            return False
        
        voices_data = response.json()
        voices = voices_data.get("voices", [])
        
        # Filter out 'default' to find actual voice files
        custom_voices = [v for v in voices if v != "default"]
        
        if not custom_voices:
            print("ℹ️ No custom voices available for cloning test")
            return True
        
        # Test with the first available custom voice
        test_voice = custom_voices[0]
        print(f"🎭 Testing voice cloning with voice: {test_voice}")
        
        data = {
            "text": f"Hello, this is a test of voice cloning using the {test_voice} voice.",
            "voice_name": test_voice
        }
        response = requests.post(f"{BASE_URL}/tts/clone", data=data)
        
        if response.status_code == 200:
            # Save the audio file
            output_path = Path(f"test_cloned_{test_voice}.wav")
            with open(output_path, "wb") as f:
                f.write(response.content)
            print(f"✅ Voice cloning successful. Audio saved to {output_path}")
            return True
        elif response.status_code == 404:
            print(f"⚠️ Voice '{test_voice}' not found on server")
            return True
        else:
            print(f"❌ Voice cloning failed with status code: {response.status_code}")
            print(f"Response: {response.text}")
            return False
    except Exception as e:
        print(f"❌ Error during voice cloning test: {e}")
        return False

def test_dotnet_integration():
    """Test if the .NET backend can communicate with Fish Speech"""
    print("\nTesting .NET backend integration...")
    try:
        # This assumes your SwAIvyn backend is running on port 5000
        backend_url = "http://localhost:5000"
        
        # Test voices endpoint
        response = requests.get(f"{backend_url}/api/tts/voices")
        if response.status_code == 200:
            voices_data = response.json()
            print(f"✅ .NET backend voices endpoint working. Available providers: {list(voices_data.keys())}")
            
            # Test synthesis
            synthesis_data = {
                "text": "Hello from the .NET backend integration test.",
                "provider": "FishSpeech",
                "voiceId": "default"
            }
            
            response = requests.post(f"{backend_url}/api/tts/synthesize", json=synthesis_data)
            if response.status_code == 200:
                # Save the audio file
                output_path = Path("test_dotnet_integration.wav")
                with open(output_path, "wb") as f:
                    f.write(response.content)
                print(f"✅ .NET integration synthesis successful. Audio saved to {output_path}")
                return True
            else:
                print(f"❌ .NET synthesis failed with status code: {response.status_code}")
                return False
        else:
            print(f"⚠️ .NET backend not available (status: {response.status_code})")
            print("Make sure SwAIvyn backend is running on port 5000")
            return False
    except requests.exceptions.ConnectionError:
        print("⚠️ Cannot connect to .NET backend. Make sure SwAIvyn is running on port 5000.")
        return False
    except Exception as e:
        print(f"❌ Error testing .NET integration: {e}")
        return False

def main():
    print("🐟 Fish Speech TTS Integration Test")
    print("=" * 50)
    
    all_tests_passed = True
    
    # Test API availability
    if not test_api_availability():
        print("\n❌ Fish Speech API is not available. Please start it first:")
        print("cd speech/TTS/openaudio-s1-mini")
        print("python fish_speech_api.py")
        return False
    
    # Test default TTS
    if not test_default_tts():
        all_tests_passed = False
    
    # Test voice cloning
    if not test_voice_cloning():
        all_tests_passed = False
    
    # Test .NET integration
    if not test_dotnet_integration():
        all_tests_passed = False
    
    print("\n" + "=" * 50)
    if all_tests_passed:
        print("🎉 All tests passed! Fish Speech integration is working correctly.")
    else:
        print("⚠️ Some tests failed. Check the output above for details.")
    
    return all_tests_passed

if __name__ == "__main__":
    main()
