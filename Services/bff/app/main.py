import os
import asyncio
import sys
import json
import ipaddress
import urllib.parse
from typing import Optional, Dict, Any, List

import httpx
import jwt
from fastapi import FastAPI, HTTPException, UploadFile, File, Form, Query, Response, WebSocket, WebSocketDisconnect, Depends
from pydantic import BaseModel
from temporalio.client import Client
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from sqlalchemy.ext.asyncio import AsyncEngine
from sqlalchemy import select, text
from datetime import timedelta, datetime
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse

# Local imports
from .auth import create_access_token, verify_password, get_current_user, require_admin, JWT_SECRET
from .agent_store import AgentStore
from .models import (
    users,
    chat_settings as t_chat_settings,
    connection_settings as t_conn_settings,
    characters as t_characters,
    conversations as t_conversations,
    messages as t_messages,
    workflows as t_workflows,
)

# ------------------------- Config -------------------------

# Prefer IPv4 loopback by default so host apps can reach Temporal in Docker
TEMPORAL_HOST = os.getenv("TEMPORAL_HOST", "127.0.0.1:7233")
ENABLE_TEMPORAL = os.getenv("ENABLE_TEMPORAL", "false").lower() == "true"
OLLAMA_HOST = os.getenv("OLLAMA_HOST", "http://localhost:11434")
LMSTUDIO_HOST = os.getenv("LMSTUDIO_HOST", "http://localhost:1234")
DATABASE_URL = os.getenv("DATABASE_URL")
UPLOADS_DIR = os.getenv("UPLOADS_DIR", "./wwwroot/uploads")
CHAR_UPLOADS_DIR = os.path.join(UPLOADS_DIR, "characters")
WORKERS_ORCH_URL = os.getenv("WORKERS_ORCH_URL", os.getenv("ORCHESTRATOR_URL", "http://localhost:8000"))

# ------------------------- Globals -------------------------

_temporal_client: Optional[Client] = None
_engine: Optional[AsyncEngine] = None
_chat_settings_store: Dict[str, Dict[str, Any]] = {}
_connections_store: Dict[str, Dict[str, Any]] = {}
_folders_store: Dict[str, Dict[str, Any]] = {}

# Agent infra: persistent repo + WS subscribers
_agents_repo = AgentStore()
_agent_stream_subscribers: Dict[asyncio.Queue, Dict[str, Any]] = {}

# ------------------------- Helpers -------------------------

def get_user_upload_dir(user_id: str) -> str:
    d = os.path.join(UPLOADS_DIR, "users", user_id)
    os.makedirs(d, exist_ok=True)
    return d

def get_shared_upload_dir() -> str:
    d = os.path.join(UPLOADS_DIR, "shared")
    os.makedirs(d, exist_ok=True)
    return d

def _default_chat_settings(uid: str) -> Dict[str, Any]:
    return {
        "llmEngine": os.getenv("DEFAULT_LLM_ENGINE", "ollama"),
        "llmModel": os.getenv("LLM_MODEL", "llama3"),
        "engineModels": {},
        "ttsProvider": os.getenv("DEFAULT_TTS_PROVIDER", "fishspeech"),
        "ttsVoiceId": os.getenv("DEFAULT_TTS_VOICE", "glados"),
    }

def _validate_url_for_ssrf(url: str) -> bool:
    """Basic SSRF guard with dev localhost allowance."""
    try:
        parsed = urllib.parse.urlparse(url)
        if parsed.scheme not in ("http", "https"):
            return False
        if not parsed.hostname:
            return False
        hostname = parsed.hostname.lower()

        is_development = (
            os.getenv("REPLIT_DEV_DOMAIN")
            or os.getenv("DATABASE_URL", "").startswith("postgresql://")
            or os.getenv("ENABLE_DEV_LOCALHOST", "false").lower() == "true"
        )
        if hostname in {"localhost", "127.0.0.1", "::1", "0.0.0.0"}:
            return True if is_development else False

        try:
            import socket
            ip = socket.gethostbyname(hostname)
            ip_obj = ipaddress.ip_address(ip)
            private_networks = [
                ipaddress.ip_network("10.0.0.0/8"),
                ipaddress.ip_network("172.16.0.0/12"),
                ipaddress.ip_network("192.168.0.0/16"),
                ipaddress.ip_network("127.0.0.0/8"),
                ipaddress.ip_network("169.254.0.0/16"),
                ipaddress.ip_network("224.0.0.0/4"),
                ipaddress.ip_network("240.0.0.0/4"),
            ]
            for network in private_networks:
                if ip_obj in network:
                    # allow loopback only in dev
                    if is_development and ip_obj in ipaddress.ip_network("127.0.0.0/8"):
                        return True
                    return False
        except Exception:
            return False
        return True
    except Exception:
        return False

