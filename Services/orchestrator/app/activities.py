import os
import requests
from typing import Dict, Any
from temporalio import activity

OLLAMA_HOST = os.getenv("OLLAMA_HOST", "http://host.docker.internal:11434")
LMSTUDIO_HOST = os.getenv("LMSTUDIO_HOST", "http://host.docker.internal:1234")
LLM_MODEL = os.getenv("LLM_MODEL", "llama3")
TTS_ADAPTER_URL = os.getenv("TTS_ADAPTER_URL", "http://tts-11labs-adapter:8082")
FISHSPEECH_URL = os.getenv("FISHSPEECH_URL", "http://tts:8081")


@activity.defn(name="generate_reply")
def generate_reply(input: Dict[str, Any]) -> Dict[str, Any]:
    text = input.get("message") or ""
    # Try Ollama (local) first
    try:
        r = requests.post(
            f"{OLLAMA_HOST}/api/generate",
            json={"model": LLM_MODEL, "prompt": text, "stream": False},
            timeout=20,
        )
        if r.ok:
            data = r.json()
            return {"reply_text": data.get("response", "")}
    except Exception:
        pass

    # Try LM Studio (OpenAI-compatible)
    try:
        r = requests.post(
            f"{LMSTUDIO_HOST}/v1/chat/completions",
            json={
                "model": LLM_MODEL,
                "messages": [
                    {"role": "system", "content": "You are a helpful assistant."},
                    {"role": "user", "content": text},
                ],
                "stream": False,
            },
            timeout=20,
        )
        if r.ok:
            data = r.json()
            choices = data.get("choices") or []
            if choices:
                content = choices[0].get("message", {}).get("content", "")
                return {"reply_text": content}
    except Exception:
        pass

    # Fallback
    return {"reply_text": f"Echo: {text}"}


@activity.defn(name="synthesize_tts")
def synthesize_tts(input: Dict[str, Any]) -> str:
    text = input.get("text") or ""
    # Prefer ElevenLabs adapter if configured
    if os.getenv("ELEVENLABS_API_KEY"):
        try:
            r = requests.post(f"{TTS_ADAPTER_URL}/synthesize", json={"text": text}, timeout=20)
            if r.ok:
                return r.json().get("url", "")
        except Exception:
            pass
    # Fallback to FishSpeech container if available
    try:
        r = requests.post(f"{FISHSPEECH_URL}/tts", json={"text": text}, timeout=25)
        if r.ok:
            return r.json().get("url", "")
    except Exception:
        pass
    return ""


@activity.defn(name="upsert_vector_memory")
def upsert_vector_memory(input: Dict[str, Any]) -> None:
    # Stub: integrate with Qdrant later
    return None


@activity.defn(name="update_graph")
def update_graph(input: Dict[str, Any]) -> None:
    # Stub: integrate with Neo4j later
    return None
