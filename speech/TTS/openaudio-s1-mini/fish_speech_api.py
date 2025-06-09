import torch
import torchaudio
import sys
from pathlib import Path
from fastapi import FastAPI, Form, HTTPException
from fastapi.responses import StreamingResponse
# Ensure the bundled Fish Speech library is on the import path
sys.path.append(str(Path(__file__).resolve().parent / "fish-speech"))

from fish_speech.s1.infer import infer_from_text, infer_from_text_with_reference
from fish_speech.config import get_cfg_defaults
from fish_speech.s1.build_model import build_model_from_cfg
import soundfile as sf
import io

app = FastAPI()

# Load Fish Audio model configuration and weights
dfg = get_cfg_defaults()
dfg.merge_from_file("config.json")
model = build_model_from_cfg(dfg)
model.load_ckpt("model.pth", "codec.pth")
device = "cuda" if torch.cuda.is_available() else "cpu"
model = model.to(device).eval()

# In-memory caches for reference voices and embeddings
voice_cache = {}
embedding_cache = {}

# Helper: Extract a speaker embedding from reference audio + text
# You'll need to ensure your model exposes a speaker_encoder() method.
def extract_speaker_embedding(audio_bytes: bytes, text: str):
    waveform, sr = torchaudio.load(io.BytesIO(audio_bytes))
    waveform = waveform.to(device)
    with torch.no_grad():
        # Replace `model.speaker_encoder` with your actual encoder call
        embedding = model.speaker_encoder(waveform, text)
    return embedding

# Preload all .wav/.txt and .pt files in voices/ on startup
def preload_voices_and_embeddings():
    voice_dir = Path("voices")
    voice_dir.mkdir(exist_ok=True)
    # Load raw references
    for wav_file in voice_dir.glob("*.wav"):
        txt_file = wav_file.with_suffix(".txt")
        if txt_file.exists():
            name = wav_file.stem
            voice_cache[name] = {
                "audio": wav_file.read_bytes(),
                "text": txt_file.read_text(encoding="utf-8")
            }
    # Load any saved embeddings
    for vec_file in voice_dir.glob("*.pt"):
        name = vec_file.stem
        embedding_cache[name] = torch.load(vec_file, map_location=device)

preload_voices_and_embeddings()

@app.get("/voices")
def list_voices():
    """List all available voice names (raw refs + embeddings)"""
    names = sorted(set(voice_cache.keys()) | set(embedding_cache.keys()))
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
    """Standard TTS using default Fish Audio voice"""
    audio = infer_from_text(
        text=text,
        model=model,
        tokenizer_path="tokenizer.tiktoken",
        special_token_path="special_tokens.json",
        device=device
    )
    buffer = io.BytesIO()
    sf.write(buffer, audio, 22050, format="WAV")
    buffer.seek(0)
    return StreamingResponse(buffer, media_type="audio/wav")

@app.post("/tts/clone")
async def tts_clone(text: str = Form(...), voice_name: str = Form(...)):
    """Few-shot TTS via .wav + .txt references"""
    if voice_name not in voice_cache:
        raise HTTPException(status_code=404, detail=f"Voice '{voice_name}' not found.")
    ref = voice_cache[voice_name]
    audio = infer_from_text_with_reference(
        text=text,
        references=[{"audio": ref["audio"], "text": ref["text"]}],
        model=model,
        tokenizer_path="tokenizer.tiktoken",
        special_token_path="special_tokens.json",
        device=device
    )
    buffer = io.BytesIO()
    sf.write(buffer, audio, 22050, format="WAV")
    buffer.seek(0)
    return StreamingResponse(buffer, media_type="audio/wav")

@app.post("/tts/voicevec")
async def tts_with_embedding(text: str = Form(...), voice_name: str = Form(...)):
    """TTS using saved speaker embedding (.pt) only"""
    if voice_name not in embedding_cache:
        raise HTTPException(status_code=404, detail=f"Embedding for '{voice_name}' not found.")
    speaker_embed = embedding_cache[voice_name]
    # Replace with your model's direct embedding-based inference method
    audio = model.infer_with_embedding(
        text=text,
        speaker_embedding=speaker_embed,
        tokenizer_path="tokenizer.tiktoken",
        special_token_path="special_tokens.json",
        device=device
    )
    buffer = io.BytesIO()
    sf.write(buffer, audio, 22050, format="WAV")
    buffer.seek(0)
    return StreamingResponse(buffer, media_type="audio/wav")

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)

