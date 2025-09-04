import os
import requests
from typing import Dict, Any, Optional
from temporalio import activity

OLLAMA_HOST = os.getenv("OLLAMA_HOST", "http://localhost:11434")
LMSTUDIO_HOST = os.getenv("LMSTUDIO_HOST", "http://localhost:1234")
LLM_MODEL = os.getenv("LLM_MODEL", "llama3")
TTS_ADAPTER_URL = os.getenv("TTS_ADAPTER_URL", "http://tts-11labs-adapter:8082")
FISHSPEECH_URL = os.getenv("FISHSPEECH_URL", "http://tts:8081")


@activity.defn(name="generate_reply")
def generate_reply(input: Dict[str, Any]) -> Dict[str, Any]:
    text = input.get("message") or ""
    engine = (input.get("engine") or "").strip().lower() or None
    model = input.get("model") or LLM_MODEL
    conn = input.get("conn") or {}
    ollama_base = (conn.get("ollama_base") or OLLAMA_HOST)
    lmstudio_base = (conn.get("lmstudio_base") or LMSTUDIO_HOST)
    openai_base = (conn.get("openai_base") or os.getenv("OPENAI_API_BASE", "https://api.openai.com/v1"))
    openai_key = (conn.get("openai_api_key") or os.getenv("OPENAI_API_KEY", ""))
    claude_api_url = (conn.get("claude_api_url") or os.getenv("CLAUDE_API_URL", "https://api.anthropic.com/v1"))
    claude_api_key = (conn.get("claude_api_key") or os.getenv("CLAUDE_API_KEY", ""))
    vllm_base = (conn.get("vllm_base") or os.getenv("VLLM_API_URL", ""))
    vllm_key = (conn.get("vllm_api_key") or os.getenv("VLLM_API_KEY", ""))

    # Helper: Ollama
    def try_ollama() -> Optional[str]:
        try:
            r = requests.post(
                f"{ollama_base}/api/generate",
                json={"model": model, "prompt": text, "stream": False},
                timeout=20,
            )
            if r.ok:
                data = r.json()
                return data.get("response", "")
        except Exception:
            pass
        return None

    # Helper: LM Studio (OpenAI-compatible)
    def try_lmstudio() -> Optional[str]:
        try:
            r = requests.post(
                f"{lmstudio_base}/v1/chat/completions",
                json={
                    "model": model,
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
                    return choices[0].get("message", {}).get("content", "")
        except Exception:
            pass
        return None

    # OpenAI-compatible (OpenAI or vLLM)
    def try_openai_like(base: str, api_key: str = "") -> Optional[str]:
        if not base:
            return None
        headers = {"Content-Type": "application/json"}
        if api_key:
            headers["Authorization"] = f"Bearer {api_key}"
        try:
            r = requests.post(
                f"{base}/chat/completions",
                headers=headers,
                json={
                    "model": model,
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
                    return choices[0].get("message", {}).get("content", "")
        except Exception:
            pass
        return None

    # Routing: honor explicit engine only, no cycling
    reply: Optional[str] = None
    if engine == "lmstudio":
        reply = try_lmstudio()
    elif engine == "ollama":
        reply = try_ollama()
    elif engine == "openai":
        reply = try_openai_like(openai_base, openai_key)
    elif engine == "claude":
        # Claude uses a different schema; map via anthropic v1
        try:
            if claude_api_key:
                r = requests.post(
                    f"{claude_api_url}/messages",
                    headers={
                        "x-api-key": claude_api_key,
                        "anthropic-version": "2023-06-01",
                        "content-type": "application/json",
                    },
                    json={
                        "model": model,
                        "max_tokens": 512,
                        "messages": [
                            {"role": "user", "content": text},
                        ],
                    },
                    timeout=20,
                )
                if r.ok:
                    data = r.json()
                    content = data.get("content")
                    if isinstance(content, list) and content:
                        part = content[0]
                        if isinstance(part, dict):
                            reply = part.get("text") or ""
        except Exception:
            reply = None
    elif engine == "vllm":
        reply = try_openai_like(vllm_base, vllm_key)
    else:
        return {"reply_text": "Error: No LLM engine selected in settings."}

    if reply is not None and reply != "":
        return {"reply_text": reply}
    return {"reply_text": "Error: LLM request failed. Check your model and connection settings."}


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