# Auth dependency wired with engine
security = HTTPBearer(auto_error=False)

async def current_user_dep(
    creds: Optional[HTTPAuthorizationCredentials] = Depends(security),
    engine: Optional[AsyncEngine] = Depends(lambda: _engine),
):
    return await get_current_user(engine, creds)

async def _connect_with_retry(addr: str) -> Client:
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

async def get_engine() -> Optional[AsyncEngine]:
    return _engine

# ------------------------- App -------------------------

app = FastAPI(title="SwAIvyn BFF", version="0.1.0")
os.makedirs(CHAR_UPLOADS_DIR, exist_ok=True)

try:
    from fastapi.staticfiles import StaticFiles
    frontend_dist = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(__file__))), "frontend", "dist")
    if os.path.exists(frontend_dist):
        app.mount("/", StaticFiles(directory=frontend_dist, html=True), name="frontend")
except Exception:
    pass

app.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "http://localhost:5000",
        "http://127.0.0.1:5000",
        "http://localhost:5173",
        "http://127.0.0.1:5173",
    ],
    allow_origin_regex=r"^https?://.*\.repl\.co(:\d+)?$",
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ------------------------- Startup -------------------------

@app.on_event("startup")
async def _startup_connect_temporal():
    if ENABLE_TEMPORAL:
        asyncio.create_task(_ensure_temporal_connected())

@app.on_event("startup")
async def _startup_db_seed():
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
    global _engine
    if _engine is not None:
        try:
            async with _engine.begin() as conn:
                await conn.execute(
                    users.update().where(users.c.id == "admin").values(role="admin")
                )
        except Exception as e:
            print(f"Admin role fix failed: {e}", file=sys.stderr, flush=True)

# ------------------------- Health -------------------------

@app.get("/healthz")
async def healthz():
    return {"status": "ok"}

@app.get("/readyz")
async def readyz():
    return {"status": "ready", "temporal": ("connected" if _temporal_client is not None else "connecting")}

@app.get("/api/healthz")
async def api_healthz():
    return await healthz()

@app.get("/api/readyz")
async def api_readyz():
    return await readyz()

@app.get("/api")
@app.head("/api")
async def api_root():
    return {"message": "SwAIvyn API"}

# ------------------------- LLM Service Health -------------------------

@app.get("/api/llm/health")
async def llm_health(current=Depends(current_user_dep)):
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    status = {"ollama": None, "lmstudio": None}
    uid = current.get("id") or "default"
    user_conn = _connections_store.get(uid, {})

    ollama_base = user_conn.get("OllamaApiUrl") or OLLAMA_HOST
    lmstudio_base = user_conn.get("LmStudioApiUrl") or LMSTUDIO_HOST

    if not _validate_url_for_ssrf(ollama_base):
        status["ollama"] = {"ok": False, "error": "Invalid or unsafe Ollama URL"}
        ollama_base = None
    if not _validate_url_for_ssrf(lmstudio_base):
        status["lmstudio"] = {"ok": False, "error": "Invalid or unsafe LM Studio URL"}
        lmstudio_base = None

    async with httpx.AsyncClient(timeout=3) as client:
        if ollama_base:
            try:
                r = await client.get(f"{ollama_base}/api/tags")
                status["ollama"] = {"ok": r.status_code == 200}
            except Exception as e:
                status["ollama"] = {"ok": False, "error": str(e)}
        if lmstudio_base:
            try:
                r = await client.get(f"{lmstudio_base}/v1/models")
                status["lmstudio"] = {"ok": r.status_code == 200}
            except Exception as e:
                status["lmstudio"] = {"ok": False, "error": str(e)}
    return status

# ------------------------- Users & Auth -------------------------

async def get_engine_dep() -> Optional[AsyncEngine]:
    return _engine

@app.get("/api/user/default")
async def get_default_user(current=Depends(current_user_dep)):
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    return {"id": current.get("id"), "username": current.get("username"), "role": current.get("role")}

class LoginRequest(BaseModel):
    username: Optional[str] = None
    email: Optional[str] = None
    password: str

