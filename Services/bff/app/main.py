import os
import asyncio
import sys
import json
from typing import Optional, Dict, Any

import httpx
from fastapi import FastAPI, HTTPException, UploadFile, File, Form, Query, Response
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
from .models import users, chat_settings as t_chat_settings, connection_settings as t_conn_settings, characters as t_characters, conversations as t_conversations, messages as t_messages, workflows as t_workflows


# Prefer IPv4 loopback by default so host apps can reach Temporal in Docker
TEMPORAL_HOST = os.getenv("TEMPORAL_HOST", "127.0.0.1:7233")
ENABLE_TEMPORAL = os.getenv("ENABLE_TEMPORAL", "false").lower() == "true"
OLLAMA_HOST = os.getenv("OLLAMA_HOST", "http://localhost:11434")
LMSTUDIO_HOST = os.getenv("LMSTUDIO_HOST", "http://localhost:1234")
DATABASE_URL = os.getenv("DATABASE_URL")
UPLOADS_DIR = os.getenv("UPLOADS_DIR", "./wwwroot/uploads")
CHAR_UPLOADS_DIR = os.path.join(UPLOADS_DIR, "characters")
WORKERS_ORCH_URL = os.getenv("WORKERS_ORCH_URL", os.getenv("ORCHESTRATOR_URL", "http://localhost:8000"))


class ChatRequest(BaseModel):
    message: str
    user_id: Optional[str] = None
    session_id: Optional[str] = None
    # Optional LLM routing hints from the client (persisted settings or overrides)
    engine: Optional[str] = None
    model: Optional[str] = None


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
    # Mount frontend dist for production deployment
    frontend_dist = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(__file__))), "frontend", "dist")
    if os.path.exists(frontend_dist):
        app.mount("/", StaticFiles(directory=frontend_dist, html=True), name="frontend")
except Exception:
    pass
app.add_middleware(
        CORSMiddleware,
        allow_origins=["http://localhost:5000", "http://127.0.0.1:5000", "http://localhost:5173", "http://127.0.0.1:5173"],
        allow_origin_regex=r"^https?://.*\.repl\.co(:\d+)?$",
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )
_temporal_client: Optional[Client] = None
_engine: Optional[AsyncEngine] = None
_chat_settings_store: Dict[str, Dict[str, Any]] = {}
_connections_store: Dict[str, Dict[str, Any]] = {}
_agents_store: Dict[str, Dict[str, Any]] = {}
_agents_store: Dict[str, Dict[str, Any]] = {}
_folders_store: Dict[str, Dict[str, Any]] = {}

def _default_chat_settings(uid: str) -> Dict[str, Any]:
    """Fallback chat settings when nothing is persisted in DB or memory.
    Keeps the shape expected by dashboard_status and chat endpoints.
    """
    return {
        "llmEngine": os.getenv("DEFAULT_LLM_ENGINE", "ollama"),
        "llmModel": os.getenv("LLM_MODEL", "llama3"),
        "engineModels": {},
        "ttsProvider": os.getenv("DEFAULT_TTS_PROVIDER", "fishspeech"),
        "ttsVoiceId": os.getenv("DEFAULT_TTS_VOICE", "glados"),
    }


# Auth dependency wired with engine
security = HTTPBearer(auto_error=False)

async def current_user_dep(
    creds: Optional[HTTPAuthorizationCredentials] = Depends(security),
    engine: Optional[AsyncEngine] = Depends(lambda: _engine),
):
    return await get_current_user(engine, creds)


async def _connect_with_retry(addr: str) -> Client:
    """Keep trying to connect to Temporal until success.
    This prevents startup from failing if the server is still initializing.
    """
    delay, attempt = 1, 0
    while True:
        attempt += 1
        try:
            return await Client.connect(addr)
        except Exception as e:
            print(f"Temporal connect failed (attempt {attempt}) to {addr}: {e}", file=sys.stderr, flush=True)
            await asyncio.sleep(delay)
            delay = min(delay * 2, 5)


async def _ensure_temporal_connected():
    global _temporal_client
    if _temporal_client is None:
        _temporal_client = await _connect_with_retry(TEMPORAL_HOST)

@app.on_event("startup")
async def _startup_connect_temporal():
    # Fire-and-forget connection attempt so startup does not block the server
    if ENABLE_TEMPORAL:
        asyncio.create_task(_ensure_temporal_connected())


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

@app.on_event("startup")
async def _startup_load_connection_settings():
    # Pre-load all user connection settings from the database into the cache.
    global _engine
    if _engine is not None:
        try:
            async with _engine.connect() as conn:
                res = await conn.execute(select(t_conn_settings))
                rows = res.fetchall()
                for row in rows:
                    m = row._mapping
                    uid = m["user_id"]
                    _connections_store[uid] = m
                print(f"Loaded connection settings for {len(rows)} users into cache.", flush=True)
        except Exception as e:
            print(f"Failed to pre-load connection settings: {e}", file=sys.stderr, flush=True)


@app.on_event("startup")
async def _startup_fix_admin_role():
    # Ensure the built-in admin account has role 'admin'
    global _engine
    if _engine is not None:
        try:
            async with _engine.begin() as conn:
                await conn.execute(
                    users.update().where(users.c.id == "admin").values(role="admin")
                )
        except Exception as e:
            print(f"Admin role fix failed: {e}", file=sys.stderr, flush=True)



@app.get("/healthz")
async def healthz():
    return {"status": "ok"}


@app.get("/readyz")
async def readyz():
    # Do not block readiness on Temporal connectivity; return 200 so UI can load/login
    # Report simple status while background task connects
    return {"status": "ready", "temporal": ("connected" if _temporal_client is not None else "connecting")}


# Dev/proxy compatibility: allow frontend to call /api/* for health endpoints
@app.get("/api/healthz")
async def api_healthz():
    return await healthz()


@app.get("/api/readyz")
async def api_readyz():
    return await readyz()


@app.get("/api")
@app.head("/api")
async def api_root():
    """Basic API endpoint to handle health probes"""
    return {"message": "SwAIvyn API"}


@app.get("/api/llm/health")
async def llm_health(userId: Optional[str] = None):
    status = {"ollama": None, "lmstudio": None}
    uid = userId or "default"
    user_conn = _connections_store.get(uid, {})
    ollama_base = user_conn.get("OllamaApiUrl")
    if ollama_base is None:
        ollama_base = OLLAMA_HOST
    lmstudio_base = user_conn.get("LmStudioApiUrl")
    if lmstudio_base is None:
        lmstudio_base = LMSTUDIO_HOST
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


