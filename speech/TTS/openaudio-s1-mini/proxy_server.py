from __future__ import annotations

import os
from pathlib import Path
from typing import List

import httpx
from fastapi import FastAPI, UploadFile, File, Form, HTTPException
from fastapi.responses import JSONResponse, Response


def make_silence_wav(seconds: float = 0.5, sample_rate: int = 16000) -> bytes:
    import struct
    num_samples = int(sample_rate * seconds)
    pcm_data = b"".join(struct.pack('<h', 0) for _ in range(num_samples))
    byte_rate = sample_rate * 2
    block_align = 2
    wav_header = (
        b"RIFF" +
        struct.pack('<I', 36 + len(pcm_data)) +
        b"WAVEfmt " +
        struct.pack('<IHHIIHH', 16, 1, 1, sample_rate, byte_rate, block_align, 16) +
        b"data" +
        struct.pack('<I', len(pcm_data))
    )
    return wav_header + pcm_data


UPSTREAM_TTS = os.getenv("UPSTREAM_TTS", "http://host.docker.internal:8081").rstrip("/")
VOICES_DIR = Path(os.getenv("VOICES_DIR", "/app/voices")).resolve()

app = FastAPI(title="Fish Speech Proxy", version="0.1.0")


@app.get("/health")
async def health():
    # Report our own readiness; optionally include upstream status
    ok = True
    up = "unknown"
    try:
        async with httpx.AsyncClient(timeout=2) as client:
            r = await client.get(f"{UPSTREAM_TTS}/health")
            up = str(r.status_code)
    except Exception:
        ok = False
        up = "down"
    return {"status": "ok" if ok else "degraded", "upstream": up}


@app.get("/voices")
async def list_voices():
    """Return available voice IDs.
    Priority: upstream -> local voices.json -> local *.wav in root -> *.wav in subfolders.
    """
    # Try upstream first
    try:
        async with httpx.AsyncClient(timeout=5) as client:
            r = await client.get(f"{UPSTREAM_TTS}/voices")
            if r.status_code == 200:
                return JSONResponse(r.json())
    except Exception:
        pass

    items: List[str] = []
    try:
        if VOICES_DIR.exists():
            # 1) voices.json if present
            vjson = VOICES_DIR / "voices.json"
            if vjson.exists():
                import json
                try:
                    data = json.loads(vjson.read_text(encoding="utf-8", errors="ignore"))
                    payload = None
                    if isinstance(data, dict) and isinstance(data.get("voices"), list):
                        payload = data["voices"]
                    elif isinstance(data, list):
                        payload = data
                    if isinstance(payload, list):
                        for entry in payload:
                            if isinstance(entry, str):
                                items.append(entry)
                            elif isinstance(entry, dict):
                                vid = entry.get("id") or entry.get("name") or entry.get("displayName")
                                if isinstance(vid, str):
                                    items.append(vid)
                except Exception:
                    pass

            # 2) *.wav directly under voices dir
            if not items:
                for p in VOICES_DIR.glob("*.wav"):
                    items.append(p.stem)

            # 3) *.wav inside first-level subdirectories; use folder name if present
            if not items:
                for sub in VOICES_DIR.iterdir():
                    if sub.is_dir():
                        wavs = list(sub.glob("*.wav"))
                        if wavs:
                            # prefer directory name as voice id
                            items.append(sub.name)
                # as a fallback, if still empty, collect stems from any nested wavs
                if not items:
                    for p in VOICES_DIR.glob("**/*.wav"):
                        items.append(p.stem)
            # de-dup while preserving order
            seen = set()
            uniq: List[str] = []
            for v in items:
                if v not in seen:
                    uniq.append(v)
                    seen.add(v)
            items = uniq
    except Exception:
        pass
    return {"voices": items}


@app.post("/tts")
async def tts(text: str = Form(...)):
    # Pass-through to upstream; fallback to silence
    try:
        async with httpx.AsyncClient(timeout=60) as client:
            r = await client.post(f"{UPSTREAM_TTS}/tts", data={"text": text})
            if r.status_code == 200:
                ctype = r.headers.get("content-type", "audio/wav")
                return Response(content=r.content, media_type=ctype)
    except Exception:
        pass
    return Response(content=make_silence_wav(), media_type="audio/wav")


@app.post("/tts/clone")
async def tts_clone(text: str = Form(...), voice_name: str = Form(...)):
    # Only clone if upstream accepts it; otherwise try default /tts as fallback
    try:
        async with httpx.AsyncClient(timeout=60) as client:
            r = await client.post(f"{UPSTREAM_TTS}/tts/clone", data={"text": text, "voice_name": voice_name})
            if r.status_code == 200:
                ctype = r.headers.get("content-type", "audio/wav")
                return Response(content=r.content, media_type=ctype)
    except Exception:
        pass
    # fallback to default tts
    return await tts(text=text)
