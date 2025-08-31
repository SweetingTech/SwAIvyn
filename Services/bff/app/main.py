import os
import asyncio
import sys
import json
from typing import Optional, Dict, Any

import httpx
from fastapi import FastAPI, HTTPException, UploadFile, File, Form
from pydantic import BaseModel
from temporalio.client import Client
import httpx
from fastapi import Depends
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from sqlalchemy.ext.asyncio import AsyncEngine
from sqlalchemy import select, text
from datetime import timedelta
from pydantic import BaseModel
from .auth import create_access_token, verify_password, get_current_user, require_admin
from .models import users, chat_settings as t_chat_settings, connection_settings as t_conn_settings, characters as t_characters, conversations as t_conversations, messages as t_messages


TEMPORAL_HOST = os.getenv("TEMPORAL_HOST", "temporal:7233")
OLLAMA_HOST = os.getenv("OLLAMA_HOST", "http://host.docker.internal:11434")
LMSTUDIO_HOST = os.getenv("LMSTUDIO_HOST", "http://host.docker.internal:1234")
DATABASE_URL = os.getenv("DATABASE_URL")
UPLOADS_DIR = os.getenv("UPLOADS_DIR", "/app/wwwroot/uploads")
CHAR_UPLOADS_DIR = os.path.join(UPLOADS_DIR, "characters")


class ChatRequest(BaseModel):
    message: str
    user_id: Optional[str] = None
    session_id: Optional[str] = None


class ChatResponse(BaseModel):
    reply_text: str
    tts_url: Optional[str] = None
    workflow_id: Optional[str] = None
    run_id: Optional[str] = None


from fastapi.middleware.cors import CORSMiddleware

app = FastAPI(title="SwAIvyn BFF", version="0.1.0")
os.makedirs(CHAR_UPLOADS_DIR, exist_ok=True)
try:
    from fastapi.staticfiles import StaticFiles
    app.mount("/uploads", StaticFiles(directory=UPLOADS_DIR), name="uploads")
except Exception:
    pass
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5173", "http://127.0.0.1:5173"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"]
)
_temporal_client: Optional[Client] = None
_engine: Optional[AsyncEngine] = None
_chat_settings_store: Dict[str, Dict[str, Any]] = {}
_connections_store: Dict[str, Dict[str, Any]] = {}


# Auth dependency wired with engine
security = HTTPBearer(auto_error=False)

async def current_user_dep(
    creds: Optional[HTTPAuthorizationCredentials] = Depends(security),
    engine: Optional[AsyncEngine] = Depends(lambda: _engine),
):
    return await get_current_user(engine, creds)


async def _connect_with_retry(addr: str) -> Client:
    delay = 1
    last_err = None
    for attempt in range(1, 16):
        try:
            return await Client.connect(addr)
        except Exception as e:
            last_err = e
            print(f"Temporal connect failed (attempt {attempt}): {e}", file=sys.stderr, flush=True)
            await asyncio.sleep(delay)
            delay = min(delay * 2, 5)
    raise last_err


@app.on_event("startup")
async def _startup_connect_temporal():
    global _temporal_client
    try:
        _temporal_client = await _connect_with_retry(TEMPORAL_HOST)
    except Exception as e:
        # Don't fail startup; connect lazily on first request
        print(f"Temporal not ready at startup: {e}", file=sys.stderr, flush=True)


@app.on_event("startup")
async def _startup_db_seed():
    # Initialize DB and seed default users if DB is configured
    global _engine
    if DATABASE_URL:
        try:
            from .db import create_engine
            from .seed import ensure_seed

            _engine = create_engine()
            if _engine is not None:
                await ensure_seed(_engine)
                print("DB ready and users seeded", flush=True)
        except Exception as e:
            print(f"DB init/seed failed: {e}", file=sys.stderr, flush=True)


@app.get("/healthz")
async def healthz():
    return {"status": "ok"}


@app.get("/readyz")
async def readyz():
    global _temporal_client
    try:
        if _temporal_client is None:
            _temporal_client = await _connect_with_retry(TEMPORAL_HOST)
        # Lightweight call to verify connectivity by listing namespaces (implicit ping)
        # Note: If SDK lacks a ping, we assume connect() suffices.
        return {"status": "ready"}
    except Exception as e:
        raise HTTPException(status_code=503, detail=f"Not ready: {e}")


# Dev/proxy compatibility: allow frontend to call /api/* for health endpoints
@app.get("/api/healthz")
async def api_healthz():
    return await healthz()


@app.get("/api/readyz")
async def api_readyz():
    return await readyz()