# Admin purge: delete conversations/messages (all, by user, or legacy-only)
@app.delete("/api/admin/conversations")
async def admin_purge_conversations(
    confirm: bool = Query(False, description="Must be true to perform delete"),
    userId: Optional[str] = Query(None, description="Delete only this user's conversations"),
    legacyOnly: bool = Query(False, description="Delete conversations with missing/invalid owners"),
    engine: Optional[AsyncEngine] = Depends(get_engine),
    current=Depends(current_user_dep),
):
    # Require admin
    require_admin(current)
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    if not confirm:
        raise HTTPException(status_code=400, detail="Set confirm=true to perform purge")

    # Build WHERE clause
    where_sql = ""
    params: Dict[str, Any] = {}
    if userId:
        where_sql = "WHERE user_id = :uid"
        params["uid"] = userId
    elif legacyOnly:
        # Missing/empty owner, fallback owner used by early builds, or orphan (no matching user)
        where_sql = (
            "WHERE user_id IS NULL OR user_id = '' "
            "OR user_id = '00000000-0000-0000-0000-000000000001' "
            "OR NOT EXISTS (SELECT 1 FROM users u WHERE u.id = conversations.user_id)"
        )
    # else: all conversations (dangerous)

    # Execute purge in a transaction
    async with engine.begin() as conn:
        # Count conversations to be removed for reporting
        try:
            sel = await conn.execute(text(f"SELECT COUNT(*) FROM conversations {where_sql}").bindparams(**params))
            conv_count = sel.scalar_one() if hasattr(sel, "scalar_one") else (sel.first()[0] if sel.first() else 0)
        except Exception:
            conv_count = None

        # Delete messages first, then conversations
        await conn.execute(text(
            f"DELETE FROM messages WHERE conversation_id IN (SELECT id FROM conversations {where_sql})"
        ).bindparams(**params))
        result = await conn.execute(text(f"DELETE FROM conversations {where_sql}").bindparams(**params))

    return {"success": True, "deletedConversations": conv_count}


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
    pin: Optional[str] = None

class PinUpdate(BaseModel):
    pin: str

class RecoveryCode(BaseModel):
    codes: list[str]

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
        return {
            "id": r["id"], 
            "username": r["username"], 
            "email": (r.get("email") or ""), 
            "language": r["language"], 
            "theme": r["theme"], 
            "default_character": r["default_character"], 
            "role": r["role"],
            "pin_set": r["pin_hash"] is not None,
            "recovery_codes_generated": r["recovery_codes_hash"] is not None
        }


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

@app.post("/api/user/{user_id}/pin")
async def update_user_pin(user_id: str, body: PinUpdate, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    if not current or (current.get("id") != user_id and current.get("role") != "admin"):
        raise HTTPException(status_code=403, detail="Forbidden")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    import bcrypt as _bcrypt
    pin_hash = _bcrypt.hashpw(body.pin.encode(), _bcrypt.gensalt()).decode()
    async with engine.begin() as conn:
        await conn.execute(users.update().where(users.c.id == user_id).values(pin_hash=pin_hash))
    return {"success": True}

@app.post("/api/user/{user_id}/recovery-codes")
async def generate_recovery_codes(user_id: str, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    if not current or (current.get("id") != user_id and current.get("role") != "admin"):
        raise HTTPException(status_code=403, detail="Forbidden")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    import secrets
    import string
    codes = ['-'.join(secrets.choice(string.ascii_lowercase) for _ in range(12)) for _ in range(5)]
    import bcrypt as _bcrypt
    codes_hash = _bcrypt.hashpw("\n".join(codes).encode(), _bcrypt.gensalt()).decode()
    async with engine.begin() as conn:
        await conn.execute(users.update().where(users.c.id == user_id).values(recovery_codes_hash=codes_hash))
    return RecoveryCode(codes=codes)


@app.get("/api/settings/llm")
async def get_llm_settings(userId: Optional[str] = None, engine: Optional[AsyncEngine] = Depends(get_engine)):
    """Return saved LLM engine/model from DB if available; otherwise memory defaults.
    Never performs external network calls; safe fallbacks to avoid 500s.
    """
    try:
        uid = userId or "default"

        if engine is not None:
            try:
                async with engine.connect() as conn:
                    res = await conn.execute(
                        select(t_chat_settings).where(t_chat_settings.c.user_id == uid).limit(1)
                    )
                    row = res.first()
                    if row:
                        m = row._mapping
                        return {
                            "engine": (m.get("llm_engine") or "ollama"),
                            "model": (m.get("llm_model") or os.getenv("LLM_MODEL", "llama3")),
                        }
            except Exception as e:
                print(f"Database read failed in get_llm_settings: {e}", file=sys.stderr, flush=True)

        s = _chat_settings_store.get(uid) or _default_chat_settings(uid)
        return {
            "engine": s.get("llmEngine", "ollama"),
            "model": s.get("llmModel", os.getenv("LLM_MODEL", "llama3")),
        }
    except Exception as e:
        print(f"get_llm_settings fatal: {e}", file=sys.stderr, flush=True)
        return {"engine": "ollama", "model": os.getenv("LLM_MODEL", "llama3")}


@app.put("/api/settings/llm")
async def put_llm_settings(payload: dict, engine: Optional[AsyncEngine] = Depends(get_engine)):
    uid = payload.get("userId") or "default"
    engine_name = payload.get("engine") or "ollama"
    model = payload.get("model") or ""
    
    # Update memory
    s = _chat_settings_store.get(uid) or _default_chat_settings(uid)
    s["llmEngine"] = engine_name
    s["llmModel"] = model
    s.setdefault("engineModels", {})[engine_name] = model
    _chat_settings_store[uid] = s
    
    # Sync to database to maintain consistency
    if engine is not None:
        try:
            async with engine.begin() as conn:
                await conn.execute(t_chat_settings.delete().where(t_chat_settings.c.user_id == uid))
                await conn.execute(
                    t_chat_settings.insert().values(
                        user_id=uid,
                        llm_engine=s.get("llmEngine"),
                        llm_model=s.get("llmModel"),
                        tts_provider=s.get("ttsProvider"),
                        tts_voice_id=s.get("ttsVoiceId"),
                        enabled_engines=json.dumps(s.get("enabledEngines", {})),
                        engine_models=json.dumps(s.get("engineModels", {})),
                    )
                )
        except Exception as e:
            print(f"Database sync failed in put_llm_settings: {e}", file=sys.stderr, flush=True)
    
    return {"success": True}


# ------------------------- Chat Settings (compat) -------------------------

def _merge_chat_settings_from_db_row(row_map: Dict[str, Any]) -> Dict[str, Any]:
    return {
        "llmEngine": row_map.get("llm_engine") or "ollama",
        "llmModel": row_map.get("llm_model") or os.getenv("LLM_MODEL", "llama3"),
        "ttsProvider": row_map.get("tts_provider") or "fishspeech",
        "ttsVoiceId": row_map.get("tts_voice_id") or "glados",
        "enabledEngines": json.loads(row_map.get("enabled_engines") or "{}"),
        "engineModels": json.loads(row_map.get("engine_models") or "{}"),
    }


@app.get("/api/chat/settings/{user_id}")
async def get_chat_settings(user_id: str, engine: Optional[AsyncEngine] = Depends(get_engine)):
    # Prefer database if configured; otherwise serve from in-memory cache with defaults
    if engine is not None:
        try:
            async with engine.connect() as conn:
                res = await conn.execute(select(t_chat_settings).where(t_chat_settings.c.user_id == user_id).limit(1))
                row = res.first()
                if row:
                    return _merge_chat_settings_from_db_row(row._mapping)
        except Exception as e:
            print(f"chat settings DB read failed: {e}", file=sys.stderr, flush=True)

    # Fallback to memory/defaults
    s = _chat_settings_store.get(user_id) or _default_chat_settings(user_id)
    return {
        "llmEngine": s.get("llmEngine", "ollama"),
        "llmModel": s.get("llmModel", os.getenv("LLM_MODEL", "llama3")),
        "ttsProvider": s.get("ttsProvider", "fishspeech"),
        "ttsVoiceId": s.get("ttsVoiceId", "glados"),
        "enabledEngines": s.get("enabledEngines", {}),
        "engineModels": s.get("engineModels", {}),
    }


@app.put("/api/chat/settings/{user_id}")
async def put_chat_settings(user_id: str, payload: dict, engine: Optional[AsyncEngine] = Depends(get_engine)):
    # Merge payload into in-memory snapshot first
    existing = _chat_settings_store.get(user_id) or _default_chat_settings(user_id)
    merged = {
        **existing,
        **{k: v for k, v in payload.items() if v is not None},
    }
    _chat_settings_store[user_id] = merged

    # Persist to database for durability/consistency if available
    if engine is not None:
        try:
            async with engine.begin() as conn:
                await conn.execute(t_chat_settings.delete().where(t_chat_settings.c.user_id == user_id))
                await conn.execute(
                    t_chat_settings.insert().values(
                        user_id=user_id,
                        llm_engine=merged.get("llmEngine"),
                        llm_model=merged.get("llmModel"),
                        tts_provider=merged.get("ttsProvider"),
                        tts_voice_id=merged.get("ttsVoiceId"),
                        enabled_engines=json.dumps(merged.get("enabledEngines", {})),
                        engine_models=json.dumps(merged.get("engineModels", {})),
                    )
                )
        except Exception as e:
            print(f"chat settings DB write failed: {e}", file=sys.stderr, flush=True)

    return {"message": "Chat settings updated successfully."}


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
async def get_connections_settings(userId: Optional[str] = None, current=Depends(current_user_dep)):
    # Resolve uid: allow admin to query other users; otherwise force current user
    uid = (current or {}).get("id") or "default"
    if (current or {}).get("role") == "admin" and userId:
        uid = userId
    
    s = _connections_store.get(uid, {})

    def get_setting(key: str, env_var: str, default: Any = ""):
        val = s.get(key)
        if val is not None:
            return val
        return os.getenv(env_var, default)

    return {
        "OpenAiApiKey": get_setting("OpenAiApiKey", "OPENAI_API_KEY"),
        "ClaudeApiKey": get_setting("ClaudeApiKey", "CLAUDE_API_KEY"),
        "ClaudeApiUrl": get_setting("ClaudeApiUrl", "CLAUDE_API_URL", "https://api.anthropic.com/v1"),
        "OllamaApiUrl": get_setting("OllamaApiUrl", "OLLAMA_HOST", OLLAMA_HOST),
        "LmStudioApiUrl": get_setting("LmStudioApiUrl", "LMSTUDIO_HOST", LMSTUDIO_HOST),
        "VllmApiUrl": get_setting("VllmApiUrl", "VLLM_API_URL"),
        "EnableStreaming": bool(get_setting("EnableStreaming", "ENABLE_STREAMING", True)),
        "TtsGpu": get_setting("TtsGpu", "TTS_GPU_ID"),
        "SttGpu": get_setting("SttGpu", "STT_GPU_ID"),
    }


@app.post("/api/settings/connections")
async def update_connections_settings(body: dict, current=Depends(current_user_dep)):
    uid = (current or {}).get("id") or "default"
    body_uid = str(body.get("UserId") or body.get("userId") or "")
    if (current or {}).get("role") == "admin" and body_uid:
        uid = body_uid
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
                    # TtsGpu and SttGpu columns removed due to schema mismatch
                )
            )
    return {"success": True}


