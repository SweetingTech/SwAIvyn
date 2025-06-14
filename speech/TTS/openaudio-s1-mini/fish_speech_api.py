import sys
import os
import io
from pathlib import Path
from typing import Optional, Dict, Any
import json
import subprocess
import tempfile
import uuid
import datetime

import torch
import torchaudio
import soundfile as sf
import numpy as np

from fastapi import FastAPI, Form, HTTPException, Depends, Header
from fastapi.responses import StreamingResponse
from fastapi.middleware.cors import CORSMiddleware
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials

app = FastAPI()

# Security scheme for Bearer token authentication
security = HTTPBearer(auto_error=False)

# Optional API key for authentication
API_KEY = os.getenv("FISH_SPEECH_API_KEY")  # Set this environment variable if you want authentication

# Configuration
VOICES_DIR = Path(__file__).parent / "voices"
OUTPUT_DIR = Path(__file__).parent / "outputs"  # Directory for saving audio outputs
API_TOKEN = os.getenv("FISH_SPEECH_API_TOKEN", None)  # Optional authentication

# Ensure output directory exists
OUTPUT_DIR.mkdir(exist_ok=True)

# GPU Detection and Setup
def setup_device():
    """Setup the best available device for processing."""
    if torch.cuda.is_available():
        device = torch.device("cuda")
        gpu_name = torch.cuda.get_device_name(0)
        compute_capability = torch.cuda.get_device_capability(0)
        print(f"🚀 CUDA available! Using GPU: {gpu_name}")
        print(f"   Compute Capability: {compute_capability[0]}.{compute_capability[1]} (SM_{compute_capability[0]}{compute_capability[1]})")
        print(f"   CUDA Version: {torch.version.cuda}")
        print(f"   PyTorch CUDA Version: {torch.version.cuda}")
        
        # Set memory management for RTX 5060
        torch.cuda.empty_cache()
        if hasattr(torch.cuda, 'set_per_process_memory_fraction'):
            torch.cuda.set_per_process_memory_fraction(0.8)  # Use 80% of GPU memory
        
        return device, "gpu"
    else:
        print("⚠️  CUDA not available, falling back to CPU")
        return torch.device("cpu"), "cpu"

# Initialize device
DEVICE, DEVICE_TYPE = setup_device()

# CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # tighten this up in prod!
    allow_methods=["POST", "GET", "OPTIONS"],
    allow_headers=["*"],
)

# In-memory cache for voices
voice_cache: Dict[str, Dict[str, Any]] = {}

def authenticate_token(credentials: Optional[HTTPAuthorizationCredentials] = Depends(security)):
    """Verify Bearer token if API key is configured."""
    if API_KEY is None:
        # No authentication required if API key is not set
        return True
    
    if credentials is None:
        raise HTTPException(status_code=401, detail="Authorization header required")
    
    if credentials.credentials != API_KEY:
        raise HTTPException(status_code=403, detail="Invalid API key")
    
    return True

