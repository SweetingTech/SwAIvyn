#!/usr/bin/env python3
"""
Fish Speech TTS API Server
A persistent API server that loads Fish Speech models in memory for real-time TTS.
Designed for integration with SwAIvyn chat system.
"""

import sys
import os
from pathlib import Path
from typing import Optional, Dict, Any, List
import json
import io
import logging
import argparse

import torch
import torchaudio
import soundfile as sf
import numpy as np

from fastapi import FastAPI, Form, HTTPException, Depends, Header
from fastapi.responses import StreamingResponse
from fastapi.middleware.cors import CORSMiddleware

# Add the current directory to Python path for imports
current_dir = Path(__file__).parent
sys.path.insert(0, str(current_dir))

# Import Fish Speech modules
try:
    from fish_speech.inference_engine import TTSInferenceEngine
    from fish_speech.models.text2semantic.inference import launch_thread_safe_queue
    from fish_speech.models.dac.inference import load_model as load_decoder_model
    from fish_speech.utils.schema import ServeTTSRequest
    print("✅ Fish Speech modules imported successfully")
except ImportError as e:
    print(f"❌ Failed to import Fish Speech modules: {e}")
    print("Make sure the fish_speech directory is properly copied")
    sys.exit(1)

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(title="Fish Speech TTS API", version="1.0.0")

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Global variables for models and voices
tts_engine: Optional[TTSInferenceEngine] = None
voice_cache: Dict[str, Any] = {}
device_info: str = ""

# Configuration - Fish Speech expects model directory structure
VOICES_DIR = Path(__file__).parent / "voices"
MODEL_DIR = Path(__file__).parent / "fish_speech_model"  # Point to model directory
CODEC_PATH = Path(__file__).parent / "codec.pth"

def setup_device():
    """Setup the best available device for processing."""
    global device_info
    
    # Force CPU mode due to GPU compatibility issues
    device = "cpu"
    device_info = "cpu"
    
    if torch.cuda.is_available():
        gpu_name = torch.cuda.get_device_name(0)
        compute_capability = torch.cuda.get_device_capability(0)
        
        # Check if GPU is compatible (avoid SM_120 issues)
        if compute_capability[0] >= 6 and compute_capability[0] <= 9:  # SM_60 to SM_90
            device = "cuda"
            device_info = f"gpu ({gpu_name})"
            logger.info(f"🚀 Using compatible GPU: {gpu_name}")
        else:
            logger.warning(f"⚠️  GPU {gpu_name} (SM_{compute_capability[0]}{compute_capability[1]}) not compatible, using CPU")
            device_info = f"cpu (GPU {gpu_name} incompatible)"
    
    logger.info(f"Device: {device_info}")
    return device

def load_fish_speech_models():
    """Load Fish Speech models into memory."""
    global tts_engine
    
    logger.info("🔄 Loading Fish Speech models...")
    
    device = setup_device()
    precision = torch.float32  # Use float32 for better compatibility
    
    try:
        # Check if model files exist
        model_pth = MODEL_DIR / "model.pth"
        if not model_pth.exists():
            raise FileNotFoundError(f"Model file not found: {model_pth}")
        if not CODEC_PATH.exists():
            raise FileNotFoundError(f"Codec file not found: {CODEC_PATH}")
        
        logger.info(f"📁 Loading LLAMA model from: {MODEL_DIR}")
        
        # Load LLAMA model (text2semantic)
        llama_queue = launch_thread_safe_queue(
            checkpoint_path=str(MODEL_DIR),  # Pass directory path, not file path
            device=device,
            precision=precision,
            compile=False  # Disable compilation for compatibility
        )
        
        logger.info(f"📁 Loading Decoder model from: {CODEC_PATH}")
        
        # Load decoder model (DAC)
        decoder_model = load_decoder_model(
            config_name="modded_dac_vq",  # Correct config for openaudio-s1-mini
            checkpoint_path=str(CODEC_PATH),
            device=device
        )
        
        # Create TTS inference engine
        tts_engine = TTSInferenceEngine(
            llama_queue=llama_queue,
            decoder_model=decoder_model,
            precision=precision,
            compile=False
        )
        
        logger.info("🔥 Warming up models...")
        
        # Warm up with a simple test
        test_request = ServeTTSRequest(
            text="Hello.",
            references=[],
            reference_id=None,
            max_new_tokens=512,   # Shorter for warmup
            chunk_length=100,
            top_p=0.8,
            repetition_penalty=1.1,
            temperature=0.8,
            format="wav",
        )
        
        # Run inference once to warm up
        for _ in tts_engine.inference(test_request):
            pass
        
        logger.info("✅ Fish Speech models loaded and warmed up successfully!")
        return True
        
    except Exception as e:
        logger.error(f"❌ Failed to load Fish Speech models: {e}")
        return False

