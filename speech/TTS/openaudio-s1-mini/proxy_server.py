from __future__ import annotations

import os
from pathlib import Path
from typing import List, Dict, Tuple, Optional, Any

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
UPSTREAM_TTS_LIST_RAW = os.getenv("UPSTREAM_TTS_LIST", "").strip()
VOICES_DIR = Path(os.getenv("VOICES_DIR", "/app/voices")).resolve()

app = FastAPI(title="Fish Speech Proxy", version="0.1.0")


def _get_upstreams() -> List[str]:
    raw = [s.strip() for s in UPSTREAM_TTS_LIST_RAW.split(",") if s.strip()]
    bases = [s.rstrip("/") for s in raw]
    if not bases:
        bases = [UPSTREAM_TTS]
    seen = set()
    uniq: List[str] = []
    for b in bases:
        if b not in seen:
            uniq.append(b)
            seen.add(b)
    return uniq


@app.get("/health")
async def health():
    statuses: List[Dict[str, str]] = []
    ok = False
    for base in _get_upstreams():
        status = "down"
        try:
            async with httpx.AsyncClient(timeout=5) as client:
                r = await client.get(f"{base}/v1/health")
                status = str(r.status_code)
                if r.status_code == 200:
                    ok = True
        except Exception:
            status = "down"
        statuses.append({"url": base, "status": status})
    top = statuses[0]["status"] if statuses else "down"
    return {"status": "ok" if ok else "degraded", "upstream": top, "upstreams": statuses}


def _extract_names_from_upstream_payload(payload: Any) -> List[str]:
    names: List[str] = []
    try:
        if isinstance(payload, dict):
            maybe = payload.get("voices")
            if isinstance(maybe, list):
                payload = maybe
        if isinstance(payload, list):
            for item in payload:
                if isinstance(item, str):
                    names.append(item)
                elif isinstance(item, dict):
                    vid = item.get("id") or item.get("name") or item.get("displayName")
                    if isinstance(vid, str):
                        names.append(vid)
    except Exception:
        pass
    return names


def _load_local_voices_index() -> Dict[str, Tuple[Path, Optional[Path]]]:
    """Map voice_name -> (audio_path, text_path?).
    Prefers voices.json; falls back to scanning directory structure.
    """
    mapping: Dict[str, Tuple[Path, Optional[Path]]] = {}
    if not VOICES_DIR.exists():
        return mapping
    # 1) voices.json
    vjson = VOICES_DIR / "voices.json"
    if vjson.exists():
        import json
        try:
            data = json.loads(vjson.read_text(encoding="utf-8", errors="ignore"))
            voices = data.get("voices") if isinstance(data, dict) else data
            if isinstance(voices, list):
                for v in voices:
                    if not isinstance(v, dict):
                        continue
                    if v.get("enabled") is False:
                        continue
                    name = v.get("name") or v.get("id") or v.get("displayName")
                    af = v.get("audioFile")
                    tf = v.get("textFile") or v.get("transcript")
                    if not isinstance(name, str) or not isinstance(af, str):
                        continue
                    ap = (VOICES_DIR / af).resolve()
                    tp = (VOICES_DIR / tf).resolve() if isinstance(tf, str) else None
                    if ap.exists():
                        mapping[name] = (ap, tp if (tp and tp.exists()) else None)
        except Exception:
            pass
    # 2) Scan immediate subfolders and root for wav+txt/lab
    try:
        # Root-level wavs
        for p in VOICES_DIR.glob("*.wav"):
            base = p.stem
            txt = None
            for ext in (".txt", ".lab"):
                cand = VOICES_DIR / f"{base}{ext}"
                if cand.exists():
                    txt = cand
                    break
            mapping.setdefault(base, (p, txt))

        # First-level directories
        for sub in VOICES_DIR.iterdir():
            if not sub.is_dir():
                continue
            wavs = list(sub.glob("*.wav"))
            if not wavs:
                continue
            # Prefer a wav matching directory name
            prefer = sub / f"{sub.name}.wav"
            ap = prefer if prefer.exists() else wavs[0]
            tp: Optional[Path] = None
            for ext in (".txt", ".lab"):
                cand = sub / f"{sub.name}{ext}"
                if cand.exists():
                    tp = cand
                    break
            mapping.setdefault(sub.name, (ap, tp))
    except Exception:
        pass
    return mapping


def _local_voice_names() -> List[str]:
    names = list(_load_local_voices_index().keys())
    # de-dup and stable order
    seen = set()
    uniq: List[str] = []
    for n in names:
        if n not in seen:
            uniq.append(n)
            seen.add(n)
    return uniq


@app.get("/voices")
async def list_voices():
    """Return only local voice IDs (no upstream probing)."""
    local_names = _local_voice_names()
    # de-dup in case of any accidental repeats
    seen = set()
    uniq: List[str] = []
    for v in local_names:
        if v not in seen:
            uniq.append(v)
            seen.add(v)
    return {"voices": uniq}


@app.post("/tts")
async def tts(text: str = Form(...)):
    # Try each upstream /v1/tts in order; return first success, else silence
    for base in _get_upstreams():
        try:
            async with httpx.AsyncClient(timeout=90) as client:
                r = await client.post(
                    f"{base}/v1/tts",
                    json={"text": text, "format": "wav"},
                )
                if r.status_code == 200 and r.content:
                    ctype = r.headers.get("content-type", "audio/wav")
                    return Response(content=r.content, media_type=ctype)
        except Exception:
            continue
    return Response(content=make_silence_wav(), media_type="audio/wav")


@app.post("/tts/clone")
async def tts_clone(text: str = Form(...), voice_name: str = Form(...)):
    """Clone TTS using either upstream default voices or local reference voices.
    Behavior:
    - If `voice_name` matches a local entry (in `VOICES_DIR`), build a Fish-Speech
      MessagePack request to `/v1/tts` on the upstream server using the local
      audio + transcript as reference.
    - Otherwise, try upstream `/tts/clone` (for built-in/default voices).
    - If all fails, fall back to `/tts` pass-through or silence.
    """
    # 1) If local voice exists, attempt reference synthesis via /v1/tts
    local_index = _load_local_voices_index()
    if voice_name in local_index:
        ap, tp = local_index[voice_name]
        try:
            import ormsgpack
            ref_audio = ap.read_bytes()
            ref_text = tp.read_text(encoding="utf-8", errors="ignore") if tp and tp.exists() else ""
            payload = {
                "text": text,
                "references": [{"audio": ref_audio, "text": ref_text}],
                "format": "wav",
                # Reasonable defaults; can be tuned later
                "top_p": 0.7,
                "repetition_penalty": 1.15,
                "temperature": 0.7,
            }
            async with httpx.AsyncClient(timeout=90) as client:
                for base in _get_upstreams():
                    try:
                        r = await client.post(
                            f"{base}/v1/tts",
                            data=ormsgpack.packb(payload),
                            headers={"Content-Type": "application/msgpack"},
                        )
                        if r.status_code == 200 and r.content:
                            ctype = r.headers.get("content-type", "audio/wav")
                            return Response(content=r.content, media_type=ctype)
                    except Exception:
                        continue
        except Exception:
            # If anything goes wrong with local reference path, fall back
            pass


    # 3) fallback to default /tts
    return await tts(text=text)