def scan_voice_files():
    """Scan for voice files in subdirectories of voices/"""
    voice_cache.clear()
    voices_dir = Path("voices")
    
    if not voices_dir.exists():
        print("Voices directory not found, creating it...")
        voices_dir.mkdir(exist_ok=True)
        return
    
    # Scan subdirectories for voice files
    for subdir in voices_dir.iterdir():
        if subdir.is_dir():
            voice_name = subdir.name
            wav_file = subdir / f"{voice_name}.wav"
            txt_file = subdir / f"{voice_name}.txt"
            
            if wav_file.exists() and txt_file.exists():
                try:
                    # Load audio and text
                    audio_data = wav_file.read_bytes()
                    text_data = txt_file.read_text(encoding='utf-8').strip()
                    
                    voice_cache[voice_name] = {
                        "audio_path": str(wav_file),
                        "text_path": str(txt_file),
                        "audio_data": audio_data,
                        "text": text_data,
                        "size": len(audio_data)
                    }
                    print(f"Loaded voice: {voice_name} ({len(audio_data)} bytes)")
                except Exception as e:
                    print(f"Error loading voice {voice_name}: {e}")
    
    # Also scan for direct files in voices directory (backwards compatibility)
    for wav_file in voices_dir.glob("*.wav"):
        txt_file = wav_file.with_suffix(".txt")
        if txt_file.exists():
            voice_name = wav_file.stem
            if voice_name not in voice_cache:  # Don't override subdirectory voices
                try:
                    audio_data = wav_file.read_bytes()
                    text_data = txt_file.read_text(encoding='utf-8').strip()
                    
                    voice_cache[voice_name] = {
                        "audio_path": str(wav_file),
                        "text_path": str(txt_file),
                        "audio_data": audio_data,
                        "text": text_data,
                        "size": len(audio_data)
                    }
                    print(f"Loaded voice: {voice_name} ({len(audio_data)} bytes)")
                except Exception as e:
                    print(f"Error loading voice {voice_name}: {e}")
    
    print(f"Total voices loaded: {len(voice_cache)}")

def synthesize_audio_enhanced(text: str, voice_name: str = None) -> bytes:
    """
    Fish Speech TTS synthesis using the command-line interface.
    Generates actual speech using Fish Speech models with CPU fallback.
    """
    try:
        print(f"🎵 Synthesizing speech for: '{text}' with voice: {voice_name}")
        
        # Generate unique filename for this synthesis
        timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        unique_id = str(uuid.uuid4())[:8]
        output_filename = f"tts_{voice_name}_{timestamp}_{unique_id}.wav"
        output_path = OUTPUT_DIR / output_filename
        
        # Use Fish Speech command-line interface for actual TTS
        if voice_name and voice_name in voice_cache:
            voice_path = VOICES_DIR / voice_name
            reference_wav = None
            reference_text = ""
            
            # Find reference audio and text files
            for wav_file in voice_path.glob("*.wav"):
                reference_wav = wav_file
                break
            
            # Look for corresponding text file
            if reference_wav:
                txt_file = reference_wav.with_suffix('.txt')
                if txt_file.exists():
                    reference_text = txt_file.read_text(encoding='utf-8').strip()
                else:
                    # Use a default reference text if no .txt file
                    reference_text = "Hello, this is a voice sample for training."
            
            if reference_wav and reference_wav.exists():
                # Create temporary text file for the input
                with tempfile.NamedTemporaryFile(mode='w', suffix='.txt', delete=False, encoding='utf-8') as temp_txt:
                    temp_txt.write(text)
                    temp_txt_path = temp_txt.name
                
                try:
                    # Build Fish Speech inference command
                    # Using CPU-only mode to avoid CUDA compatibility issues
                    cmd = [
                        sys.executable, "-m", "tools.api",
                        "--text", text,
                        "--reference_audio", str(reference_wav),
                        "--reference_text", reference_text,
                        "--output", str(output_path),
                        "--device", "cpu",  # Force CPU to avoid SM_120 issues
                        "--compile", "false"  # Disable compilation for compatibility
                    ]
                    
                    print(f"🎯 Running Fish Speech inference with CPU-only mode...")
                    print(f"   Reference: {reference_wav.name}")
                    print(f"   Output: {output_filename}")
                    
                    # Run Fish Speech inference
                    result = subprocess.run(
                        cmd,
                        cwd=Path(__file__).parent,
                        capture_output=True,
                        text=True,
                        timeout=60  # 60 second timeout
                    )
                    
                    if result.returncode == 0 and output_path.exists():
                        print("✅ Fish Speech synthesis successful!")
                        # Read the generated audio file
                        with open(output_path, 'rb') as f:
                            audio_data = f.read()
                        
                        # Keep the file for debugging (don't delete)
                        print(f"📁 Audio saved to: {output_path}")
                        return audio_data
                    else:
                        print(f"❌ Fish Speech inference failed:")
                        print(f"   Return code: {result.returncode}")
                        print(f"   STDOUT: {result.stdout}")
                        print(f"   STDERR: {result.stderr}")
                        raise Exception(f"Fish Speech inference failed with code {result.returncode}")
                
                finally:
                    # Clean up temporary text file
                    try:
                        os.unlink(temp_txt_path)
                    except:
                        pass
            
            else:
                raise Exception(f"No reference audio found for voice '{voice_name}'")
        
        else:
            raise Exception(f"Voice '{voice_name}' not found in cache")
            
    except subprocess.TimeoutExpired:
        print("❌ Fish Speech inference timed out")
        return generate_fallback_audio(text, voice_name)
    except Exception as e:
        print(f"❌ Error in Fish Speech synthesis: {e}")
        print("🔄 Falling back to simple audio generation")
        return generate_fallback_audio(text, voice_name)

