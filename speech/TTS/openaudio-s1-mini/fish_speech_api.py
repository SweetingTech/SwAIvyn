import sys
import sys
import os
import io
from pathlib import Path
from typing import Optional

import torch
import torchaudio
import soundfile as sf

from fastapi import FastAPI, Form, HTTPException, Depends, Header
from fastapi.responses import StreamingResponse
from fastapi.middleware.cors import CORSMiddleware
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials

# Ensure the bundled Fish Speech library is on the import path
sys.path.append(str(Path(__file__).resolve().parent / "fish-speech"))

from fish_speech.config import get_cfg_defaults
from fish_speech.s1.build_model import build_model_from_cfg
from fish_speech.s1.infer import infer_from_text, infer_from_text_with_reference

app = FastAPI()

# (Optional) If you'll be hitting this from a browser or other origins
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # tighten this up in prod!
    allow_methods=["POST", "GET", "OPTIONS"],
    allow_headers=["*"],
)

# Load Fish Audio model configuration and weights
cfg = get_cfg_defaults()
cfg.merge_from_file("config.json")
model = build_model_from_cfg(cfg)
model.load_ckpt("model.pth", "codec.pth")
device = "cuda" if torch.cuda.is_available() else "cpu"
model = model.to(device).eval()

# In-memory caches for reference voices and embeddings
voice_cache: dict[str, dict[str, bytes | str]] = {}
embedding_cache: dict[str, torch.Tensor] = {}

def extract_speaker_embedding(audio_bytes: bytes, text: str) -> torch.Tensor:
    """Extract a speaker embedding from reference audio + text."""
    waveform, sr = torchaudio.load(io.BytesIO(audio_bytes))
    waveform = waveform.to(device)
    with torch.no_grad():
        # Make sure your model actually exposes this
        embedding = model.speaker_encoder(waveform, text)
    return embedding

def preload_voices_and_embeddings() -> None:
    voice_dir = Path("voices")
    voice_dir.mkdir(exist_ok=True)
    # raw .wav + .txt pairs
    for wav_file in voice_dir.glob("*.wav"):
        txt_file = wav_file.with_suffix(".txt")
        if txt_file.exists():
            name = wav_file.stem
            voice_cache[name] = {
                "audio": wav_file.read_bytes(),
                "text": txt_file.read_text(encoding="utf-8")
            }
    # precomputed .pt embeddings
    for vec_file in voice_dir.glob("*.pt"):
        name = vec_file.stem
        embedding_cache[name] = torch.load(vec_file, map_location=device)

preload_voices_and_embeddings()

@app.get("/voices")
def list_voices():
    """List all available voice names (raw refs + embeddings)."""
    names = sorted(set(voice_cache) | set(embedding_cache))
    return {"voices": names}

@app.post("/voices/save")
async def save_embedding(voice_name: str = Form(...)):
    """Generate and persist a speaker embedding (.pt) for a given voice."""
    if voice_name not in voice_cache:
        raise HTTPException(status_code=404, detail=f"Voice '{voice_name}' not found.")
    ref = voice_cache[voice_name]
    embedding = extract_speaker_embedding(ref["audio"], ref["text"])
    vec_path = Path("voices") / f"{voice_name}.pt"
    torch.save(embedding, vec_path)
    embedding_cache[voice_name] = embedding
    return {"status": "saved", "voice": voice_name}

@app.post("/tts")
async def tts(text: str = Form(...)):
    """Standard TTS using default Fish Audio voice."""
    audio = infer_from_text(
        text=text,
        model=model,
        tokenizer_path="tokenizer.tiktoken",
        special_token_path="special_tokens.json",
        device=device,
    )
    buffer = io.BytesIO()
    sf.write(buffer, audio, 22050, format="WAV")
    buffer.seek(0)
    return StreamingResponse(buffer, media_type="audio/wav")

@app.post("/tts/clone")
async def tts_clone(text: str = Form(...), voice_name: str = Form(...)):
    """Few-shot TTS via .wav + .txt references."""
    if voice_name not in voice_cache:
        raise HTTPException(status_code=404, detail=f"Voice '{voice_name}' not found.")
    ref = voice_cache[voice_name]
    audio = infer_from_text_with_reference(
        text=text,
        references=[{"audio": ref["audio"], "text": ref["text"]}],
        model=model,
        tokenizer_path="tokenizer.tiktoken",
        special_token_path="special_tokens.json",
        device=device,
    )
    buffer = io.BytesIO()
    sf.write(buffer, audio, 22050, format="WAV")
    buffer.seek(0)
    return StreamingResponse(buffer, media_type="audio/wav")

@app.post("/tts/voicevec")
async def tts_with_embedding(text: str = Form(...), voice_name: str = Form(...)):
    """TTS using saved speaker embedding (.pt) only."""
    if voice_name not in embedding_cache:
        raise HTTPException(status_code=404, detail=f"Embedding for '{voice_name}' not found.")
    
    speaker_embed = embedding_cache[voice_name]
    
    # Use the embedding with the model for inference
    audio = infer_from_text(
        text=text,
        model=model,
        tokenizer_path="tokenizer.tiktoken",
        special_token_path="special_tokens.json",
        device=device,
        speaker_embedding=speaker_embed  # Pass the embedding if supported
    )
    
    buffer = io.BytesIO()
    sf.write(buffer, audio, 22050, format="WAV")
    buffer.seek(0)
    return StreamingResponse(buffer, media_type="audio/wav")

@app.get("/health")
def health_check():
    """Health check endpoint."""
    return {"status": "healthy", "device": device}

if __name__ == "__main__":
    import uvicorn
    import argparse
    
    parser = argparse.ArgumentParser(description="Fish Speech TTS API Server")
    parser.add_argument("--llama-checkpoint-path", type=str, default=".", help="Path to LLAMA checkpoint")
    parser.add_argument("--decoder-checkpoint-path", type=str, default="codec.pth", help="Path to decoder checkpoint")
    parser.add_argument("--device", type=str, default="cpu", help="Device to use (cpu/cuda)")
    parser.add_argument("--listen", type=str, default="127.0.0.1:8081", help="Host:port to listen on")
    
    args = parser.parse_args()
    
    # Parse host and port from listen argument
    if ":" in args.listen:
        host, port = args.listen.split(":")
        port = int(port)
    else:
        host = args.listen
        port = 8081
    
    print(f"Starting Fish Speech API server on {host}:{port}")
    print(f"Device: {args.device}")
    print(f"LLAMA checkpoint path: {args.llama_checkpoint_path}")
    print(f"Decoder checkpoint path: {args.decoder_checkpoint_path}")
    
    uvicorn.run(app, host=host, port=port)
        raise HTTPException(status_code=404, detail=f"Embedding for '{voice_name}' not found.")
    speaker_embed = embedding_cache[voice_name]
    # Make sure your model has this method
    audio = model.infer_with_embedding(
        text=text,
        speaker_embedding=speaker_embed,
        tokenizer_path="tokenizer.tiktoken",
        special_token_path="special_tokens.json",
        device=device,
    )
    buffer = io.BytesIO()
    sf.write(buffer, audio, 22050, format="WAV")
    buffer.seek(0)
    return StreamingResponse(buffer, media_type="audio/wav")

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