def scan_voice_files():
    """Scan for voice files in the voices directory."""
    global voice_cache
    voice_cache.clear()
    
    if not VOICES_DIR.exists():
        logger.warning(f"Voices directory not found: {VOICES_DIR}")
        return
    
    logger.info(f"🔍 Scanning voices in: {VOICES_DIR}")
    
    for voice_dir in VOICES_DIR.iterdir():
        if voice_dir.is_dir():
            voice_name = voice_dir.name
            wav_files = list(voice_dir.glob("*.wav"))
            
            if wav_files:
                # Find the largest WAV file (best quality reference)
                best_wav = max(wav_files, key=lambda f: f.stat().st_size)
                
                # Look for corresponding text file
                txt_file = best_wav.with_suffix('.txt')
                reference_text = ""
                
                if txt_file.exists():
                    try:
                        reference_text = txt_file.read_text(encoding='utf-8').strip()
                    except:
                        reference_text = f"Reference audio for {voice_name}"
                else:
                    reference_text = f"Reference audio for {voice_name}"
                
                voice_cache[voice_name] = {
                    "audio_path": str(best_wav),
                    "text": reference_text,
                    "size": best_wav.stat().st_size
                }
                
                logger.info(f"📢 Loaded voice: {voice_name} ({best_wav.stat().st_size} bytes)")
    
    logger.info(f"Total voices loaded: {len(voice_cache)}")

def synthesize_with_fish_speech(text: str, voice_name: str = None) -> bytes:
    """Synthesize speech using the loaded Fish Speech models."""
    global tts_engine
    
    if tts_engine is None:
        raise HTTPException(status_code=500, detail="Fish Speech models not loaded")
    
    logger.info(f"🎵 Synthesizing: '{text}' with voice: {voice_name}")
    
    # Prepare references if voice is specified
    references = []
    if voice_name and voice_name in voice_cache:
        voice_data = voice_cache[voice_name]
        # Read the audio file as bytes
        try:
            with open(voice_data["audio_path"], "rb") as f:
                audio_bytes = f.read()
            references = [{
                "audio": audio_bytes,
                "text": voice_data["text"]
            }]
        except Exception as e:
            logger.error(f"Failed to read voice file {voice_data['audio_path']}: {e}")
            references = []
    
    # Create TTS request
    request = ServeTTSRequest(
        text=text,
        references=references,
        reference_id=None,
        max_new_tokens=1024,  # Reduced for faster generation on CPU
        chunk_length=100,     # Smaller chunks for faster processing
        top_p=0.8,
        repetition_penalty=1.1,
        temperature=0.8,
        format="wav",
    )
    
    # Generate audio
    audio_chunks = []
    try:
        logger.info("🔄 Starting TTS inference...")
        chunk_count = 0
        for result in tts_engine.inference(request):
            chunk_count += 1
            logger.info(f"📦 Processing chunk {chunk_count}: {result.code}")

            if result.code == "segment" and isinstance(result.audio, tuple):
                # Streaming segment - convert to 16-bit PCM
                audio_data = (result.audio[1] * 32768).astype(np.int16)
                audio_chunks.append(audio_data)
                logger.info(f"✅ Added streaming audio chunk: {audio_data.shape}")
            elif result.code == "final" and isinstance(result.audio, tuple):
                # Final complete audio - convert to 16-bit PCM
                logger.info(f"🔍 Final audio tuple: {type(result.audio)}, length: {len(result.audio)}")
                logger.info(f"🔍 Audio data type: {type(result.audio[1])}, shape: {result.audio[1].shape if hasattr(result.audio[1], 'shape') else 'no shape'}")
                audio_data = (result.audio[1] * 32768).astype(np.int16)
                audio_chunks.append(audio_data)
                logger.info(f"✅ Added final audio: {audio_data.shape}")
                break
            elif result.code == "error":
                raise Exception(f"TTS error: {result.error}")
            elif result.code == "header":
                logger.info("📋 Received audio header")
                continue

        if not audio_chunks:
            raise Exception("No audio generated - check if text is too short or model parameters")

        # Combine all chunks
        full_audio = np.concatenate(audio_chunks)
        logger.info(f"🎵 Combined audio shape: {full_audio.shape}")

        # Get sample rate from the result
        sample_rate = 22050  # Default fallback
        if hasattr(tts_engine.decoder_model, 'spec_transform'):
            sample_rate = tts_engine.decoder_model.spec_transform.sample_rate
        elif hasattr(tts_engine.decoder_model, 'sample_rate'):
            sample_rate = tts_engine.decoder_model.sample_rate

        # Convert to WAV bytes
        audio_io = io.BytesIO()
        sf.write(audio_io, full_audio, sample_rate, format='WAV')
        audio_bytes = audio_io.getvalue()

        logger.info(f"✅ Generated {len(audio_bytes)} bytes of audio at {sample_rate}Hz")
        return audio_bytes
        
    except Exception as e:
        logger.error(f"❌ TTS synthesis failed: {e}")
        raise HTTPException(status_code=500, detail=f"TTS synthesis failed: {str(e)}")