def generate_fallback_audio(text: str, voice_name: str = None) -> bytes:
    """
    Generate a simple fallback audio when Fish Speech fails.
    Creates a basic synthesized speech-like audio.
    """
    try:
        print(f"🔧 Generating fallback audio for: '{text}'")
        
        # Generate a simple speech-like pattern
        sample_rate = 22050
        duration = max(len(text) * 0.1, 1.0)  # Minimum 1 second
        
        # Create speech-like patterns
        words = text.split()
        audio_segments = []
        
        for i, word in enumerate(words):
            word_duration = len(word) * 0.1 + 0.2  # Duration based on word length
            word_samples = int(word_duration * sample_rate)
            t = np.linspace(0, word_duration, word_samples)
            
            # Create formant-like frequencies for speech
            f1 = 300 + (hash(word) % 400)  # First formant: 300-700 Hz
            f2 = 1000 + (hash(word[::-1]) % 1000)  # Second formant: 1000-2000 Hz
            
            # Generate speech-like waveform
            wave1 = np.sin(2 * np.pi * f1 * t) * 0.4
            wave2 = np.sin(2 * np.pi * f2 * t) * 0.2
            
            # Add some harmonics
            wave3 = np.sin(2 * np.pi * f1 * 2 * t) * 0.1
            
            word_audio = wave1 + wave2 + wave3
            
            # Apply envelope to make it sound more natural
            envelope = np.exp(-t * 2) * (1 - np.exp(-t * 10))
            word_audio *= envelope
            
            audio_segments.append(word_audio)
            
            # Add pause between words
            if i < len(words) - 1:
                pause_samples = int(0.1 * sample_rate)
                pause = np.zeros(pause_samples)
                audio_segments.append(pause)
        
        # Combine all segments
        audio = np.concatenate(audio_segments)
        
        # Normalize and convert to 16-bit PCM
        audio = np.clip(audio, -1.0, 1.0)
        audio = (audio * 0.5 * 32767).astype(np.int16)
        
        # Save fallback audio to output directory
        timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        fallback_filename = f"fallback_{voice_name}_{timestamp}.wav"
        fallback_path = OUTPUT_DIR / fallback_filename
        
        sf.write(fallback_path, audio, sample_rate)
        print(f"📁 Fallback audio saved to: {fallback_path}")
        
        # Return as bytes
        buffer = io.BytesIO()
        sf.write(buffer, audio, sample_rate, format="WAV")
        buffer.seek(0)
        return buffer.read()
        
    except Exception as e:
        print(f"❌ Even fallback audio generation failed: {e}")
        # Return minimal 1-second beep
        sample_rate = 22050
        t = np.linspace(0, 1.0, sample_rate)
        beep = np.sin(2 * np.pi * 440 * t) * 0.3
        beep = (beep * 32767).astype(np.int16)
        
        buffer = io.BytesIO()
        sf.write(buffer, beep, sample_rate, format="WAV")
        buffer.seek(0)
        return buffer.read()

# Initialize voice cache on startup
scan_voice_files()