@app.get("/api/llm/health")
async def llm_health(userId: Optional[str] = None):
    status = {"ollama": None, "lmstudio": None}
    uid = userId or "default"
    user_conn = _connections_store.get(uid, {})
    ollama_base = user_conn.get("OllamaApiUrl") or OLLAMA_HOST
    lmstudio_base = user_conn.get("LmStudioApiUrl") or LMSTUDIO_HOST
    async with httpx.AsyncClient(timeout=3) as client:
        # Ollama: list tags
        try:
            r = await client.get(f"{ollama_base}/api/tags")
            status["ollama"] = {"ok": r.status_code == 200}
        except Exception as e:
            status["ollama"] = {"ok": False, "error": str(e)}

        # LM Studio: OpenAI-compatible models
        try:
            r = await client.get(f"{lmstudio_base}/v1/models")
            status["lmstudio"] = {"ok": r.status_code == 200}
        except Exception as e:
            status["lmstudio"] = {"ok": False, "error": str(e)}

    return status


# ------------------------- Users & Settings -------------------------

async def get_engine() -> Optional[AsyncEngine]:
    return _engine


@app.get("/api/user/default")
async def get_default_user(engine: Optional[AsyncEngine] = Depends(get_engine)):
    # If DB present, return the default user (or first user). Otherwise return an in-memory default.
    if engine is not None:
        try:
            from .models import users
            async with engine.connect() as conn:
                res = await conn.execute(select(users).where(users.c.is_default == True).limit(1))
                row = res.first()
                if not row:
                    res = await conn.execute(select(users).limit(1))
                    row = res.first()
                if row:
                    r = row._mapping
                    return {"id": r["id"], "username": r["username"], "email": r["email"]}
        except Exception as e:
            print(f"user/default fetch failed: {e}", file=sys.stderr, flush=True)
    # Fallback inline user
    return {"id": "djay", "username": "DJay", "email": "djay@example.com"}


class LoginRequest(BaseModel):
    username: Optional[str] = None
    email: Optional[str] = None
    password: str


@app.post("/api/auth/login")
async def login(body: LoginRequest, engine: Optional[AsyncEngine] = Depends(get_engine)):
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    identifier = (body.username or body.email or "").strip()
    if not identifier:
        raise HTTPException(status_code=400, detail="username or email required")
    async with engine.connect() as conn:
        stmt = select(users).where((users.c.username == identifier) | (users.c.email == identifier)).limit(1)
        res = await conn.execute(stmt)
        row = res.first()
        if not row:
            raise HTTPException(status_code=401, detail="Invalid credentials")
        r = row._mapping
        if not r["password_hash"] or not verify_password(body.password, r["password_hash"]):
            raise HTTPException(status_code=401, detail="Invalid credentials")
        token = create_access_token({"sub": r["id"], "role": r["role"]})
        return {"access_token": token, "token_type": "bearer", "user": {"id": r["id"], "username": r["username"], "email": r["email"], "role": r["role"]}}


@app.get("/api/auth/me")
async def auth_me(current=Depends(current_user_dep)):
    return current


class PasswordChange(BaseModel):
    old_password: str
    new_password: str