# API Endpoints

@app.get("/")
def root():
    return {"message": "Fish Speech TTS API Server", "status": "running"}

@app.get("/health")
def health_check():
    """Health check endpoint."""
    return {
        "status": "healthy",
        "voices_loaded": len(voice_cache),
        "authentication": "disabled",
        "device": device_info,
        "cuda_available": torch.cuda.is_available(),
        "device_name": torch.cuda.get_device_name(0) if torch.cuda.is_available() else "CPU",
        "models_loaded": tts_engine is not None
    }

@app.get("/voices")
def get_voices():
    """Get list of available voices."""
    return {"voices": list(voice_cache.keys())}

@app.get("/voices/{voice_name}/details")
def get_voice_details(voice_name: str):
    """Get detailed information about a specific voice."""
    if voice_name not in voice_cache:
        raise HTTPException(status_code=404, detail=f"Voice '{voice_name}' not found")
    
    voice = voice_cache[voice_name]
    return {
        "name": voice_name,
        "text": voice["text"],
        "audio_size": voice["size"],
        "audio_path": voice["audio_path"]
    }

@app.post("/voices/refresh")
def refresh_voices():
    """Refresh the voice cache by rescanning the voices directory."""
    scan_voice_files()
    return {"message": f"Voice cache refreshed. Found {len(voice_cache)} voices.", "voices": list(voice_cache.keys())}

@app.post("/tts")
async def tts(text: str = Form(...)):
    """Standard TTS using default voice or first available voice."""
    if not text.strip():
        raise HTTPException(status_code=400, detail="Text cannot be empty")
    
    # Use first available voice as default
    voice_name = next(iter(voice_cache.keys())) if voice_cache else None
    
    try:
        audio_data = synthesize_with_fish_speech(text, voice_name)
        return StreamingResponse(io.BytesIO(audio_data), media_type="audio/wav")
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"TTS synthesis failed: {str(e)}")

@app.post("/tts/clone")
async def tts_clone(text: str = Form(...), voice_name: str = Form(...)):
    """Voice cloning TTS using specified reference voice."""
    if not text.strip():
        raise HTTPException(status_code=400, detail="Text cannot be empty")
    
    if voice_name not in voice_cache:
        raise HTTPException(status_code=404, detail=f"Voice '{voice_name}' not found.")
    
    try:
        audio_data = synthesize_with_fish_speech(text, voice_name)
        return StreamingResponse(io.BytesIO(audio_data), media_type="audio/wav")
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"TTS synthesis failed: {str(e)}")

# Startup and shutdown events

@app.on_event("startup")
async def startup_event():
    """Initialize models and voice cache on startup."""
    logger.info("🚀 Starting Fish Speech TTS API Server...")
    
    # Load models
    if not load_fish_speech_models():
        logger.error("❌ Failed to load models, server will not function properly")
    
    # Scan voices
    scan_voice_files()
    
    logger.info("✅ Fish Speech TTS API Server ready!")

@app.on_event("shutdown")
async def shutdown_event():
    """Cleanup on shutdown."""
    logger.info("🛑 Shutting down Fish Speech TTS API Server...")

def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(description="Fish Speech TTS API Server")
    parser.add_argument("--listen", default="localhost:8081", help="Host:port to listen on")
    parser.add_argument("--workers", type=int, default=1, help="Number of workers")
    
    args = parser.parse_args()
    
    host, port = args.listen.split(":")
    port = int(port)
    
    print(f"🎤 Starting Fish Speech TTS API on {host}:{port}")
    print(f"🎯 Models: {MODEL_DIR / 'model.pth'}")
    print(f"🎭 Voices: {VOICES_DIR}")
    
    import uvicorn
    uvicorn.run(app, host=host, port=port, workers=args.workers)

if __name__ == "__main__":
    main()