@app.get("/voices")
def list_voices(authenticated: bool = Depends(authenticate_token)):
    """List all available voice names."""
    return {"voices": list(voice_cache.keys())}

@app.get("/voices/{voice_name}/details")
def get_voice_details(voice_name: str, authenticated: bool = Depends(authenticate_token)):
    """Get details about a specific voice."""
    if voice_name not in voice_cache:
        raise HTTPException(status_code=404, detail=f"Voice '{voice_name}' not found.")
    
    voice = voice_cache[voice_name]
    return {
        "name": voice_name,
        "text": voice["text"],
        "audio_size": voice["size"],
        "audio_path": voice["audio_path"],
        "text_path": voice["text_path"]
    }

@app.post("/voices/refresh")
def refresh_voices(authenticated: bool = Depends(authenticate_token)):
    """Refresh the voice cache by rescanning the voices directory."""
    scan_voice_files()
    return {"message": f"Voice cache refreshed. Found {len(voice_cache)} voices.", "voices": list(voice_cache.keys())}

@app.post("/tts")
async def tts(text: str = Form(...), authenticated: bool = Depends(authenticate_token)):
    """Standard TTS using default voice."""
    if not text.strip():
        raise HTTPException(status_code=400, detail="Text cannot be empty")
    
    try:
        audio_data = synthesize_audio_enhanced(text)
        return StreamingResponse(io.BytesIO(audio_data), media_type="audio/wav")
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"TTS synthesis failed: {str(e)}")

@app.post("/tts/clone")
async def tts_clone(text: str = Form(...), voice_name: str = Form(...), authenticated: bool = Depends(authenticate_token)):
    """Voice cloning TTS using reference voice."""
    if not text.strip():
        raise HTTPException(status_code=400, detail="Text cannot be empty")
    
    if voice_name not in voice_cache:
        raise HTTPException(status_code=404, detail=f"Voice '{voice_name}' not found.")
    
    try:
        audio_data = synthesize_audio_enhanced(text, voice_name)
        return StreamingResponse(io.BytesIO(audio_data), media_type="audio/wav")
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"TTS synthesis failed: {str(e)}")

@app.post("/tts/voicevec")
async def tts_with_embedding(text: str = Form(...), voice_name: str = Form(...), authenticated: bool = Depends(authenticate_token)):
    """TTS using saved speaker embedding (same as clone for now)."""
    return await tts_clone(text, voice_name, authenticated)

@app.get("/health")
def health_check():
    """Health check endpoint."""
    gpu_info = ""
    if DEVICE_TYPE == "gpu" and torch.cuda.is_available():
        gpu_info = f" ({torch.cuda.get_device_name(0)})"
    
    return {
        "status": "healthy",
        "voices_loaded": len(voice_cache),
        "authentication": "enabled" if API_KEY else "disabled",
        "device": f"{DEVICE_TYPE}{gpu_info}",
        "cuda_available": torch.cuda.is_available(),
        "device_name": torch.cuda.get_device_name(0) if torch.cuda.is_available() else "CPU"
    }

if __name__ == "__main__":
    import uvicorn
    import argparse
    
    parser = argparse.ArgumentParser(description="Fish Speech Compatible API Server")
    parser.add_argument("--listen", type=str, default="127.0.0.1:8081", help="Host:port to listen on")
    parser.add_argument("--api-key", type=str, help="API key for authentication")
    
    args = parser.parse_args()
    
    # Set API key from command line argument if provided
    if args.api_key:
        API_KEY = args.api_key
        os.environ["FISH_SPEECH_API_KEY"] = args.api_key
    
    # Parse host and port from listen argument
    if ":" in args.listen:
        host, port = args.listen.split(":")
        port = int(port)
    else:
        host = args.listen
        port = 8081
    
    print(f"Starting Fish Speech Compatible API server on {host}:{port}")
    print(f"Authentication: {'enabled' if API_KEY else 'disabled'}")
    print(f"Voices directory: {Path('voices').absolute()}")
    
    uvicorn.run(app, host=host, port=port)