@app.post("/api/auth/change-password")
async def change_password(body: PasswordChange, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    if not current or not current.get("id"):
        raise HTTPException(status_code=401, detail="Unauthorized")
    uid = current.get("id")
    async with engine.begin() as conn:
        res = await conn.execute(select(users).where(users.c.id == uid).limit(1))
        row = res.first()
        if not row:
            raise HTTPException(status_code=404, detail="User not found")
        r = row._mapping
        if not r["password_hash"] or not verify_password(body.old_password, r["password_hash"]):
            raise HTTPException(status_code=401, detail="Invalid credentials")
        import bcrypt as _bcrypt
        pw_hash = _bcrypt.hashpw(body.new_password.encode(), _bcrypt.gensalt()).decode()
        await conn.execute(users.update().where(users.c.id == uid).values(password_hash=pw_hash))
    return {"success": True}


class UserCreate(BaseModel):
    id: str
    username: str
    email: str
    password: str
    role: str = "user"
    language: str = "en"
    theme: str = "light"
    default_character: Optional[str] = None
    is_default: bool = False


@app.get("/api/admin/users")
async def admin_list_users(engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    require_admin(current)
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    async with engine.connect() as conn:
        res = await conn.execute(select(users))
        rows = [dict(row._mapping) for row in res.fetchall()]
        for r in rows:
            r.pop("password_hash", None)
        return rows


@app.post("/api/admin/users")
async def admin_create_user(body: UserCreate, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    require_admin(current)
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    import bcrypt as _bcrypt
    pw_hash = _bcrypt.hashpw(body.password.encode(), _bcrypt.gensalt()).decode()
    async with engine.begin() as conn:
        await conn.execute(users.insert().values(
            id=body.id,
            username=body.username,
            email=body.email,
            password_hash=pw_hash,
            role=body.role,
            language=body.language,
            theme=body.theme,
            default_character=body.default_character,
            is_default=body.is_default,
        ))
    return {"success": True}


class UserUpdate(BaseModel):
    username: Optional[str] = None
    email: Optional[str] = None
    language: Optional[str] = None
    theme: Optional[str] = None
    default_character: Optional[str] = None


@app.get("/api/user/{user_id}")
async def get_user(user_id: str, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    # Allow anonymous read of basic user profile in dev/stub mode. Auth required for cross-user reads when a token is present.
    if current is not None and (current.get("id") != user_id and current.get("role") != "admin"):
        raise HTTPException(status_code=403, detail="Forbidden")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    async with engine.connect() as conn:
        res = await conn.execute(select(users).where(users.c.id == user_id))
        row = res.first()
        if not row:
            raise HTTPException(status_code=404, detail="Not found")
        r = row._mapping
        return {"id": r["id"], "username": r["username"], "email": r["email"], "language": r["language"], "theme": r["theme"], "default_character": r["default_character"], "role": r["role"]}


@app.put("/api/user/{user_id}")
async def update_user(user_id: str, body: UserUpdate, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    # user can update self or admin can update anyone
    if not current or (current.get("id") != user_id and current.get("role") != "admin"):
        raise HTTPException(status_code=403, detail="Forbidden")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    updates = {k: v for k, v in body.model_dump().items() if v is not None}
    if not updates:
        return {"success": True}
    async with engine.begin() as conn:
        await conn.execute(users.update().where(users.c.id == user_id).values(**updates))
    return {"success": True}


@app.get("/api/settings/llm")
async def get_llm_settings(userId: Optional[str] = None):
    # Minimal settings for initialization
    uid = userId or "default"
    s = _chat_settings_store.get(uid) or _default_chat_settings(uid)
    return {"engine": s.get("llmEngine", "ollama"), "model": s.get("llmModel", os.getenv("LLM_MODEL", "llama3"))}


@app.put("/api/settings/llm")
async def put_llm_settings(payload: dict):
    uid = payload.get("userId") or "default"
    s = _chat_settings_store.get(uid) or _default_chat_settings(uid)
    engine = payload.get("engine") or s.get("llmEngine")
    model = payload.get("model") or ""
    s["llmEngine"] = engine
    s["llmModel"] = model
    s.setdefault("engineModels", {})[engine] = model
    _chat_settings_store[uid] = s
    return {"success": True}


async def _load_connections(uid: str) -> Dict[str, Any]:
    if _engine is None:
        return {}
    try:
        async with _engine.connect() as conn:
            res = await conn.execute(select(t_conn_settings).where(t_conn_settings.c.user_id == uid).limit(1))
            row = res.first()
            return dict(row._mapping) if row else {}
    except Exception:
        return {}


@app.get("/api/settings/connections")
async def get_connections_settings(userId: Optional[str] = None):
    uid = userId or "default"
    s = _connections_store.get(uid, {})
    if _engine is not None:
        db = await _load_connections(uid)
        if db:
            s = {**s, **db}
    return {
        "OpenAiApiKey": s.get("OpenAiApiKey", os.getenv("OPENAI_API_KEY", "")),
        "ClaudeApiKey": s.get("ClaudeApiKey", os.getenv("CLAUDE_API_KEY", "")),
        "ClaudeApiUrl": s.get("ClaudeApiUrl", os.getenv("CLAUDE_API_URL", "https://api.anthropic.com/v1")),
        "OllamaApiUrl": s.get("OllamaApiUrl", os.getenv("OLLAMA_HOST", OLLAMA_HOST)),
        "LmStudioApiUrl": s.get("LmStudioApiUrl", os.getenv("LMSTUDIO_HOST", LMSTUDIO_HOST)),
        "EnableStreaming": bool(s.get("EnableStreaming", True)),
        "TtsGpu": s.get("TtsGpu"),
        "SttGpu": s.get("SttGpu"),
    }


@app.post("/api/settings/connections")
async def update_connections_settings(body: dict):
    uid = str(body.get("UserId") or body.get("userId") or "default")
    _connections_store[uid] = {**_connections_store.get(uid, {}), **body}
    if _engine is not None:
        async with _engine.begin() as conn:
            await conn.execute(t_conn_settings.delete().where(t_conn_settings.c.user_id == uid))
            await conn.execute(
                t_conn_settings.insert().values(
                    user_id=uid,
                    OpenAiApiKey=body.get("OpenAiApiKey"),
                    ClaudeApiKey=body.get("ClaudeApiKey"),
                    ClaudeApiUrl=body.get("ClaudeApiUrl"),
                    OllamaApiUrl=body.get("OllamaApiUrl"),
                    LmStudioApiUrl=body.get("LmStudioApiUrl"),
                    EnableStreaming=bool(body.get("EnableStreaming", True)),
                    TtsGpu=body.get("TtsGpu"),
                    SttGpu=body.get("SttGpu"),
                )
            )
    return {"success": True}


@app.put("/api/settings/connections")
async def put_connections_settings(body: dict):
    uid = str(body.get("UserId") or body.get("userId") or "default")
    _connections_store[uid] = {**_connections_store.get(uid, {}), **body}
    if _engine is not None:
        async with _engine.begin() as conn:
            await conn.execute(t_conn_settings.delete().where(t_conn_settings.c.user_id == uid))
            await conn.execute(
                t_conn_settings.insert().values(
                    user_id=uid,
                    OpenAiApiKey=body.get("OpenAiApiKey"),
                    ClaudeApiKey=body.get("ClaudeApiKey"),
                    ClaudeApiUrl=body.get("ClaudeApiUrl"),
                    OllamaApiUrl=body.get("OllamaApiUrl"),
                    LmStudioApiUrl=body.get("LmStudioApiUrl"),
                    EnableStreaming=bool(body.get("EnableStreaming", True)),
                    TtsGpu=body.get("TtsGpu"),
                    SttGpu=body.get("SttGpu"),
                )
            )
    return {"success": True}


@app.get("/api/tts/settings")
async def get_tts_settings(userId: Optional[str] = None):
    # Provide richer settings so the Voice tab can render both providers
    return {
        "apiKey": os.getenv("ELEVENLABS_API_KEY", ""),
        "voiceId": os.getenv("DEFAULT_TTS_VOICE", "glados"),
        "fishSpeechApiKey": os.getenv("FISHSPEECH_API_KEY", ""),
        "ttsProvider": os.getenv("DEFAULT_TTS_PROVIDER", "fishspeech"),
        "providers": [
            {"id": "elevenlabs", "name": "ElevenLabs", "available": True},
            {"id": "fishspeech", "name": "Fish Speech", "available": True},
        ],
    }


@app.post("/api/tts/settings")
async def set_tts_settings(body: dict):
    # Accept but do not persist (stub)
    return {"success": True}


@app.get("/api/tts/voices")
async def list_tts_voices(provider: str = "fishspeech", userId: Optional[str] = None):
    # Stub voice lists
    if provider.lower() == "fishspeech":
        return {"provider": "fishspeech", "voices": ["jazzy", "glados", "scarlet"]}
    if provider.lower() == "elevenlabs":
        # Optionally call ElevenLabs adapter here later
        return {"provider": "elevenlabs", "voices": ["Bella", "Rachel", "Charlie"]}
    return {"provider": provider, "voices": []}


@app.get("/api/tts/voices/{voice_name}/details")
async def tts_voice_details(voice_name: str):
    return {
        "name": voice_name,
        "transcript": "Sample transcript",
        "hasAudioFile": False,
        "hasEmbedding": False,
        "createdAt": datetime.utcnow().isoformat() + "Z",
        "audioFileSize": 0,
    }


from fastapi import Response
from datetime import datetime


@app.post("/api/tts/synthesize")
async def tts_synthesize(body: dict):
    # Return a 0.5s silence WAV to satisfy the UI
    import struct
    sample_rate = 16000
    duration_sec = 0.5
    num_samples = int(sample_rate * duration_sec)
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
    return Response(content=wav_header + pcm_data, media_type="audio/wav")


@app.get("/api/settings/DefaultCharacterId")
async def get_default_character_id(userId: Optional[str] = None, engine: Optional[AsyncEngine] = Depends(get_engine)):
    # For compatibility with frontend, return { value: "id" }
    if engine is not None and userId:
        try:
            async with engine.connect() as conn:
                res = await conn.execute(text("SELECT default_character FROM users WHERE id = :uid"), {"uid": userId})
                row = res.first()
                if row and row[0]:
                    return {"value": row[0]}
        except Exception:
            pass
    return {"value": "default"}


@app.get("/api/character/user/{user_id}")
async def get_user_characters(user_id: str, engine: Optional[AsyncEngine] = Depends(get_engine)):
    # Try DB first
    if engine is not None:
        try:
            async with engine.connect() as conn:
                res = await conn.execute(
                    text(
                        """
                        SELECT id, name,
                               COALESCE(system_prompt, '') AS system_prompt,
                               COALESCE(image_path, '') AS image_path
                        FROM characters
                        WHERE user_id = :uid OR user_id IS NULL
                        ORDER BY name
                        """
                    ),
                    {"uid": user_id},
                )
                rows = res.fetchall()
                if rows:
                    return [
                        {
                            "id": r[0],
                            "name": r[1],
                            "systemPrompt": r[2],
                            "imagePath": r[3],
                        }
                        for r in rows
                    ]
        except Exception:
            pass
    # Fallback default if no DB or empty
    return [
        {"id": "default", "name": "Default", "systemPrompt": "You are a helpful AI assistant.", "imagePath": ""}
    ]


@app.post("/api/character/image")
async def upload_character_image(file: UploadFile = File(...), character_id: Optional[str] = Form(None), engine: Optional[AsyncEngine] = Depends(get_engine)):
    import uuid as _uuid
    ext = os.path.splitext(file.filename or "")[1].lower()
    if ext not in [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"]:
        ext = ".png"
    fname = f"{_uuid.uuid4().hex}{ext}"
    dest = os.path.join(CHAR_UPLOADS_DIR, fname)
    os.makedirs(CHAR_UPLOADS_DIR, exist_ok=True)
    content = await file.read()
    with open(dest, "wb") as f:
        f.write(content)
    public_path = f"/uploads/characters/{fname}"
    if engine is not None and character_id:
        try:
            async with engine.begin() as conn:
                await conn.execute(t_characters.update().where(t_characters.c.id == character_id).values(image_path=public_path))
        except Exception:
            pass
    return {"imagePath": public_path}


@app.post("/api/character/import-yaml")
async def import_character_yaml(payload: dict, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    try:
        import yaml  # type: ignore
    except Exception:
        raise HTTPException(status_code=500, detail="PyYAML not installed on server")
    raw = payload.get("yaml") or ""
    if not raw:
        raise HTTPException(status_code=400, detail="yaml required")
    try:
        data = yaml.safe_load(raw) or {}
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Invalid YAML: {e}")
    name = str(data.get("name") or "Imported Character")
    parts = []
    for k in ("description", "personality", "scenario", "instructions"):
        v = data.get(k)
        if isinstance(v, str) and v.strip():
            parts.append(v.strip())
    system_prompt = "\n\n".join(parts) or "You are a helpful AI assistant."
    # optional image download
    img_url = None
    for key in ("image", "image_url", "imageUrl", "avatar", "avatar_url", "avatarUrl"):
        v = data.get(key)
        if isinstance(v, str) and v.strip():
            img_url = v.strip()
            break
    image_path = ""
    if img_url and (img_url.startswith("http://") or img_url.startswith("https://")):
        try:
            async with httpx.AsyncClient(timeout=10) as client:
                resp = await client.get(img_url)
                resp.raise_for_status()
                ext = os.path.splitext(img_url)[1].lower()
                if ext not in [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"]:
                    ctype = resp.headers.get("content-type", "")
                    if "jpeg" in ctype:
                        ext = ".jpg"
                    elif "png" in ctype:
                        ext = ".png"
                    elif "webp" in ctype:
                        ext = ".webp"
                    else:
                        ext = ".png"
                import uuid as _uuid
                fname = f"{_uuid.uuid4().hex}{ext}"
                os.makedirs(CHAR_UPLOADS_DIR, exist_ok=True)
                with open(os.path.join(CHAR_UPLOADS_DIR, fname), "wb") as f:
                    f.write(resp.content)
                image_path = f"/uploads/characters/{fname}"
        except Exception:
            image_path = ""
    import uuid
    cid = str(uuid.uuid4())
    if engine is not None:
        async with engine.begin() as conn:
            await conn.execute(t_characters.insert().values(
                id=cid,
                user_id=(current or {}).get("id"),
                name=name,
                system_prompt=system_prompt,
                image_path=image_path,
            ))
    return {"id": cid, "name": name, "imagePath": image_path}


# ------------------------- Chat Routes (compat) -------------------------

@app.post("/api/conversation/chat")
async def conversation_chat(body: dict):
    # Map to orchestrated chat
    msg = body.get("message") or ""
    session_id = body.get("conversationId") or body.get("session_id")
    req = ChatRequest(message=msg, session_id=session_id)
    # reuse existing chat workflow logic
    resp = await chat(req)
    return {"response": resp.reply_text, "tts_url": resp.tts_url}


import uuid


@app.post("/api/conversation")
async def conversation_create(body: dict, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    conv_id = str(uuid.uuid4())
    title = body.get("title") or "New Chat"
    folder_id = body.get("folderId")
    now = datetime.utcnow().isoformat() + "Z"
    user_id = (current or {}).get("id") or body.get("userId") or "00000000-0000-0000-0000-000000000001"
    if engine is not None:
        try:
            async with engine.begin() as conn:
                await conn.execute(
                    t_conversations.insert().values(
                        id=conv_id,
                        user_id=user_id,
                        title=title,
                        folder_id=folder_id,
                        created_at=now,
                        last_updated=now,
                    )
                )
        except Exception as e:
            print(f"conversation create failed: {e}", file=sys.stderr, flush=True)
    return {
        "id": conv_id,
        "userId": user_id,
        "title": title,
        "folderId": folder_id,
        "createdAt": now,
        "lastUpdated": now,
    }


@app.put("/api/conversation/{conv_id}/title")
async def conversation_update_title(conv_id: str, body: dict, engine: Optional[AsyncEngine] = Depends(get_engine)):
    if engine is not None:
        async with engine.begin() as conn:
            await conn.execute(
                t_conversations.update().where(t_conversations.c.id == conv_id).values(
                    title=body.get("title") or "New Chat",
                    last_updated=datetime.utcnow().isoformat() + "Z",
                )
            )
    return {"success": True}


@app.put("/api/conversation/{conv_id}/folder")
async def conversation_update_folder(conv_id: str, body: dict, engine: Optional[AsyncEngine] = Depends(get_engine)):
    if engine is not None:
        async with engine.begin() as conn:
            await conn.execute(
                t_conversations.update().where(t_conversations.c.id == conv_id).values(
                    folder_id=body.get("folderId"),
                    last_updated=datetime.utcnow().isoformat() + "Z",
                )
            )
    return {"success": True}


@app.get("/api/conversation/{conv_id}/messages")
async def conversation_messages(conv_id: str, engine: Optional[AsyncEngine] = Depends(get_engine)):
    if engine is not None:
        try:
            async with engine.connect() as conn:
                res = await conn.execute(select(t_messages).where(t_messages.c.conversation_id == conv_id))
                rows = res.fetchall()
                return [dict(r._mapping) for r in rows]
        except Exception as e:
            print(f"messages fetch failed: {e}", file=sys.stderr, flush=True)
    return []


@app.post("/api/conversation/message")
async def conversation_append_message(body: dict, engine: Optional[AsyncEngine] = Depends(get_engine)):
    now = datetime.utcnow().isoformat() + "Z"
    mid = str(uuid.uuid4())
    rec = {
        "id": mid,
        "conversationId": body.get("conversationId"),
        "role": body.get("role", "user"),
        "content": body.get("content", ""),
        "timestamp": now,
    }
    if engine is not None:
        try:
            async with engine.begin() as conn:
                await conn.execute(
                    t_messages.insert().values(
                        id=mid,
                        conversation_id=rec["conversationId"],
                        role=rec["role"],
                        content=rec["content"],
                        timestamp=now,
                    )
                )
        except Exception as e:
            print(f"append message failed: {e}", file=sys.stderr, flush=True)
    return rec


@app.get("/api/folder/user/{user_id}")
async def list_folders(user_id: str):
    # Stub empty folder list
    return []


@app.get("/api/conversation/user/{user_id}")
async def list_conversations(user_id: str, engine: Optional[AsyncEngine] = Depends(get_engine)):
    if engine is not None:
        try:
            async with engine.connect() as conn:
                res = await conn.execute(select(t_conversations).where(t_conversations.c.user_id == user_id))
                rows = res.fetchall()
                out = []
                for r in rows:
                    m = r._mapping
                    out.append({
                        "id": m["id"],
                        "userId": m["user_id"],
                        "title": m["title"],
                        "folderId": m["folder_id"],
                        "createdAt": m["created_at"],
                        "lastUpdated": m["last_updated"],
                    })
                return out
        except Exception as e:
            print(f"list conv failed: {e}", file=sys.stderr, flush=True)
    return []


@app.get("/api/dashboard/status")
async def dashboard_status():
    # Minimal status object
    return {"status": "ok", "services": {"bff": True, "temporal": True}}


@app.get("/api/memorysyncstatus/status/{user_id}")
async def memorysync_status(user_id: str):
    now = datetime.utcnow().isoformat() + "Z"
    return {
        "userId": user_id,
        "sqliteCount": 0,
        "neo4jCount": 0,
        "inSync": True,
        "missingInNeo4j": {"count": 0, "memoryIds": [], "details": []},
        "missingInSqlite": {"count": 0, "memoryIds": []},
        "timestamp": now,
    }


@app.post("/api/memorysyncstatus/repair/{user_id}")
async def memorysync_repair(user_id: str):
    now = datetime.utcnow().isoformat() + "Z"
    return {
        "userId": user_id,
        "totalMissingMemories": 0,
        "successfulRepairs": 0,
        "failedRepairs": 0,
        "repairDetails": [],
        "timestamp": now,
    }


@app.post("/api/memorysyncstatus/full/{user_id}")
async def memorysync_full(user_id: str):
    now = datetime.utcnow().isoformat() + "Z"
    base = await memorysync_status(user_id)
    return {
        "userId": user_id,
        "initialStatus": base,
        "finalStatus": base,
        "repairResult": None,
        "summary": {
            "wasInSync": True,
            "isNowInSync": True,
            "improvementMade": False,
            "totalInitialIssues": 0,
            "totalRemainingIssues": 0,
            "issuesResolved": 0,
        },
        "timestamp": now,
    }


@app.get("/api/upload/documents")
async def list_documents():
    return []


@app.post("/api/upload/documents")
async def create_document():
    return {"id": "stub", "status": "created"}


@app.post("/api/upload/documents/{doc_id}")
async def update_document(doc_id: str):
    return {"id": doc_id, "status": "updated"}


@app.post("/api/upload/documents/{doc_id}/reprocess")
async def reprocess_document(doc_id: str):
    return {"id": doc_id, "status": "queued"}


# Memory endpoints used by Memory page
@app.get("/api/memory/{user_id}")
async def list_memories(user_id: str):
    return []


@app.post("/api/memory")
async def create_memory(item: dict):
    now = datetime.utcnow().isoformat() + "Z"
    return {
        "id": str(uuid.uuid4()),
        "userId": item.get("userId"),
        "content": item.get("content", ""),
        "category": item.get("category", "Personal"),
        "isShared": bool(item.get("isShared")),
        "createdAt": now,
        "lastAccessed": now,
    }


@app.delete("/api/memory/{memory_id}")
async def delete_memory(memory_id: str):
    return {"success": True}


@app.get("/api/agents")
async def list_agents(userId: Optional[str] = None):
    return []


@app.post("/api/agents/{agent_id}/start")
async def start_agent(agent_id: str):
    return {"id": agent_id, "status": "started"}


@app.post("/api/agents/{agent_id}/stop")
async def stop_agent(agent_id: str):
    return {"id": agent_id, "status": "stopped"}


@app.delete("/api/agents/{agent_id}")
async def delete_agent(agent_id: str):
    return {"id": agent_id, "status": "deleted"}


@app.get("/api/llm/models")
async def list_llm_models(engine: str = "ollama", userId: Optional[str] = None):
    try:
        uid = userId or "default"
        user_conn = _connections_store.get(uid, {})
        ollama_base = user_conn.get("OllamaApiUrl") or OLLAMA_HOST
        lmstudio_base = user_conn.get("LmStudioApiUrl") or LMSTUDIO_HOST
        async with httpx.AsyncClient(timeout=5) as client:
            if engine.lower() == "ollama":
                r = await client.get(f"{ollama_base}/api/tags")
                r.raise_for_status()
                data = r.json()
                models = [m.get("name") for m in data.get("models", []) if m.get("name")]
                return {"engine": "ollama", "models": models}
            elif engine.lower() == "lmstudio":
                r = await client.get(f"{lmstudio_base}/v1/models")
                r.raise_for_status()
                data = r.json()
                models = [m.get("id") for m in data.get("data", []) if m.get("id")]
                return {"engine": "lmstudio", "models": models}
    except Exception as e:
        raise HTTPException(status_code=502, detail=f"Model listing failed: {e}")
    return {"engine": engine, "models": []}


# Back-compat aliases for LLm routes used by the frontend
@app.get("/api/llm/ollama/models")
async def list_ollama_models(userId: Optional[str] = None):
    data = await list_llm_models(engine="ollama", userId=userId)
    return data


@app.get("/api/llm/lmstudio/models")
async def list_lmstudio_models(userId: Optional[str] = None):
    data = await list_llm_models(engine="lmstudio", userId=userId)
    return data


@app.get("/api/llm/lmstudio/model")
async def get_lmstudio_selected_model(userId: Optional[str] = None):
    # Try LM Studio live endpoint first (api/v0/models), fall back to stored/env
    try:
        uid = userId or "default"
        user_conn = _connections_store.get(uid, {})
        lmstudio_base = user_conn.get("LmStudioApiUrl") or LMSTUDIO_HOST
        async with httpx.AsyncClient(timeout=3) as client:
            r = await client.get(f"{lmstudio_base}/api/v0/models")
            if r.status_code == 200:
                data = r.json()
                items = data if isinstance(data, list) else data.get("data") or []
                loaded = next((m for m in items if m.get("loaded") or m.get("isLoaded")), None)
                if loaded and loaded.get("id"):
                    return {"model": loaded.get("id")}
    except Exception:
        pass
    # Fallback to any saved per-user selection or env
    if userId:
        s = _chat_settings_store.get(userId)
        if s and s.get("engineModels", {}).get("lmstudio"):
            return {"model": s["engineModels"]["lmstudio"]}
    return {"model": os.getenv("LLM_MODEL", "llama3")}


def _default_chat_settings(user_id: str) -> Dict[str, Any]:
    return {
        "llmEngine": "ollama",
        "llmModel": os.getenv("LLM_MODEL", "llama3"),
        "enabledEngines": {
            "ollama": True,
            "lmstudio": True,
            "openai": False,
            "claude": False,
            "vllm": False,
        },
        "engineModels": {
            "ollama": os.getenv("LLM_MODEL", "llama3"),
            "lmstudio": "",
            "openai": "",
            "claude": "",
            "vllm": "",
        },
        "ttsProvider": "fishspeech",
        "ttsVoiceId": "glados",
    }


@app.get("/api/chat/settings/{user_id}")
async def get_chat_settings(user_id: str, engine: Optional[AsyncEngine] = Depends(get_engine)):
    if engine is not None:
        try:
            async with engine.connect() as conn:
                res = await conn.execute(select(t_chat_settings).where(t_chat_settings.c.user_id == user_id).limit(1))
                row = res.first()
                if row:
                    m = row._mapping
                    return {
                        "llmEngine": m.get("llm_engine") or "ollama",
                        "llmModel": m.get("llm_model") or os.getenv("LLM_MODEL", "llama3"),
                        "ttsProvider": m.get("tts_provider") or "fishspeech",
                        "ttsVoiceId": m.get("tts_voice_id") or "glados",
                        "enabledEngines": json.loads(m.get("enabled_engines") or "{}"),
                        "engineModels": json.loads(m.get("engine_models") or "{}"),
                    }
        except Exception:
            pass
    s = _chat_settings_store.get(user_id) or _default_chat_settings(user_id)
    _chat_settings_store[user_id] = s
    return s


@app.put("/api/chat/settings/{user_id}")
async def update_chat_settings(user_id: str, payload: dict, engine: Optional[AsyncEngine] = Depends(get_engine)):
    enabled = payload.get("enabledEngines") or {}
    if enabled.get("openai") or payload.get("llmEngine") == "openai":
        conn = await get_connections_settings(userId=user_id)
        if not conn.get("OpenAiApiKey"):
            raise HTTPException(status_code=400, detail="OpenAI requires API key")
    if enabled.get("claude") or payload.get("llmEngine") == "claude":
        conn = await get_connections_settings(userId=user_id)
        if not conn.get("ClaudeApiKey"):
            raise HTTPException(status_code=400, detail="Claude requires API key")

    s = _chat_settings_store.get(user_id) or _default_chat_settings(user_id)
    for k in ["llmEngine", "llmModel", "ttsProvider", "ttsVoiceId"]:
        if k in payload:
            s[k] = payload[k]
    if isinstance(enabled, dict):
        s.setdefault("enabledEngines", {}).update(enabled)
    if isinstance(payload.get("engineModels"), dict):
        s.setdefault("engineModels", {}).update(payload["engineModels"])
    _chat_settings_store[user_id] = s
    if engine is not None:
        try:
            async with engine.begin() as conn:
                await conn.execute(t_chat_settings.delete().where(t_chat_settings.c.user_id == user_id))
                await conn.execute(
                    t_chat_settings.insert().values(
                        user_id=user_id,
                        llm_engine=s.get("llmEngine"),
                        llm_model=s.get("llmModel"),
                        tts_provider=s.get("ttsProvider"),
                        tts_voice_id=s.get("ttsVoiceId"),
                        enabled_engines=json.dumps(s.get("enabledEngines", {})),
                        engine_models=json.dumps(s.get("engineModels", {})),
                    )
                )
        except Exception as e:
            print(f"Persist chat settings failed: {e}", file=sys.stderr, flush=True)
    return {"message": "Chat settings updated successfully."}


@app.post("/api/chat", response_model=ChatResponse)
async def chat(req: ChatRequest):
    global _temporal_client
    if _temporal_client is None:
        try:
            _temporal_client = await _connect_with_retry(TEMPORAL_HOST)
        except Exception as e:
            raise HTTPException(status_code=503, detail=f"Temporal unavailable: {e}")

    workflow_id = f"reply-{req.session_id or 'default'}-{abs(hash(req.message)) % 999999}"
    try:
        handle = await _temporal_client.start_workflow(
            "ReplyWorkflow",
            req.model_dump(),
            id=workflow_id,
            task_queue="reply-queue",
            execution_timeout=timedelta(minutes=10),
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to start workflow: {e}")

    try:
        # Try to get a quick result; otherwise return IDs to poll later.
        result = await asyncio.wait_for(handle.result(), timeout=15)
        return ChatResponse(**result, workflow_id=handle.id, run_id=handle.run_id)
    except asyncio.TimeoutError:
        return ChatResponse(reply_text="(processing)", workflow_id=handle.id, run_id=handle.run_id)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Workflow error: {e}")


# Aggregated models for chat UI based on saved settings only (no live discovery)
@app.get("/api/llm/available")
async def llm_available(userId: Optional[str] = None):
    user_id = userId or "default"
    s = _chat_settings_store.get(user_id) or _default_chat_settings(user_id)
    enabled = s.get("enabledEngines", {})
    models = s.get("engineModels", {})
    def pack(engine: str):
        return {
            "available": bool(enabled.get(engine)),
            "models": [m] if (enabled.get(engine) and (m := models.get(engine))) else [],
        }
    return {
        "ollama": pack("ollama"),
        "lmstudio": pack("lmstudio"),
        "openai": pack("openai"),
        "claude": pack("claude"),
        "vllm": pack("vllm"),
    }


