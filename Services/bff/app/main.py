import os
import asyncio
import sys
import json
import ipaddress
import urllib.parse
import uuid
from typing import Optional, Dict, Any, List

import httpx
from fastapi import (
    FastAPI,
    HTTPException,
    UploadFile,
    File,
    Form,
    Query,
    Response,
    Request,
    Depends,
)
from pydantic import BaseModel
from temporalio.client import Client
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from sqlalchemy.ext.asyncio import AsyncEngine
from sqlalchemy import select, text
from datetime import timedelta, datetime
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse

# Local imports
from .auth import create_access_token, verify_password, get_current_user, require_admin, JWT_SECRET  # JWT_SECRET may be unused if WS disabled
from .agent_store import AgentStore  # Repo is currently unused but kept for future swap
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
WORKERS_ORCH_BASE = WORKERS_ORCH_URL.rstrip("/")

# ------------------------- Globals -------------------------

_temporal_client: Optional[Client] = None
_engine: Optional[AsyncEngine] = None
_chat_settings_store: Dict[str, Dict[str, Any]] = {}
_connections_store: Dict[str, Dict[str, Any]] = {}
_folders_store: Dict[str, Dict[str, Any]] = {}

# Agent infra: in-memory store (matches current REST impl)
_agents_store: Dict[str, Dict[str, Any]] = {}
# Repo placeholder if you later swap persistence
_agents_repo = AgentStore()

# ------------------------- Models -------------------------

class ChatRequest(BaseModel):
    message: str
    user_id: Optional[str] = None
    session_id: Optional[str] = None
    engine: Optional[str] = None
    model: Optional[str] = None

class ChatResponse(BaseModel):
    reply_text: str
    tts_url: Optional[str] = None
    workflow_id: Optional[str] = None
    run_id: Optional[str] = None

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

async def get_engine_dep() -> Optional[AsyncEngine]:
    return _engine

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

class CharacterCreate(BaseModel):
    name: str
    system_prompt: str
    image_path: Optional[str] = None
    is_shared: bool = False  # Admin-only: create shared character

class CharacterUpdate(BaseModel):
    name: Optional[str] = None
    system_prompt: Optional[str] = None
    systemPrompt: Optional[str] = None  # Support camelCase from frontend
    image_path: Optional[str] = None
    imagePath: Optional[str] = None  # Support camelCase from frontend

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

# ------------------------- Character Management -------------------------