@app.put("/api/settings/connections")
async def put_connections_settings(body: dict, current=Depends(current_user_dep)):
    uid = (current or {}).get("id") or "default"
    body_uid = str(body.get("UserId") or body.get("userId") or "")
    if (current or {}).get("role") == "admin" and body_uid:
        uid = body_uid
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
                    # TtsGpu and SttGpu columns removed due to schema mismatch
                )
            )
    return {"success": True}


# ------------------------- Conversations (list for user) -------------------------

@app.get("/api/conversation/user/{user_id}")
async def list_user_conversations(user_id: str, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    # Require same-user or admin
    if not current or (current.get("id") != user_id and current.get("role") != "admin"):
        raise HTTPException(status_code=403, detail="Forbidden")
    if engine is None:
        # No DB configured; return empty list to avoid frontend loops
        return []
    try:
        async with engine.connect() as conn:
            res = await conn.execute(
                text(
                    """
                    SELECT id, user_id, title, folder_id, created_at, last_updated
                    FROM conversations
                    WHERE user_id = :uid
                    ORDER BY last_updated DESC
                    """
                ),
                {"uid": user_id},
            )
            rows = res.fetchall()
            return [
                {
                    "id": r[0],
                    "userId": r[1],
                    "title": r[2],
                    "folderId": r[3],
                    "createdAt": r[4],
                    "lastUpdated": r[5],
                }
                for r in rows
            ]
    except Exception as e:
        print(f"list conversations failed: {e}", file=sys.stderr, flush=True)
        # Graceful fallback
        return []


# ------------------------- Folders (stub list for user) -------------------------

@app.get("/api/folder/user/{user_id}")
async def list_user_folders(user_id: str):
    # No folders table in current schema; return an empty list for now
    return []


@app.post("/api/folder")
async def create_folder(body: dict):
    # Simple in-memory stub to satisfy UI; persists for process lifetime
    global _connections_store
    try:
        import uuid as __uuid
        fid = __uuid.uuid4().hex
    except Exception:
        fid = str(int(asyncio.get_event_loop().time() * 1000))
    rec = {
        "id": fid,
        "userId": body.get("userId"),
        "name": body.get("name") or "New Folder",
        "parentId": body.get("parentId"),
    }
    # Return as { data: rec } to match ChatSidebar expectations
    return {"data": rec}


@app.put("/api/folder/{folder_id}/name")
async def rename_folder(folder_id: str, body: dict):
    # No-op success stub for now
    return {"success": True}


@app.delete("/api/folder/{folder_id}")
async def delete_folder(folder_id: str):
    # No-op success stub for now
    return {"success": True}


# ------------------------- LLM Models Discovery -------------------------

def _resolve_llm_base(engine_name: str, uid: str, base_url_override: Optional[str] = None) -> Optional[str]:
    if base_url_override:
        return base_url_override
    user_conn = _connections_store.get(uid, {})
    if engine_name == "ollama":
        return user_conn.get("OllamaApiUrl") or OLLAMA_HOST
    if engine_name == "lmstudio":
        return user_conn.get("LmStudioApiUrl") or LMSTUDIO_HOST
    if engine_name == "vllm":
        return user_conn.get("VllmApiUrl") or os.getenv("VLLM_API_URL")
    return None


@app.get("/api/llm/models")
async def list_llm_models(engine: str, userId: Optional[str] = None, baseUrl: Optional[str] = None):
    uid = userId or "default"
    engine_name = (engine or "").lower()
    base = _resolve_llm_base(engine_name, uid, baseUrl)
    if not base:
        return []
    try:
        async with httpx.AsyncClient(timeout=5) as client:
            if engine_name == "ollama":
                r = await client.get(f"{base}/api/tags")
                r.raise_for_status()
                data = r.json()
                models = [m.get("name") for m in data.get("models", []) if m.get("name")]
                return {"engine": "ollama", "models": models}
            elif engine_name == "lmstudio":
                r = await client.get(f"{base}/v1/models")
                r.raise_for_status()
                data = r.json()
                models = [m.get("id") for m in data.get("data", []) if m.get("id")]
                return {"engine": "lmstudio", "models": models}
            elif engine_name == "vllm":
                r = await client.get(f"{base}/v1/models")
                r.raise_for_status()
                data = r.json()
                models = [m.get("id") for m in data.get("data", []) if m.get("id")]
                return {"engine": "vllm", "models": models}
    except Exception as e:
        print(f"model discovery failed for {engine_name}: {e}", file=sys.stderr, flush=True)
    return {"engine": engine_name, "models": []}


@app.get("/api/llm/lmstudio/model")
async def lmstudio_loaded_model(userId: Optional[str] = None, baseUrl: Optional[str] = None):
    uid = userId or "default"
    base = _resolve_llm_base("lmstudio", uid, baseUrl)
    model_id = ""
    if not base:
        return {"model": model_id}
    try:
        async with httpx.AsyncClient(timeout=5) as client:
            # Try loaded models endpoint first
            try:
                r = await client.get(f"{base}/api/v0/models")
                if r.status_code == 200:
                    data = r.json()
                    items = data if isinstance(data, list) else data.get("data", [])
                    loaded = next((m for m in items if m.get("loaded") or m.get("isLoaded")), None)
                    if loaded and loaded.get("id"):
                        return {"model": loaded.get("id")}
            except Exception:
                pass

            # Fallback to first available model from OpenAI-compatible endpoint
            r2 = await client.get(f"{base}/v1/models")
            if r2.status_code == 200:
                data2 = r2.json()
                items2 = data2.get("data", [])
                if items2:
                    model_id = items2[0].get("id", "")
    except Exception as e:
        print(f"lmstudio loaded model fetch failed: {e}", file=sys.stderr, flush=True)
    return {"model": model_id}


@app.get("/api/tts/settings")
async def get_tts_settings(userId: Optional[str] = None, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    # Per-user TTS settings, persisted in chat_settings when DB is available; otherwise env defaults
    uid = userId or (current or {}).get("id") or "default"
    out = {
        "apiKey": os.getenv("FISHSPEECH_API_KEY", ""),
        "voiceId": os.getenv("DEFAULT_TTS_VOICE", ""),
        "fishSpeechApiKey": os.getenv("FISHSPEECH_API_KEY", ""),
        "ttsProvider": os.getenv("DEFAULT_TTS_PROVIDER", "fishspeech"),
        "providers": [
            {"id": "fishspeech", "name": "Fish Speech", "available": True},
        ],
    }
    if engine is not None:
        try:
            async with engine.connect() as conn:
                res = await conn.execute(select(t_chat_settings).where(t_chat_settings.c.user_id == uid).limit(1))
                row = res.first()
                if row:
                    m = row._mapping
                    if m.get("tts_provider"):
                        out["ttsProvider"] = m.get("tts_provider")
                    if m.get("tts_voice_id"):
                        out["voiceId"] = m.get("tts_voice_id")
        except Exception:
            pass
    return out


@app.post("/api/tts/settings")
async def set_tts_settings(body: dict, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    # Persist tts_provider and tts_voice_id in chat_settings for the user
    uid = body.get("userId") or (current or {}).get("id") or "default"
    provider = (body.get("ttsProvider") or "fishspeech").strip().lower()
    voice_id = (body.get("voiceId") or body.get("voice") or "").strip()
    if engine is None:
        return {"success": True}
    try:
        async with engine.begin() as conn:
            # Upsert row
            res = await conn.execute(select(t_chat_settings.c.user_id).where(t_chat_settings.c.user_id == uid).limit(1))
            if res.first():
                await conn.execute(
                    t_chat_settings.update().where(t_chat_settings.c.user_id == uid).values(tts_provider=provider or None, tts_voice_id=voice_id or None)
                )
            else:
                await conn.execute(
                    t_chat_settings.insert().values(user_id=uid, llm_engine='ollama', tts_provider=provider or None, tts_voice_id=voice_id or None)
                )
        return {"success": True}
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to save TTS settings: {e}")


@app.get("/api/tts/voices")
async def list_tts_voices(provider: str = "fishspeech", userId: Optional[str] = None):
    base = os.getenv("FISHSPEECH_URL", "http://localhost:8081").rstrip("/")
    if provider.lower() != "fishspeech":
        provider = "fishspeech"
    
    # Load custom voices from voices.json
    custom_voices = []
    try:
        import json
        # Use absolute path from workspace root
        workspace_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
        voices_json_path = os.path.join(workspace_root, "speech/TTS/openaudio-s1-mini/voices/voices.json")
        if os.path.exists(voices_json_path):
            with open(voices_json_path, 'r') as f:
                voices_data = json.load(f)
                custom_voices = [voice["name"] for voice in voices_data.get("voices", []) if voice.get("enabled", True)]
    except Exception as e:
        print(f"Warning: Could not load custom voices from voices.json: {e}")
    
    # Try to get default Fish Speech voices from service
    fishspeech_voices = []
    try:
        async with httpx.AsyncClient(timeout=10) as client:
            r = await client.get(f"{base}/voices")
            if r.status_code == 200:
                data = r.json()
                fishspeech_voices = data if isinstance(data, list) else data.get("voices", [])
    except Exception:
        # If Fish Speech service is not available, use default voices
        fishspeech_voices = ["glados", "alloy", "echo", "fable", "onyx", "nova", "shimmer"]
    
    # Combine custom voices and Fish Speech voices, removing duplicates
    all_voices = list(dict.fromkeys(custom_voices + fishspeech_voices))
    
    return {"provider": "fishspeech", "voices": all_voices}


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
    """Synthesize speech using Fish Speech by default. Falls back to silence WAV on error."""
    text = (body.get("text") or "").strip()
    voice = (body.get("voiceId") or body.get("voice") or "").strip()
    base = os.getenv("FISHSPEECH_URL", "http://localhost:8081").rstrip("/")
    if not text:
        raise HTTPException(status_code=400, detail="text required")
    # Prefer clone when a valid voice exists; fall back to default /tts
    endpoint = f"{base}/tts"
    form = {"text": text}
    if voice:
        try:
            async with httpx.AsyncClient(timeout=10) as client:
                vr = await client.get(f"{base}/voices")
                if vr.status_code == 200:
                    data = vr.json()
                    voices = data if isinstance(data, list) else data.get("voices", [])
                    if voice in voices:
                        endpoint = f"{base}/tts/clone"
                        form["voice_name"] = voice
        except Exception:
            pass
    try:
        async with httpx.AsyncClient(timeout=30) as client:
            r = await client.post(endpoint, data=form)
            if r.status_code == 200:
                ctype = r.headers.get("content-type", "audio/wav")
                return Response(content=r.content, media_type=ctype)
    except Exception:
        pass
    # Fallback short silence
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


@app.post("/api/stt/transcribe")
async def stt_transcribe(audio: UploadFile = File(...), language: Optional[str] = Form(None), task: Optional[str] = Form(None)):
    """Proxy transcription to Whisper ASR webservice (default http://localhost:9000)."""
    stt_base = os.getenv("STT_URL", "http://localhost:9000").rstrip("/")
    target = f"{stt_base}/asr"
    data = {"language": (language or "en"), "task": (task or "transcribe")}
    content = await audio.read()
    files = {"audio_file": (audio.filename or "speech.webm", content, audio.content_type or "audio/webm")}
    try:
        async with httpx.AsyncClient(timeout=120) as client:
            r = await client.post(target, data=data, files=files)
            r.raise_for_status()
            return r.json()
    except Exception as e:
        raise HTTPException(status_code=503, detail=f"STT unavailable: {e}")


@app.post("/api/character")
async def create_character(payload: dict, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    """Create a new character from form data"""
    # CRITICAL SECURITY FIX: Require authentication for character creation
    if current is None:
        raise HTTPException(status_code=401, detail="Authentication required")
    
    name = payload.get("name", "New Character").strip()
    system_prompt = payload.get("systemPrompt", "You are a helpful AI assistant.").strip()
    image_path = payload.get("imagePath", "").strip()
    shared = payload.get("shared", False)
    
    # Input validation
    if not name:
        raise HTTPException(status_code=400, detail="Character name is required")
    if len(name) > 100:
        raise HTTPException(status_code=400, detail="Character name too long (max 100 characters)")
    if len(system_prompt) > 10000:
        raise HTTPException(status_code=400, detail="System prompt too long (max 10,000 characters)")
    
    # Validate image path (if provided) to prevent path traversal and XSS
    if image_path:
        # Only allow relative paths starting with /uploads/ or /static/
        if not (image_path.startswith("/uploads/") or image_path.startswith("/static/")):
            raise HTTPException(status_code=400, detail="Invalid image path")
        # Prevent path traversal
        if ".." in image_path or "~" in image_path:
            raise HTTPException(status_code=400, detail="Invalid image path")
        if len(image_path) > 500:
            raise HTTPException(status_code=400, detail="Image path too long")
    
    # Basic XSS prevention - reject content with script tags
    import re
    if re.search(r'<script[^>]*>.*?</script>', system_prompt, re.IGNORECASE | re.DOTALL):
        raise HTTPException(status_code=400, detail="Invalid content in system prompt")
    if re.search(r'<script[^>]*>.*?</script>', name, re.IGNORECASE | re.DOTALL):
        raise HTTPException(status_code=400, detail="Invalid content in character name")
    
    import uuid
    cid = str(uuid.uuid4())
    
    # Extract user info from authenticated user (guaranteed to be non-None at this point)
    current_user_id = current.get("id")
    current_role = current.get("role")
    
    # Additional security: ensure we have a valid user ID
    if not current_user_id:
        raise HTTPException(status_code=401, detail="Invalid user authentication")
    
    # Only admins can create shared/global characters (user_id = None)
    if shared and current_role == "admin":
        user_id = None  # Global character
    else:
        user_id = current_user_id  # User-specific character
    
    if engine is not None:
        async with engine.begin() as conn:
            await conn.execute(t_characters.insert().values(
                id=cid,
                user_id=user_id,
                name=name,
                system_prompt=system_prompt,
                image_path=image_path,
            ))
    
    return {"id": cid, "name": name, "systemPrompt": system_prompt, "imagePath": image_path, "shared": user_id is None}


@app.post("/api/character/import-yaml")
async def import_character_yaml(payload: dict, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    # CRITICAL SECURITY FIX: Require authentication for character creation
    if current is None:
        raise HTTPException(status_code=401, detail="Authentication required")
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
    
    # Input validation for YAML import
    if len(name) > 100:
        raise HTTPException(status_code=400, detail="Character name too long (max 100 characters)")
    if len(system_prompt) > 10000:
        raise HTTPException(status_code=400, detail="System prompt too long (max 10,000 characters)")
    
    # Basic XSS prevention
    import re
    if re.search(r'<script[^>]*>.*?</script>', system_prompt, re.IGNORECASE | re.DOTALL):
        raise HTTPException(status_code=400, detail="Invalid content in system prompt")
    if re.search(r'<script[^>]*>.*?</script>', name, re.IGNORECASE | re.DOTALL):
        raise HTTPException(status_code=400, detail="Invalid content in character name")
    
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
    # Check if this should be a shared character (from payload)
    shared = payload.get("shared", False)
    
    # Extract user info from authenticated user (guaranteed to be non-None at this point)
    current_user_id = current.get("id")
    current_role = current.get("role")
    
    # Additional security: ensure we have a valid user ID
    if not current_user_id:
        raise HTTPException(status_code=401, detail="Invalid user authentication")
    
    # Only admins can create shared/global characters (user_id = None)
    if shared and current_role == "admin":
        user_id = None  # Global character
    else:
        user_id = current_user_id  # User-specific character
    
    import uuid
    cid = str(uuid.uuid4())
    if engine is not None:
        async with engine.begin() as conn:
            await conn.execute(t_characters.insert().values(
                id=cid,
                user_id=user_id,
                name=name,
                system_prompt=system_prompt,
                image_path=image_path,
            ))
    return {"id": cid, "name": name, "imagePath": image_path, "shared": user_id is None}


# ------------------------- Conversations (CRUD + messages) -------------------------

import uuid as _uuid_mod


@app.post("/api/conversation")
async def create_conversation(body: dict, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    conv_id = str(_uuid_mod.uuid4())
    title = body.get("title") or "New Chat"
    folder_id = body.get("folderId")
    now = datetime.utcnow().isoformat() + "Z"
    user_id = (current or {}).get("id") or body.get("userId") or "default"

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
async def update_conversation_title(conv_id: str, body: dict, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    if engine is not None:
        async with engine.begin() as conn:
            # Owner or admin
            owner = None
            try:
                res = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
                row = res.first()
                owner = row[0] if row else None
            except Exception:
                pass
            if not current or (owner and current.get("id") != owner and current.get("role") != "admin"):
                raise HTTPException(status_code=403, detail="Forbidden")
            await conn.execute(
                t_conversations.update().where(t_conversations.c.id == conv_id).values(
                    title=body.get("title") or "New Chat",
                    last_updated=datetime.utcnow().isoformat() + "Z",
                )
            )
    return {"success": True}


@app.put("/api/conversation/{conv_id}/folder")
async def update_conversation_folder(conv_id: str, body: dict, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    if engine is not None:
        async with engine.begin() as conn:
            owner = None
            try:
                res = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
                row = res.first()
                owner = row[0] if row else None
            except Exception:
                pass
            if not current or (owner and current.get("id") != owner and current.get("role") != "admin"):
                raise HTTPException(status_code=403, detail="Forbidden")
            await conn.execute(
                t_conversations.update().where(t_conversations.c.id == conv_id).values(
                    folder_id=body.get("folderId"),
                    last_updated=datetime.utcnow().isoformat() + "Z",
                )
            )
    return {"success": True}


@app.put("/api/conversation/{conv_id}/open")
async def touch_conversation_open(conv_id: str, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    uid = (current or {}).get("id")
    if not uid:
        raise HTTPException(status_code=401, detail="Unauthorized")
    now = datetime.utcnow().isoformat() + "Z"
    async with engine.begin() as conn:
        res = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
        row = res.first()
        owner = row[0] if row else None
        if not owner or (uid != owner and (current or {}).get("role") != "admin"):
            raise HTTPException(status_code=403, detail="Forbidden")
        await conn.execute(
            t_conversations.update().where(t_conversations.c.id == conv_id).values(last_updated=now)
        )
    return {"success": True}


@app.get("/api/conversation/{conv_id}")
async def get_conversation(conv_id: str, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    async with engine.connect() as conn:
        res = await conn.execute(select(t_conversations).where(t_conversations.c.id == conv_id).limit(1))
        row = res.first()
        if not row:
            raise HTTPException(status_code=404, detail="Not found")
        m = row._mapping
        if not current or (current.get("id") != m["user_id"] and current.get("role") != "admin"):
            raise HTTPException(status_code=403, detail="Forbidden")
        return {
            "id": m["id"],
            "userId": m["user_id"],
            "title": m["title"],
            "folderId": m["folder_id"],
            "createdAt": m["created_at"],
            "lastUpdated": m["last_updated"],
        }


@app.delete("/api/conversation/{conv_id}")
async def delete_conversation(conv_id: str, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    uid = (current or {}).get("id")
    role = (current or {}).get("role")
    if not uid:
        raise HTTPException(status_code=401, detail="Unauthorized")
    async with engine.begin() as conn:
        res = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
        row = res.first()
        if not row:
            return Response(status_code=204)
        owner = row[0]
        if role != "admin" and uid != owner:
            raise HTTPException(status_code=403, detail="Forbidden")
        await conn.execute(t_messages.delete().where(t_messages.c.conversation_id == conv_id))
        await conn.execute(t_conversations.delete().where(t_conversations.c.id == conv_id))
    return Response(status_code=204)


@app.get("/api/conversation/{conv_id}/messages")
async def get_conversation_messages(conv_id: str, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    if engine is not None:
        try:
            async with engine.connect() as conn:
                res = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
                row = res.first()
                owner = row[0] if row else None
                if not current or (owner and current.get("id") != owner and current.get("role") != "admin"):
                    raise HTTPException(status_code=403, detail="Forbidden")
                res = await conn.execute(select(t_messages).where(t_messages.c.conversation_id == conv_id))
                rows = res.fetchall()
                out = []
                for r in rows:
                    m = r._mapping
                    out.append({
                        "id": m["id"],
                        "conversationId": m["conversation_id"],
                        "role": m["role"],
                        "content": m["content"],
                        "timestamp": m["timestamp"],
                    })
                return out
        except HTTPException:
            raise
        except Exception as e:
            print(f"messages fetch failed: {e}", file=sys.stderr, flush=True)
    return []


@app.post("/api/conversation/message")
async def append_conversation_message(body: dict, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    now = datetime.utcnow().isoformat() + "Z"
    mid = str(_uuid_mod.uuid4())
    conv_id = body.get("conversationId")
    rec = {
        "id": mid,
        "conversationId": conv_id,
        "role": body.get("role", "user"),
        "content": body.get("content", ""),
        "timestamp": now,
    }
    if engine is not None and conv_id:
        try:
            async with engine.begin() as conn:
                res = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
                row = res.first()
                owner = row[0] if row else None
                if not current or (owner and current.get("id") != owner and current.get("role") != "admin"):
                    raise HTTPException(status_code=403, detail="Forbidden")
                await conn.execute(
                    t_messages.insert().values(
                        id=mid,
                        conversation_id=conv_id,
                        role=rec["role"],
                        content=rec["content"],
                        timestamp=now,
                    )
                )
        except Exception as e:
            print(f"append message failed: {e}", file=sys.stderr, flush=True)
    return rec


@app.post("/api/conversation/character-context")
async def set_character_context(body: dict, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    # Store/update a system message representing the character/system prompt
    conv_id = body.get("conversationId")
    user_id = body.get("userId") or (current or {}).get("id")
    system_prompt = body.get("systemPrompt") or ""
    if not conv_id or not user_id:
        return {"success": False}
    if engine is not None:
        try:
            async with engine.begin() as conn:
                # Verify ownership
                res = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
                row = res.first()
                owner = row[0] if row else None
                if not owner or (owner != user_id and (current or {}).get("role") != "admin"):
                    raise HTTPException(status_code=403, detail="Forbidden")
                # Insert a system message (simple approach; deduplication can be added later)
                await conn.execute(
                    t_messages.insert().values(
                        id=str(_uuid_mod.uuid4()),
                        conversation_id=conv_id,
                        role="system",
                        content=system_prompt,
                        timestamp=datetime.utcnow().isoformat() + "Z",
                    )
                )
        except HTTPException:
            raise
        except Exception as e:
            print(f"set character context failed: {e}", file=sys.stderr, flush=True)
    return {"success": True}


# ------------------------- Chat Routes (compat) -------------------------

@app.post("/api/conversation/chat")
async def conversation_chat(body: dict, current=Depends(current_user_dep)):
    # Resolve user and settings
    uid = (current or {}).get("id") or body.get("userId") or "default"
    msg = body.get("message") or ""
    session_id = body.get("conversationId") or body.get("session_id")

    # If a conversation is specified, ensure the user owns it (unless admin)
    if session_id and _engine is not None:
        try:
            async with _engine.connect() as conn:
                res = await conn.execute(
                    select(t_conversations.c.user_id)
                    .where(t_conversations.c.id == session_id)
                    .limit(1)
                )
                row = res.first()
                owner = row[0] if row else None
                if owner and uid != owner and (current or {}).get("role") != "admin":
                    raise HTTPException(status_code=403, detail="Forbidden")
        except HTTPException:
            raise
        except Exception as e:
            print(f"conversation ownership check failed: {e}", file=sys.stderr, flush=True)
            # fail closed if ownership cannot be verified
            raise HTTPException(status_code=500, detail="Internal server error")

    # Load user settings (database first, then memory fallback)
    user_settings = {}
    if _engine is not None:
        try:
            async with _engine.connect() as conn:
                res = await conn.execute(select(t_chat_settings).where(t_chat_settings.c.user_id == uid).limit(1))
                row = res.first()
                if row:
                    m = row._mapping
                    user_settings = {
                        "llmEngine": m.get("llm_engine") or "ollama",
                        "llmModel": m.get("llm_model") or "llama3",
                        "enabledEngines": json.loads(m.get("enabled_engines") or "{}"),
                        "engineModels": json.loads(m.get("engine_models") or "{}"),
                    }
        except Exception:
            pass
    
    if not user_settings:
        s = _chat_settings_store.get(uid) or _default_chat_settings(uid)
        user_settings = {
            "llmEngine": s.get("llmEngine", "ollama"),
            "llmModel": s.get("llmModel", "llama3"),
            "enabledEngines": s.get("enabledEngines", {}),
            "engineModels": s.get("engineModels", {}),
        }
    # Engine/model selection and validation
    engine = (body.get("engine") or "").strip() or None
    model = (body.get("model") or "").strip() or None
    if not engine:
        engine = user_settings.get("llmEngine")
    if not model:
        engine_models = user_settings.get("engineModels", {})
        model = engine_models.get(engine) or user_settings.get("llmModel")

    enabled_engines = user_settings.get("enabledEngines", {})
    if engine and not enabled_engines.get(engine):
        raise HTTPException(status_code=400, detail=f"Engine '{engine}' is not enabled for user. Please enable it in settings first.")
    if engine and not model:
        raise HTTPException(status_code=400, detail=f"No model configured for engine '{engine}'. Please configure a model in settings.")

    # Build connection payload with env fallbacks
    conn = _connections_store.get(uid, {})

    def get_conn_setting(key: str, env_var: str, default: Any = ""):
        val = conn.get(key)
        if val is not None:
            return val
        return os.getenv(env_var, default)

    def _normalize_host(url: Optional[str]) -> Optional[str]:
        if not url:
            return url
        try:
            return url.replace("host.docker.internal", "localhost")
        except Exception:
            return url

    conn_payload = {
        "ollama_base": _normalize_host(get_conn_setting("OllamaApiUrl", "OLLAMA_HOST", OLLAMA_HOST)),
        "lmstudio_base": _normalize_host(get_conn_setting("LmStudioApiUrl", "LMSTUDIO_HOST", LMSTUDIO_HOST)),
        "openai_base": _normalize_host(get_conn_setting("OpenAiApiUrl", "OPENAI_API_BASE", "https://api.openai.com/v1")),
        "openai_api_key": get_conn_setting("OpenAiApiKey", "OPENAI_API_KEY"),
        "claude_api_url": get_conn_setting("ClaudeApiUrl", "CLAUDE_API_URL", "https://api.anthropic.com/v1"),
        "claude_api_key": get_conn_setting("ClaudeApiKey", "CLAUDE_API_KEY"),
        "vllm_base": _normalize_host(get_conn_setting("VllmApiUrl", "VLLM_API_URL")),
        "vllm_api_key": get_conn_setting("VllmApiKey", "VLLM_API_KEY"),
    }

    # Execute via the default chat workflow adapter (no behavior change)
    return await _execute_default_chat_workflow(uid, msg, session_id, engine, model, conn_payload)


async def _execute_default_chat_workflow(uid: str, msg: str, session_id: Optional[str], engine: str, model: str, conn_payload: Dict[str, Any]):
    # Start Temporal workflow per engine — mirrors previous inline logic
    global _temporal_client
    if _temporal_client is None:
        try:
            _temporal_client = await _connect_with_retry(TEMPORAL_HOST)
        except Exception as e:
            raise HTTPException(status_code=503, detail=f"Temporal unavailable: {e}")

    engine_key = (engine or '').strip().lower()
    workflow_name_map = {
        'ollama': 'ReplyWorkflow_Ollama',
        'lmstudio': 'ReplyWorkflow_LMStudio',
        'openai': 'ReplyWorkflow_OpenAI',
        'claude': 'ReplyWorkflow_Claude',
        'vllm': 'ReplyWorkflow_VLLM',
    }
    wf_name = workflow_name_map.get(engine_key)
    if not wf_name:
        raise HTTPException(status_code=400, detail=f"Invalid or missing engine: {engine}")

    req = ChatRequest(message=msg, session_id=session_id, engine=engine, model=model, user_id=uid)
    payload = req.model_dump()
    payload["conn"] = conn_payload

    try:
        handle = await _temporal_client.start_workflow(
            wf_name,
            payload,
            id=f"reply-{session_id or 'default'}-{abs(hash(msg)) % 999999}",
            task_queue="reply-queue",
            execution_timeout=timedelta(minutes=10),
        )
        try:
            result = await asyncio.wait_for(handle.result(), timeout=15)
        except asyncio.TimeoutError:
            result = {"reply_text": "(processing)", "tts_url": None}
        return {"response": result.get("reply_text", ""), "tts_url": result.get("tts_url")}
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to start workflow: {e}")


@app.get("/api/test")
async def test_endpoint():
    return {"message": "test successful"}


@app.get("/api/dashboard/status")
async def dashboard_status(userId: Optional[str] = None, engine: Optional[AsyncEngine] = Depends(get_engine), current=Depends(current_user_dep)):
    """Dashboard summary strictly from saved state (DB/memory). No external API calls.
    - LLM: engine/model from chat_settings for the user
    - TTS: provider/voice from chat_settings (tts columns)
    - Metrics: counts from DB (characters, conversations)
    """
    uid = (current or {}).get("id") or userId or "default"

    # Load persisted user chat settings
    user_settings: Dict[str, Any] = {}
    if engine is not None:
        try:
            async with engine.connect() as conn:
                res = await conn.execute(select(t_chat_settings).where(t_chat_settings.c.user_id == uid).limit(1))
                row = res.first()
                if row:
                    m = row._mapping
                    user_settings = {
                        "llmEngine": m.get("llm_engine") or "ollama",
                        "llmModel": m.get("llm_model") or os.getenv("LLM_MODEL", "llama3"),
                        "engineModels": json.loads(m.get("engine_models") or "{}"),
                        "ttsProvider": m.get("tts_provider") or "fishspeech",
                        "ttsVoiceId": m.get("tts_voice_id") or "glados",
                    }
        except Exception as e:
            print(f"Dashboard status database read failed: {e}", file=sys.stderr, flush=True)

    if not user_settings:
        s = _chat_settings_store.get(uid) or _default_chat_settings(uid)
        user_settings = {
            "llmEngine": s.get("llmEngine", "ollama"),
            "llmModel": s.get("llmModel", os.getenv("LLM_MODEL", "llama3")),
            "engineModels": s.get("engineModels", {}),
            "ttsProvider": s.get("ttsProvider", "fishspeech"),
            "ttsVoiceId": s.get("ttsVoiceId", "glados"),
        }

    # Prefer engine-specific model mapping if present
    llm_engine = user_settings["llmEngine"]
    engine_models = user_settings.get("engineModels", {})
    llm_model = engine_models.get(llm_engine) or user_settings.get("llmModel") or "Not selected"

    # Basic metrics from DB only
    character_count = 0
    memory_count = 0
    conversation_count = 0
    if engine is not None:
        try:
            async with engine.connect() as conn:
                res = await conn.execute(
                    text("SELECT COUNT(*) FROM characters WHERE user_id = :uid OR user_id IS NULL"),
                    {"uid": uid},
                )
                row = res.first()
                character_count = row[0] if row else 0

                res = await conn.execute(select(t_conversations).where(t_conversations.c.user_id == uid))
                conversation_count = len(res.fetchall())
        except Exception:
            pass

    # Aggregate agents (reported by external workers via this API) — DB not required
    running_list = []
    completed = failed = pending = 0
    try:
        viewer_is_admin = (current or {}).get("role") == "admin"
        viewer_id = (current or {}).get("id")
        for ag in _agents_store.values():
            if viewer_is_admin or ag.get("userId") == viewer_id:
                st = (ag.get("status") or "").lower()
                if st == "running":
                    running_list.append(ag)
                elif st == "completed":
                    completed += 1
                elif st == "failed":
                    failed += 1
                else:
                    pending += 1
    except Exception:
        pass

    return {
        "status": "ok",
        "llm": {"engine": llm_engine, "model": llm_model},
        "tts": {"provider": user_settings.get("ttsProvider"), "voice": user_settings.get("ttsVoiceId")},
        "metrics": {
            "characterCount": character_count,
            "memoryCount": memory_count,
            "conversationCount": conversation_count,
        },
        "agents": {"running": running_list, "completed": completed, "failed": failed, "pending": pending},
        "notifications": [],
    }


# ------------------------- Agents reporting API (for external workers) -------------------------
from datetime import datetime as _dt


@app.get("/api/agents")
async def agents_list(userId: Optional[str] = None, current=Depends(current_user_dep)):
    viewer_is_admin = (current or {}).get("role") == "admin"
    uid = userId or (current or {}).get("id")
    out = []
    for ag in _agents_store.values():
        if viewer_is_admin or ag.get("userId") == uid:
            out.append(ag)
    return out


@app.post("/api/agents/{agent_id}/start")
async def agents_start(agent_id: str, body: dict = {}, current=Depends(current_user_dep)):
    uid = (current or {}).get("id") or body.get("userId") or "default"
    now = _dt.utcnow().isoformat() + "Z"
    ag = _agents_store.get(agent_id) or {}
    ag.update({
        "id": agent_id,
        "name": body.get("name") or ag.get("name") or agent_id,
        "status": "running",
        "userId": uid,
        "startedAt": ag.get("startedAt") or now,
        "finishedAt": None,
        "meta": body.get("meta") or ag.get("meta") or {},
    })
    _agents_store[agent_id] = ag
    return {"success": True, "agent": ag}


@app.post("/api/agents/{agent_id}/stop")
async def agents_stop(agent_id: str, body: dict = {}, current=Depends(current_user_dep)):
    now = _dt.utcnow().isoformat() + "Z"
    final_status = (body.get("status") or "completed").lower()
    if final_status not in ("completed", "failed", "pending"):
        final_status = "completed"
    ag = _agents_store.get(agent_id) or {"id": agent_id}
    ag.update({
        "status": final_status,
        "finishedAt": now,
        "meta": body.get("meta") or ag.get("meta") or {},
    })
    _agents_store[agent_id] = ag
    return {"success": True, "agent": ag}


@app.delete("/api/agents/{agent_id}")
async def agents_delete(agent_id: str, current=Depends(current_user_dep)):
    _agents_store.pop(agent_id, None)
    return {"success": True}


# ------------------------- Agents catalog proxy (workers orchestrator) -------------------------
@app.get("/api/agents/catalog")
async def agents_catalog():
    try:
        async with httpx.AsyncClient(timeout=10) as client:
            r = await client.get(f"{WORKERS_ORCH_URL.rstrip('/')}/api/agents")
            r.raise_for_status()
            return r.json()
    except httpx.HTTPStatusError as e:
        raise HTTPException(status_code=e.response.status_code, detail=f"Workers returned {e.response.status_code}")
    except Exception as e:
        raise HTTPException(status_code=503, detail=f"Workers orchestrator unavailable: {e}")


# ------------------------- Workflows (read-only, for UI) -------------------------
@app.get("/api/workflows")
async def workflows_list():
    # Return list of workflow ids, names, versions
    out = []
    if _engine is not None:
        try:
            async with _engine.connect() as conn:
                res = await conn.execute(select(t_workflows.c.id, t_workflows.c.name, t_workflows.c.version))
                for row in res.fetchall():
                    out.append({"id": row[0], "name": row[1], "version": str(row[2])})
        except Exception:
            pass
    if not out:
        out = [{"id": "default-chat", "name": "Default Chat", "version": "1"}]
    return out


@app.get("/api/workflows/default")
async def workflows_default():
    return await workflows_get("default-chat")


@app.get("/api/workflows/{workflow_id}")
async def workflows_get(workflow_id: str):
    import json as _json
    if _engine is not None:
        try:
            async with _engine.connect() as conn:
                res = await conn.execute(select(t_workflows).where(t_workflows.c.id == workflow_id).limit(1))
                row = res.first()
                if row:
                    m = row._mapping
                    try:
                        definition = _json.loads(m["definition"]) if m.get("definition") else {}
                    except Exception:
                        definition = {"raw": m.get("definition")}
                    return {
                        "id": m["id"],
                        "name": m["name"],
                        "version": str(m["version"]),
                        "definition": definition,
                    }
        except Exception:
            pass
    # Fallback minimal default-chat
    return {
        "id": "default-chat",
        "name": "Default Chat",
        "version": "1",
        "definition": {
            "id": "default-chat",
            "name": "Default Chat",
            "version": 1,
            "steps": [
                {"id": "select_llm", "type": "select_llm"},
                {"id": "build_conn", "type": "build_connections"},
                {"id": "temporal", "type": "start_temporal", "queue": "reply-queue"},
                {"id": "format_reply", "type": "format"},
            ],
        },
    }