@app.post("/api/auth/login")
async def login(body: LoginRequest, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
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
async def change_password(body: PasswordChange, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
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

# Admin purge: delete conversations/messages
@app.delete("/api/admin/conversations")
async def admin_purge_conversations(
    confirm: bool = Query(False, description="Must be true to perform delete"),
    userId: Optional[str] = Query(None, description="Delete only this user's conversations"),
    legacyOnly: bool = Query(False, description="Delete conversations with missing/invalid owners"),
    engine: Optional[AsyncEngine] = Depends(get_engine_dep),
    current=Depends(current_user_dep),
):
    require_admin(current)
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    if not confirm:
        raise HTTPException(status_code=400, detail="Set confirm=true to perform purge")

    where_sql = ""
    params: Dict[str, Any] = {}
    if userId:
        where_sql = "WHERE user_id = :uid"
        params["uid"] = userId
    elif legacyOnly:
        where_sql = (
            "WHERE user_id IS NULL OR user_id = '' "
            "OR user_id = '00000000-0000-0000-0000-000000000001' "
            "OR NOT EXISTS (SELECT 1 FROM users u WHERE u.id = conversations.user_id)"
        )

    async with engine.begin() as conn:
        try:
            sel = await conn.execute(text(f"SELECT COUNT(*) FROM conversations {where_sql}").bindparams(**params))
            conv_count = sel.scalar_one() if hasattr(sel, "scalar_one") else (sel.first()[0] if sel.first() else 0)
        except Exception:
            conv_count = None

        await conn.execute(text(
            f"DELETE FROM messages WHERE conversation_id IN (SELECT id FROM conversations {where_sql})"
        ).bindparams(**params))
        await conn.execute(text(f"DELETE FROM conversations {where_sql}").bindparams(**params))

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
async def admin_list_users(engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
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
async def admin_create_user(body: UserCreate, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
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
    codes: List[str]

@app.get("/api/user/{user_id}")
async def get_user(user_id: str, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    if current.get("id") != user_id and current.get("role") != "admin":
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
            "recovery_codes_generated": r["recovery_codes_hash"] is not None,
        }

@app.put("/api/user/{user_id}")
async def update_user(user_id: str, body: UserUpdate, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
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
async def update_user_pin(user_id: str, body: PinUpdate, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
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
async def generate_recovery_codes(user_id: str, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    if not current or (current.get("id") != user_id and current.get("role") != "admin"):
        raise HTTPException(status_code=403, detail="Forbidden")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    import secrets, string, bcrypt as _bcrypt
    codes = ["-".join(secrets.choice(string.ascii_lowercase) for _ in range(12)) for _ in range(5)]
    codes_hash = _bcrypt.hashpw("\n".join(codes).encode(), _bcrypt.gensalt()).decode()
    async with engine.begin() as conn:
        await conn.execute(users.update().where(users.c.id == user_id).values(recovery_codes_hash=codes_hash))
    return RecoveryCode(codes=codes)

# ------------------------- Chat settings -------------------------

def _merge_chat_settings_from_db_row(row_map: Dict[str, Any]) -> Dict[str, Any]:
    def safe_json_parse(value: Any, default: Dict[str, Any]) -> Dict[str, Any]:
        if value is None:
            return default
        if isinstance(value, dict):
            return value
        if isinstance(value, str):
            try:
                parsed = json.loads(value)
                return parsed if isinstance(parsed, dict) else default
            except (json.JSONDecodeError, TypeError):
                print(f"Failed to parse JSON: {value!r}", file=sys.stderr, flush=True)
                return default
        return default

    return {
        "llmEngine": row_map.get("llm_engine") or "ollama",
        "llmModel": row_map.get("llm_model") or os.getenv("LLM_MODEL", "llama3"),
        "ttsProvider": row_map.get("tts_provider") or "fishspeech",
        "ttsVoiceId": row_map.get("tts_voice_id") or "glados",
        "enabledEngines": safe_json_parse(row_map.get("enabled_engines"), {}),
        "engineModels": safe_json_parse(row_map.get("engine_models"), {}),
    }

@app.get("/api/settings/llm")
async def get_llm_settings(current=Depends(current_user_dep), engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    uid = current.get("id") or "default"

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
    return {"engine": s.get("llmEngine", "ollama"), "model": s.get("llmModel", os.getenv("LLM_MODEL", "llama3"))}

@app.put("/api/settings/llm")
async def put_llm_settings(payload: dict, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    uid = current.get("id")
    if not uid:
        raise HTTPException(status_code=401, detail="Invalid user authentication")

    engine_name = payload.get("engine") or "ollama"
    model = payload.get("model") or ""

    s = _chat_settings_store.get(uid) or _default_chat_settings(uid)
    s["llmEngine"] = engine_name
    s["llmModel"] = model
    s.setdefault("engineModels", {})[engine_name] = model
    _chat_settings_store[uid] = s

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

@app.get("/api/chat/settings/{user_id}")
async def get_chat_settings(user_id: str, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    if current.get("id") != user_id and current.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Access denied")

    if engine is not None:
        try:
            async with engine.connect() as conn:
                res = await conn.execute(select(t_chat_settings).where(t_chat_settings.c.user_id == user_id).limit(1))
                row = res.first()
                if row:
                    return _merge_chat_settings_from_db_row(row._mapping)
        except Exception as e:
            print(f"chat settings DB read failed: {e}", file=sys.stderr, flush=True)

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
async def put_chat_settings(user_id: str, payload: dict, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    if current.get("id") != user_id and current.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Access denied")

    existing = _chat_settings_store.get(user_id) or _default_chat_settings(user_id)
    merged = {**existing, **{k: v for k, v in payload.items() if v is not None}}
    _chat_settings_store[user_id] = merged

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
                )
            )
    return {"success": True}

# ------------------------- Conversations list -------------------------

@app.get("/api/conversation/user/{user_id}")
async def list_user_conversations(user_id: str, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    if not current or (current.get("id") != user_id and current.get("role") != "admin"):
        raise HTTPException(status_code=403, detail="Forbidden")
    if engine is None:
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
        return []

# ------------------------- Folders stub -------------------------

@app.get("/api/folder/user/{user_id}")
async def list_user_folders(user_id: str):
    return []

@app.post("/api/folder")
async def create_folder(body: dict):
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
    return {"data": rec}

@app.put("/api/folder/{folder_id}/name")
async def rename_folder(folder_id: str, body: dict):
    return {"success": True}

@app.delete("/api/folder/{folder_id}")
async def delete_folder(folder_id: str):
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
async def list_llm_models(engine: str, baseUrl: Optional[str] = None, current=Depends(current_user_dep)):
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    uid = current.get("id") or "default"
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
            elif engine_name in {"lmstudio", "vllm"}:
                r = await client.get(f"{base}/v1/models")
                r.raise_for_status()
                data = r.json()
                models = [m.get("id") for m in data.get("data", []) if m.get("id")]
                return {"engine": engine_name, "models": models}
    except Exception as e:
        print(f"model discovery failed for {engine_name}: {e}", file=sys.stderr, flush=True)
    return {"engine": engine_name, "models": []}

@app.get("/api/llm/lmstudio/model")
async def lmstudio_loaded_model(baseUrl: Optional[str] = None, current=Depends(current_user_dep)):
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    uid = current.get("id") or "default"
    base = _resolve_llm_base("lmstudio", uid, baseUrl)
    model_id = ""
    if not base:
        return {"model": model_id}
    try:
        async with httpx.AsyncClient(timeout=5) as client:
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
            r2 = await client.get(f"{base}/v1/models")
            if r2.status_code == 200:
                data2 = r2.json()
                items2 = data2.get("data", [])
                if items2:
                    model_id = items2[0].get("id", "")
    except Exception as e:
        print(f"lmstudio loaded model fetch failed: {e}", file=sys.stderr, flush=True)
    return {"model": model_id}

# ------------------------- TTS -------------------------

@app.get("/api/tts/settings")
async def get_tts_settings(engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    uid = current.get("id") or "default"
    out = {
        "apiKey": os.getenv("FISHSPEECH_API_KEY", ""),
        "voiceId": os.getenv("DEFAULT_TTS_VOICE", ""),
        "fishSpeechApiKey": os.getenv("FISHSPEECH_API_KEY", ""),
        "ttsProvider": os.getenv("DEFAULT_TTS_PROVIDER", "fishspeech"),
        "providers": [{"id": "fishspeech", "name": "Fish Speech", "available": True}],
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
async def set_tts_settings(body: dict, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    uid = body.get("userId") or (current or {}).get("id") or "default"
    provider = (body.get("ttsProvider") or "fishspeech").strip().lower()
    voice_id = (body.get("voiceId") or body.get("voice") or "").strip()
    if engine is None:
        return {"success": True}
    try:
        async with engine.begin() as conn:
            res = await conn.execute(select(t_chat_settings.c.user_id).where(t_chat_settings.c.user_id == uid).limit(1))
            if res.first():
                await conn.execute(
                    t_chat_settings.update().where(t_chat_settings.c.user_id == uid).values(tts_provider=provider or None, tts_voice_id=voice_id or None)
                )
            else:
                await conn.execute(
                    t_chat_settings.insert().values(user_id=uid, llm_engine="ollama", tts_provider=provider or None, tts_voice_id=voice_id or None)
                )
        return {"success": True}
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to save TTS settings: {e}")

@app.get("/api/tts/voices")
async def list_tts_voices(provider: str = "fishspeech", userId: Optional[str] = None):
    base = os.getenv("FISHSPEECH_URL", "http://localhost:8081").rstrip("/")
    if provider.lower() != "fishspeech":
        provider = "fishspeech"
    custom_voices = []
    try:
        workspace_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "../../.."))
        voices_json_path = os.path.join(workspace_root, "speech/TTS/openaudio-s1-mini/voices/voices.json")
        if os.path.exists(voices_json_path):
            with open(voices_json_path, "r") as f:
                voices
