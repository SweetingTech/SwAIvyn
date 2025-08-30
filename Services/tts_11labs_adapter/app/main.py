import os
from fastapi import FastAPI
from pydantic import BaseModel


class SynthesizeRequest(BaseModel):
    text: str
    voice_id: str | None = None


app = FastAPI(title="ElevenLabs Adapter (stub)")


@app.get("/healthz")
async def healthz():
    return {"status": "ok"}


@app.post("/synthesize")
async def synthesize(req: SynthesizeRequest):
    # Stub for development: return a placeholder URL.
    # In production, call ElevenLabs API with ELEVENLABS_API_KEY.
    return {"url": ""}