@app.get("/api/characters")
async def list_characters(engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    """List characters accessible to the current user (their own + shared characters)"""
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    
    uid = current.get("id")
    is_admin = current.get("role") == "admin"
    
    async with engine.connect() as conn:
        if is_admin:
            # Admins see all characters
            res = await conn.execute(select(t_characters).order_by(t_characters.c.name))
        else:
            # Regular users see shared characters (user_id=null) + their own characters
            res = await conn.execute(
                select(t_characters)
                .where((t_characters.c.user_id == uid) | (t_characters.c.user_id.is_(None)))
                .order_by(t_characters.c.name)
            )
        
        characters = []
        for row in res.fetchall():
            char_dict = dict(row._mapping)
            # Rename fields to match frontend expectations
            char_dict["systemPrompt"] = char_dict.pop("system_prompt", "")
            char_dict["imagePath"] = char_dict.pop("image_path", "")
            char_dict["is_shared"] = char_dict["user_id"] is None
            characters.append(char_dict)
        
        return characters

@app.post("/api/characters")
async def create_character(body: CharacterCreate, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    """Create a new character"""
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    
    uid = current.get("id")
    is_admin = current.get("role") == "admin"
    
    # Only admins can create shared characters
    if body.is_shared and not is_admin:
        raise HTTPException(status_code=403, detail="Only admins can create shared characters")
    
    # Generate unique ID
    char_id = str(uuid.uuid4())
    
    # Set user_id based on whether it's shared
    user_id = None if body.is_shared else uid
    
    async with engine.begin() as conn:
        await conn.execute(
            t_characters.insert().values(
                id=char_id,
                user_id=user_id,
                name=body.name,
                system_prompt=body.system_prompt,
                image_path=body.image_path,
            )
        )
    
    return {"id": char_id, "success": True}

@app.get("/api/characters/{character_id}")
async def get_character(character_id: str, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    """Get a specific character"""
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    
    uid = current.get("id")
    is_admin = current.get("role") == "admin"
    
    async with engine.connect() as conn:
        res = await conn.execute(
            select(t_characters).where(t_characters.c.id == character_id).limit(1)
        )
        row = res.first()
        
        if not row:
            raise HTTPException(status_code=404, detail="Character not found")
        
        char_dict = dict(row._mapping)
        
        # Check permissions
        if not is_admin and char_dict["user_id"] not in (None, uid):
            raise HTTPException(status_code=403, detail="Access denied")
        
        # Rename fields to match frontend expectations
        char_dict["systemPrompt"] = char_dict.pop("system_prompt", "")
        char_dict["imagePath"] = char_dict.pop("image_path", "")
        char_dict["is_shared"] = char_dict["user_id"] is None
        return char_dict

@app.put("/api/characters/{character_id}")
async def update_character(character_id: str, body: CharacterUpdate, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    """Update a character"""
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    
    uid = current.get("id")
    is_admin = current.get("role") == "admin"
    
    async with engine.begin() as conn:
        # Check if character exists and user has permission
        res = await conn.execute(
            select(t_characters).where(t_characters.c.id == character_id).limit(1)
        )
        row = res.first()
        
        if not row:
            raise HTTPException(status_code=404, detail="Character not found")
        
        char_dict = dict(row._mapping)
        
        # Check permissions: admin can edit any, users can edit their own or shared
        if not is_admin and char_dict["user_id"] not in (None, uid):
            raise HTTPException(status_code=403, detail="Access denied")
        
        # Only admins can modify shared characters
        if char_dict["user_id"] is None and not is_admin:
            raise HTTPException(status_code=403, detail="Only admins can modify shared characters")
        
        # Build update values
        update_values = {}
        if body.name is not None:
            update_values["name"] = body.name
        if body.system_prompt is not None or body.systemPrompt is not None:
            update_values["system_prompt"] = body.system_prompt or body.systemPrompt
        if body.image_path is not None or body.imagePath is not None:
            update_values["image_path"] = body.image_path or body.imagePath
        
        if update_values:
            await conn.execute(
                t_characters.update()
                .where(t_characters.c.id == character_id)
                .values(**update_values)
            )
        
        return {"success": True}

@app.delete("/api/characters/{character_id}")
async def delete_character(character_id: str, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    """Delete a character"""
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    
    uid = current.get("id")
    is_admin = current.get("role") == "admin"
    
    async with engine.begin() as conn:
        # Check if character exists and user has permission
        res = await conn.execute(
            select(t_characters).where(t_characters.c.id == character_id).limit(1)
        )
        row = res.first()
        
        if not row:
            raise HTTPException(status_code=404, detail="Character not found")
        
        char_dict = dict(row._mapping)
        
        # Check permissions: admin can delete any, users can delete their own
        if not is_admin and char_dict["user_id"] != uid:
            raise HTTPException(status_code=403, detail="Access denied")
        
        # Prevent deletion of default characters
        if character_id in ("default", "glados", "sam", "sherlock"):
            raise HTTPException(status_code=400, detail="Cannot delete built-in characters")
        
        await conn.execute(
            t_characters.delete().where(t_characters.c.id == character_id)
        )
        
        return {"success": True}

# ------------------------- Character Management (Legacy Endpoints) -------------------------
# These endpoints match the frontend expectations

@app.get("/api/character/user/{user_id}")
async def list_user_characters(user_id: str, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    """List characters for a specific user (legacy endpoint)"""
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    
    current_uid = current.get("id")
    is_admin = current.get("role") == "admin"
    
    # Users can only see their own characters unless they're admin
    if user_id != current_uid and not is_admin:
        raise HTTPException(status_code=403, detail="Access denied")
    
    async with engine.connect() as conn:
        # Get user's own characters + shared characters
        res = await conn.execute(
            select(t_characters)
            .where((t_characters.c.user_id == user_id) | (t_characters.c.user_id.is_(None)))
            .order_by(t_characters.c.name)
        )
        
        characters = []
        for row in res.fetchall():
            char_dict = dict(row._mapping)
            # Rename fields to match frontend expectations
            char_dict["systemPrompt"] = char_dict.pop("system_prompt", "")
            char_dict["imagePath"] = char_dict.pop("image_path", "")
            char_dict["is_shared"] = char_dict["user_id"] is None
            characters.append(char_dict)
        
        return characters

@app.post("/api/character")
async def create_character_legacy(body: CharacterCreate, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    """Create a new character (legacy endpoint)"""
    return await create_character(body, engine, current)

@app.put("/api/character/{character_id}")
async def update_character_legacy(character_id: str, body: CharacterUpdate, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    """Update a character (legacy endpoint)"""
    return await update_character(character_id, body, engine, current)

@app.delete("/api/character/{character_id}")
async def delete_character_legacy(character_id: str, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    """Delete a character (legacy endpoint)"""
    return await delete_character(character_id, engine, current)

@app.post("/api/character/import-yaml")
async def import_character_yaml(body: dict, engine: Optional[AsyncEngine] = Depends(get_engine_dep), current=Depends(current_user_dep)):
    """Import a character from YAML format"""
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    
    yaml_content = body.get("yaml", "")
    if not yaml_content:
        raise HTTPException(status_code=400, detail="YAML content required")
    
    try:
        import yaml
        data = yaml.safe_load(yaml_content)
        if not isinstance(data, dict):
            raise HTTPException(status_code=400, detail="Invalid YAML format")
        
        # Extract character data from YAML
        name = data.get("name", "Imported Character")
        
        # Build system prompt from YAML structure
        parts = []
        for field in ["description", "personality", "scenario", "instructions"]:
            value = data.get(field, "")
            if isinstance(value, str) and value.strip():
                parts.append(value.strip())
        
        system_prompt = "\n\n".join(parts) if parts else "You are a helpful AI assistant."
        
        # Create character
        char_create = CharacterCreate(
            name=name,
            system_prompt=system_prompt,
            image_path=None,  # Could extract from YAML if available
            is_shared=False   # Default to private
        )
        
        return await create_character(char_create, engine, current)
        
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"Failed to parse YAML: {str(e)}")
