# -*- coding: utf-8 -*-

import io
import json
import ipaddress
import logging
import math
import os
import asyncio
import re
import secrets
import socket
import string
import struct
import time
import urllib.parse
import uuid
from contextlib import asynccontextmanager
from collections import defaultdict
from typing import Optional, Dict, Any, List, Mapping, Set

import bcrypt
import yaml

import httpx
from fastapi import (
    FastAPI,
    HTTPException,
    UploadFile,
    File,
    Query,
    Request,
    Depends,
)
from fastapi.middleware.cors import CORSMiddleware
from fastapi.security import HTTPAuthorizationCredentials
from fastapi.staticfiles import StaticFiles
from fastapi.responses import JSONResponse, Response
from pydantic import BaseModel
from sqlalchemy import func, not_, exists
from temporalio.client import Client
from sqlalchemy.ext.asyncio import AsyncEngine
from sqlalchemy import select, text
from datetime import timedelta, datetime, timezone
from starlette.middleware.base import BaseHTTPMiddleware

# Local imports
from .auth import create_access_token, verify_password, get_current_user, require_admin, JWT_SECRET, JWT_EXPIRES_MINUTES
from .agent_store import AgentStore  # Repo is currently unused but kept for future swap
from .models import (
    users,
    chat_settings as t_chat_settings,
    connection_settings as t_conn_settings,
    characters as t_characters,
    conversations as t_conversations,
    messages as t_messages,
    workflows as t_workflows,
    agents,
    agent_registry,
    agent_tasks,
    agent_results,
)

# ------------------------- Logging & Config -------------------------

logging.basicConfig(
    level=os.getenv("BFF_LOG_LEVEL", "INFO").upper(),
    format="%(asctime)s %(levelname)s [%(name)s] %(message)s",
)
logger = logging.getLogger("swai.bff")

# Required environment variables for startup validation (name -> description)
REQUIRED_ENV_VARS: Mapping[str, str] = {
    "DATABASE_URL": "PostgreSQL connection string",
    "JWT_SECRET": "JWT signing secret (generate: python -c \"import secrets; print(secrets.token_urlsafe(64))\")",
}

TEMPORAL_HOST = os.getenv("TEMPORAL_HOST", "127.0.0.1:7233")
ENABLE_TEMPORAL = os.getenv("ENABLE_TEMPORAL", "false").lower() == "true"
OLLAMA_HOST = os.getenv("OLLAMA_HOST", "http://localhost:11434")
LMSTUDIO_HOST = os.getenv("LMSTUDIO_HOST", "http://localhost:1234")
DATABASE_URL = os.getenv("DATABASE_URL")
UPLOADS_DIR = os.getenv("UPLOADS_DIR", "./wwwroot/uploads")
CHAR_UPLOADS_DIR = os.path.join(UPLOADS_DIR, "characters")
WORKERS_ORCH_URL = os.getenv("WORKERS_ORCH_URL", os.getenv("ORCHESTRATOR_URL", "http://localhost:8000"))
WORKERS_ORCH_BASE = WORKERS_ORCH_URL.rstrip("/")
DEFAULT_DEV_ORIGINS = [
    "http://localhost:5000",
    "http://127.0.0.1:5000",
    "http://localhost:5173",
    "http://127.0.0.1:5173",
]

# ------------------------- Globals -------------------------


def _validate_required_env_vars(required: Mapping[str, str]) -> None:
    missing = [name for name, _ in required.items() if not os.getenv(name)]
    if missing:
        for name in missing:
            logger.critical(
                "Missing required environment variable %s (%s)",
                name,
                required[name],
            )
        raise RuntimeError(
            "Missing required environment variables: " + ", ".join(missing)
        )


def _load_allowed_origins() -> List[str]:
    raw = os.getenv("ALLOWED_ORIGINS")
    if raw:
        origins = [origin.strip() for origin in raw.split(",") if origin.strip()]
        if origins:
            return origins
    return DEFAULT_DEV_ORIGINS

_temporal_client: Optional[Client] = None
_engine: Optional[AsyncEngine] = None

# In-memory stores with timestamp for TTL eviction (user_id -> (data, last_access_ts))
_chat_settings_store: Dict[str, Dict[str, Any]] = {}
_chat_settings_ts: Dict[str, float] = {}
_connections_store: Dict[str, Dict[str, Any]] = {}
_connections_ts: Dict[str, float] = {}
_folders_store: Dict[str, Dict[str, Any]] = {}
_CACHE_TTL_SECONDS = int(os.getenv("CACHE_TTL_SECONDS", "3600"))  # 1 hour default

# Agent infra: in-memory store (matches current REST impl)
_agents_store: Dict[str, Dict[str, Any]] = {}
# Repo placeholder if you later swap persistence
_agents_repo = AgentStore()

# ---------------------------------------------------------------------------
# Rate limiting (login endpoint) — simple in-memory token bucket per IP
# ---------------------------------------------------------------------------
_login_attempts: Dict[str, List[float]] = defaultdict(list)
_LOGIN_RATE_WINDOW = int(os.getenv("LOGIN_RATE_WINDOW_SECONDS", "60"))
_LOGIN_RATE_MAX = int(os.getenv("LOGIN_RATE_MAX_ATTEMPTS", "10"))


def _check_login_rate_limit(ip: str) -> None:
    """Raise 429 if IP has exceeded allowed login attempts within the window."""
    now = time.time()
    window_start = now - _LOGIN_RATE_WINDOW
    attempts = _login_attempts[ip]
    # Prune old attempts
    _login_attempts[ip] = [t for t in attempts if t > window_start]
    if len(_login_attempts[ip]) >= _LOGIN_RATE_MAX:
        raise HTTPException(
            status_code=429,
            detail=f"Too many login attempts. Try again in {_LOGIN_RATE_WINDOW} seconds.",
        )
    _login_attempts[ip].append(now)


# ---------------------------------------------------------------------------
# Password complexity validation
# ---------------------------------------------------------------------------
_MIN_PASSWORD_LENGTH = int(os.getenv("MIN_PASSWORD_LENGTH", "8"))


def _validate_password_complexity(password: str) -> None:
    """Raise 400 if password doesn't meet complexity requirements."""
    if len(password) < _MIN_PASSWORD_LENGTH:
        raise HTTPException(
            status_code=400,
            detail=f"Password must be at least {_MIN_PASSWORD_LENGTH} characters.",
        )
    if not re.search(r"[A-Z]", password):
        raise HTTPException(status_code=400, detail="Password must contain at least one uppercase letter.")
    if not re.search(r"[a-z]", password):
        raise HTTPException(status_code=400, detail="Password must contain at least one lowercase letter.")
    if not re.search(r"\d", password):
        raise HTTPException(status_code=400, detail="Password must contain at least one digit.")


# ---------------------------------------------------------------------------
# Input length helpers
# ---------------------------------------------------------------------------
_MAX_TITLE_LEN = 300
_MAX_NAME_LEN = 200
_MAX_SYSTEM_PROMPT_LEN = 32_000
_MAX_MESSAGE_LEN = 64_000
_MAX_YAML_BYTES = 512_000  # 512 KB


def _assert_length(value: str, max_len: int, field: str) -> str:
    if len(value) > max_len:
        raise HTTPException(status_code=400, detail=f"{field} exceeds maximum length of {max_len} characters.")
    return value


# ---------------------------------------------------------------------------
# API key field-level encryption (Fernet symmetric encryption)
# Set FIELD_ENCRYPTION_KEY to a Fernet key to enable.
# Generate: python -c "from cryptography.fernet import Fernet; print(Fernet.generate_key().decode())"
# ---------------------------------------------------------------------------
_FIELD_ENCRYPTION_KEY = os.getenv("FIELD_ENCRYPTION_KEY", "")
_fernet = None
if _FIELD_ENCRYPTION_KEY:
    try:
        from cryptography.fernet import Fernet as _Fernet
        _fernet = _Fernet(_FIELD_ENCRYPTION_KEY.encode())
        logger.info("Field-level encryption enabled for API keys.")
    except Exception as _fe:
        logger.warning("Failed to initialize field encryption (%s). API keys will be stored unencrypted.", _fe)
else:
    logger.warning(
        "FIELD_ENCRYPTION_KEY not set. API keys are stored unencrypted. "
        "Set this variable to enable at-rest encryption."
    )

_API_KEY_FIELDS = {"OpenAiApiKey", "ClaudeApiKey"}


def _encrypt_api_key(value: Optional[str]) -> Optional[str]:
    if not value or not _fernet:
        return value
    return _fernet.encrypt(value.encode()).decode()


def _decrypt_api_key(value: Optional[str]) -> Optional[str]:
    if not value or not _fernet:
        return value
    try:
        return _fernet.decrypt(value.encode()).decode()
    except Exception:
        # Value may be stored unencrypted (migration)
        return value


# ---------------------------------------------------------------------------
# Cache TTL eviction helper
# ---------------------------------------------------------------------------
def _evict_stale_cache_entries() -> None:
    """Remove in-memory cache entries older than _CACHE_TTL_SECONDS."""
    cutoff = time.time() - _CACHE_TTL_SECONDS
    stale_chat = [uid for uid, ts in _chat_settings_ts.items() if ts < cutoff]
    for uid in stale_chat:
        _chat_settings_store.pop(uid, None)
        _chat_settings_ts.pop(uid, None)
    stale_conn = [uid for uid, ts in _connections_ts.items() if ts < cutoff]
    for uid in stale_conn:
        _connections_store.pop(uid, None)
        _connections_ts.pop(uid, None)

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

def _dt_str(value: Any) -> str:
    """Serialize a datetime or string timestamp to ISO 8601 string."""
    if value is None:
        return ""
    if hasattr(value, "isoformat"):
        return value.isoformat()
    return str(value)


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

async def _validate_url_for_ssrf(url: str) -> bool:
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
            or os.getenv("ENVIRONMENT", "production").lower() in ("development", "dev", "local")
            or os.getenv("ENABLE_DEV_LOCALHOST", "false").lower() == "true"
        )
        if hostname in {"localhost", "127.0.0.1", "::1", "0.0.0.0"}:
            return True if is_development else False

        try:
            loop = asyncio.get_running_loop()
            try:
                ip = await loop.run_in_executor(None, socket.gethostbyname, hostname)
            except Exception:
                return False
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

async def get_engine_dep() -> Optional[AsyncEngine]:
    return _engine


_TEMPORAL_MAX_RETRIES = int(os.getenv("TEMPORAL_CONNECT_MAX_RETRIES", "20"))


async def _connect_with_retry(addr: str, max_attempts: int = _TEMPORAL_MAX_RETRIES) -> Client:
    delay, attempt = 1, 0
    while True:
        attempt += 1
        try:
            return await Client.connect(addr)
        except Exception as e:
            logger.warning(
                "Temporal connect failed (attempt %s/%s) to %s: %s",
                attempt,
                max_attempts,
                addr,
                e,
            )
            if attempt >= max_attempts:
                logger.error("Temporal connect gave up after %s attempts to %s", max_attempts, addr)
                raise
            await asyncio.sleep(delay)
            delay = min(delay * 2, 5)

async def _ensure_temporal_connected():
    global _temporal_client
    if _temporal_client is None:
        _temporal_client = await _connect_with_retry(TEMPORAL_HOST)

# ------------------------- Lifespan -------------------------

@asynccontextmanager
async def lifespan(application: FastAPI):
    """Replaces deprecated @app.on_event startup handlers."""
    global _engine, _temporal_client

    _validate_required_env_vars(REQUIRED_ENV_VARS)
    os.makedirs(CHAR_UPLOADS_DIR, exist_ok=True)

    if ENABLE_TEMPORAL:
        asyncio.create_task(_ensure_temporal_connected())

    if DATABASE_URL:
        try:
            from .db import create_engine
            from .seed import ensure_seed
            _engine = create_engine()
            if _engine is not None:
                await ensure_seed(_engine)
                logger.info("Database ready and seed complete")
        except Exception as exc:
            logger.exception("Database initialization failed: %s", exc)

    if _engine is not None:
        try:
            async with _engine.connect() as conn:
                res = await conn.execute(select(t_conn_settings))
                rows = res.fetchall()
                for row in rows:
                    m = row._mapping
                    uid = m["user_id"]
                    _connections_store[uid] = dict(m)
                    _connections_ts[uid] = time.time()
                logger.info("Loaded connection settings for %s users into cache", len(rows))
        except Exception as exc:
            logger.exception("Failed to pre-load connection settings: %s", exc)

    yield
    # Shutdown: dispose DB engine if created
    if _engine is not None:
        await _engine.dispose()
        logger.info("Database engine disposed")


# ------------------------- App -------------------------

app = FastAPI(title="SwAIvyn BFF", version="0.1.0", lifespan=lifespan)

# ------------------------- Security Middleware -------------------------

_MAX_BODY_SIZE = int(os.getenv("MAX_BODY_SIZE_BYTES", str(10 * 1024 * 1024)))  # 10 MB default


class RequestBodySizeLimitMiddleware(BaseHTTPMiddleware):
    """Reject requests whose Content-Length exceeds MAX_BODY_SIZE_BYTES."""

    async def dispatch(self, request: Request, call_next):
        content_length = request.headers.get("content-length")
        if content_length is not None:
            try:
                content_length_value = int(content_length)
            except ValueError:
                # Malformed Content-Length header; treat as bad request
                return JSONResponse(
                    status_code=400,
                    content={"detail": "Invalid Content-Length header."},
                )
            if content_length_value > _MAX_BODY_SIZE:
                return JSONResponse(
                    status_code=413,
                    content={"detail": f"Request body too large (max {_MAX_BODY_SIZE} bytes)."},
                )
        return await call_next(request)


class SecurityHeadersMiddleware(BaseHTTPMiddleware):
    """Add security response headers to every API response."""

    async def dispatch(self, request: Request, call_next):
        response = await call_next(request)
        response.headers["X-Content-Type-Options"] = "nosniff"
        response.headers["X-Frame-Options"] = "DENY"
        response.headers["X-XSS-Protection"] = "1; mode=block"
        response.headers["Referrer-Policy"] = "strict-origin-when-cross-origin"
        response.headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()"
        is_https = request.url.scheme == "https" or request.headers.get("x-forwarded-proto") == "https"
        if is_https:
            response.headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains"
        return response


class AuthenticationMiddleware(BaseHTTPMiddleware):
    """Global authentication middleware to secure all API endpoints."""

    async def dispatch(self, request: Request, call_next):
        path = request.url.path

        unauthenticated_paths = [
            "/healthz", "/readyz", "/api/healthz", "/api/readyz", "/api",
            "/api/auth/login",
            "/api/auth/logout",
        ]

        # Allow static files, frontend routes, and OPTIONS requests (CORS preflight)
        if not path.startswith("/api") or path in unauthenticated_paths or request.method == "OPTIONS":
            return await call_next(request)

        # Accept token from Authorization header OR HTTPOnly cookie
        token: Optional[str] = None
        auth_header = request.headers.get("authorization")
        if auth_header and auth_header.startswith("Bearer "):
            token = auth_header.split(" ", 1)[1]
        if not token:
            token = request.cookies.get("auth_token")

        if not token:
            return JSONResponse(
                status_code=401,
                content={"detail": "Authentication required"},
            )

        try:
            creds = HTTPAuthorizationCredentials(scheme="Bearer", credentials=token)
            user = await get_current_user(_engine, creds)
            if not user:
                return JSONResponse(status_code=401, content={"detail": "Invalid or expired token"})
            request.state.user = user
        except Exception:
            return JSONResponse(status_code=401, content={"detail": "Invalid authentication token"})

        return await call_next(request)

try:
    frontend_dist = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(__file__))), "frontend", "dist")
    if os.path.exists(frontend_dist):
        app.mount("/", StaticFiles(directory=frontend_dist, html=True), name="frontend")
except Exception:
    pass

# Middleware order (outermost → innermost):
# RequestBodySizeLimit → SecurityHeaders → CORS → Authentication
app.add_middleware(AuthenticationMiddleware)

# Restrict Replit regex to the specific project subdomain when configured
_replit_dev_domain = os.getenv("REPLIT_DEV_DOMAIN", "")
_replit_origin_regex: Optional[str] = None
if _replit_dev_domain:
    # Escape the specific domain to prevent wildcard matching
    _escaped = re.escape(_replit_dev_domain)
    _replit_origin_regex = rf"^https?://{_escaped}$"

app.add_middleware(
    CORSMiddleware,
    allow_origins=_load_allowed_origins(),
    allow_origin_regex=_replit_origin_regex,
    allow_credentials=True,
    allow_methods=["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"],
    allow_headers=["Content-Type", "Authorization", "X-Agent-ID", "X-Agent-API-Key"],
)

app.add_middleware(SecurityHeadersMiddleware)
app.add_middleware(RequestBodySizeLimitMiddleware)

# ------------------------- Health -------------------------

@app.get("/healthz")
async def healthz():
    return {"status": "ok"}


@app.get("/readyz")
async def readyz():
    db_status = "not_configured"
    if _engine is not None:
        try:
            async with _engine.connect() as conn:
                await conn.execute(text("SELECT 1"))
            db_status = "ok"
        except Exception as exc:
            logger.warning("Readiness DB check failed: %s", exc)
            db_status = "error"
    return {
        "status": "ready" if db_status in ("ok", "not_configured") else "degraded",
        "db": db_status,
        "temporal": "connected" if _temporal_client is not None else "connecting",
    }


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
async def llm_health(request: Request):
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    status = {"ollama": None, "lmstudio": None}
    uid = u.get("id") or "default"
    user_conn = _connections_store.get(uid, {})

    ollama_base = user_conn.get("OllamaApiUrl") or OLLAMA_HOST
    lmstudio_base = user_conn.get("LmStudioApiUrl") or LMSTUDIO_HOST

    if not await _validate_url_for_ssrf(ollama_base):
        status["ollama"] = {"ok": False, "error": "Invalid or unsafe Ollama URL"}
        ollama_base = None
    if not await _validate_url_for_ssrf(lmstudio_base):
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
async def get_default_user(request: Request):
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    return {"id": u.get("id"), "username": u.get("username"), "role": u.get("role")}

# ------------------------- Missing API endpoints that frontend expects -------------------------

@app.get("/api/dashboard/status")
async def dashboard_status(request: Request, userId: str = Query(), db: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Dashboard status with active LLM selection for the user"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if userId != u.get("id") and u.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Forbidden")

    status = {
        "user_id": userId,
        "status": "active",
        "character_count": 3,
        "agent_count": 0,
        "conversation_count": 0,
        "llmEngine": None,
        "llmModel": None,
    }

    # Pull current selection from DB; fall back to defaults/cache
    try:
        if db is not None:
            async with db.connect() as conn:
                # LLM selection
                res = await conn.execute(select(t_chat_settings).where(t_chat_settings.c.user_id == userId).limit(1))
                row = res.first()
                if row:
                    m = row._mapping
                    status["llmEngine"] = (m.get("llm_engine") or os.getenv("DEFAULT_LLM_ENGINE", "ollama")).lower()
                    status["llmModel"] = m.get("llm_model") or os.getenv("LLM_MODEL", "llama3")
                else:
                    s = _chat_settings_store.get(userId) or _default_chat_settings(userId)
                    status["llmEngine"] = (s.get("llmEngine") or os.getenv("DEFAULT_LLM_ENGINE", "ollama")).lower()
                    status["llmModel"] = s.get("llmModel") or os.getenv("LLM_MODEL", "llama3")
                # Counts (best-effort)
                try:
                    cr = await conn.execute(select(t_characters.c.id))
                    status["character_count"] = len(cr.fetchall())
                except Exception:
                    pass
                try:
                    cv = await conn.execute(select(t_conversations.c.id).where(t_conversations.c.user_id == userId))
                    status["conversation_count"] = len(cv.fetchall())
                except Exception:
                    pass
    except Exception as e:
        logger.exception("dashboard_status failed: %s", e)

    return status

@app.get("/api/agents/catalog")
async def agents_catalog(request: Request):
    """Available agents catalog that frontend expects"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")

    # Return available agents - for now empty, will be populated when agents are registered
    return []

@app.get("/api/folder/user/{user_id}")
async def get_user_folders(user_id: str, request: Request):
    """User folders endpoint that frontend calls"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")

    # Return empty folders for now - this will store user file organization
    return {"folders": []}

@app.get("/api/conversation/user/{user_id}")
async def get_user_conversations(user_id: str, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """List conversations for a user (array result expected by frontend)"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if u.get("id") != user_id and u.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Forbidden")
    if engine is None:
        return []
    async with engine.connect() as conn:
        res = await conn.execute(select(t_conversations).where(t_conversations.c.user_id == user_id))
        rows = res.fetchall()
        out = []
        for row in rows:
            m = row._mapping
            out.append({
                "id": m["id"],
                "userId": m["user_id"],
                "title": m["title"],
                "folderId": m["folder_id"],
                "createdAt": _dt_str(m["created_at"]),
                "lastUpdated": _dt_str(m["last_updated"]),
            })
        return out

@app.post("/api/conversation")
async def create_conversation(body: dict, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Create a new conversation for the authenticated user"""
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    uid = u.get("id")
    title = _assert_length((body or {}).get("title") or "New Chat", _MAX_TITLE_LEN, "title")
    folder_id = (body or {}).get("folderId")
    conv_id = str(uuid.uuid4())
    now = datetime.now(timezone.utc)
    async with engine.begin() as conn:
        await conn.execute(t_conversations.insert().values(
            id=conv_id,
            user_id=uid,
            title=title,
            folder_id=folder_id,
            created_at=now,
            last_updated=now,
        ))
    return {
        "id": conv_id,
        "userId": uid,
        "title": title,
        "folderId": folder_id,
        "createdAt": now.isoformat(),
        "lastUpdated": now.isoformat(),
    }

@app.get("/api/conversation/{conv_id}")
async def get_conversation(conv_id: str, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    async with engine.connect() as conn:
        res = await conn.execute(select(t_conversations).where(t_conversations.c.id == conv_id).limit(1))
        row = res.first()
        if not row:
            raise HTTPException(status_code=404, detail="Not Found")
        m = row._mapping
        if u.get("role") != "admin" and m["user_id"] != u.get("id"):
            raise HTTPException(status_code=403, detail="Forbidden")
        return {
            "id": m["id"],
            "userId": m["user_id"],
            "title": m["title"],
            "folderId": m["folder_id"],
            "createdAt": m["created_at"],
            "lastUpdated": m["last_updated"],
        }

@app.get("/api/conversation/{conv_id}/messages")
async def get_messages(
    conv_id: str,
    request: Request,
    limit: int = Query(100, ge=1, le=500),
    offset: int = Query(0, ge=0),
    engine: Optional[AsyncEngine] = Depends(get_engine_dep),
):
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        return []
    async with engine.connect() as conn:
        # verify ownership
        conv = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
        conv_row = conv.first()
        if not conv_row:
            return []
        if u.get("role") != "admin" and conv_row._mapping["user_id"] != u.get("id"):
            raise HTTPException(status_code=403, detail="Forbidden")
        res = await conn.execute(
            select(t_messages)
            .where(t_messages.c.conversation_id == conv_id)
            .order_by(t_messages.c.timestamp)
            .limit(limit)
            .offset(offset)
        )
        rows = res.fetchall()
        return [{
            "id": r._mapping["id"],
            "conversationId": r._mapping["conversation_id"],
            "role": r._mapping["role"],
            "content": r._mapping["content"],
            "timestamp": _dt_str(r._mapping["timestamp"]),
        } for r in rows]

@app.post("/api/conversation/message")
async def append_message(body: dict, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    conv_id = (body or {}).get("conversationId")
    role = (body or {}).get("role") or "user"
    content = _assert_length((body or {}).get("content") or "", _MAX_MESSAGE_LEN, "content")
    if not conv_id:
        raise HTTPException(status_code=400, detail="conversationId required")
    now = datetime.now(timezone.utc)
    async with engine.begin() as conn:
        # verify ownership before appending
        conv = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
        conv_row = conv.first()
        if not conv_row:
            raise HTTPException(status_code=404, detail="Conversation not found")
        if u.get("role") != "admin" and conv_row._mapping["user_id"] != u.get("id"):
            raise HTTPException(status_code=403, detail="Forbidden")
        msg_id = str(uuid.uuid4())
        await conn.execute(t_messages.insert().values(
            id=msg_id,
            conversation_id=conv_id,
            role=role,
            content=content,
            timestamp=now,
        ))
        # bump conversation last_updated
        await conn.execute(t_conversations.update().where(t_conversations.c.id == conv_id).values(last_updated=now))
    return {
        "id": msg_id,
        "conversationId": conv_id,
        "role": role,
        "content": content,
        "timestamp": now.isoformat(),
    }

@app.put("/api/conversation/{conv_id}/title")
async def update_conversation_title(conv_id: str, body: dict, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    title = _assert_length((body or {}).get("title") or "", _MAX_TITLE_LEN, "title")
    now = datetime.now(timezone.utc)
    async with engine.begin() as conn:
        # verify ownership
        conv = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
        conv_row = conv.first()
        if not conv_row:
            raise HTTPException(status_code=404, detail="Conversation not found")
        if u.get("role") != "admin" and conv_row._mapping["user_id"] != u.get("id"):
            raise HTTPException(status_code=403, detail="Forbidden")
        await conn.execute(t_conversations.update().where(t_conversations.c.id == conv_id).values(title=title, last_updated=now))
    return {"success": True}

@app.put("/api/conversation/{conv_id}/folder")
async def update_conversation_folder(conv_id: str, body: dict, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    folder_id = (body or {}).get("folderId")
    now = datetime.now(timezone.utc)
    async with engine.begin() as conn:
        # verify ownership
        conv = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
        conv_row = conv.first()
        if not conv_row:
            raise HTTPException(status_code=404, detail="Conversation not found")
        if u.get("role") != "admin" and conv_row._mapping["user_id"] != u.get("id"):
            raise HTTPException(status_code=403, detail="Forbidden")
        await conn.execute(t_conversations.update().where(t_conversations.c.id == conv_id).values(folder_id=folder_id, last_updated=now))
    return {"success": True}

@app.put("/api/conversation/{conv_id}/open")
async def update_last_open(conv_id: str, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    now = datetime.now(timezone.utc)
    async with engine.begin() as conn:
        # verify ownership
        conv = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
        conv_row = conv.first()
        if not conv_row:
            raise HTTPException(status_code=404, detail="Conversation not found")
        if u.get("role") != "admin" and conv_row._mapping["user_id"] != u.get("id"):
            raise HTTPException(status_code=403, detail="Forbidden")
        await conn.execute(t_conversations.update().where(t_conversations.c.id == conv_id).values(last_updated=now))
    return {"success": True}

@app.delete("/api/conversation/{conv_id}")
async def delete_conversation(conv_id: str, request: Request, userId: Optional[str] = Query(None), engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    uid = u.get("id")
    # if query userId provided and doesn't match, require admin
    if userId and userId != uid and u.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Forbidden")
    async with engine.begin() as conn:
        # verify ownership
        conv = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
        conv_row = conv.first()
        if not conv_row:
            # idempotent delete
            return {"success": True}
        if u.get("role") != "admin" and conv_row._mapping["user_id"] != uid:
            raise HTTPException(status_code=403, detail="Forbidden")
        await conn.execute(t_messages.delete().where(t_messages.c.conversation_id == conv_id))
        await conn.execute(t_conversations.delete().where(t_conversations.c.id == conv_id))
    return {"success": True}

@app.post("/api/conversation/chat")
async def conversation_chat(body: dict, request: Request, db: Optional[AsyncEngine] = Depends(get_engine_dep)):
    if db is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    conv_id = (body or {}).get("conversationId")
    message = _assert_length(
        (body or {}).get("message") or (body or {}).get("content") or (body or {}).get("prompt") or "",
        _MAX_MESSAGE_LEN, "message"
    )
    if not conv_id or not message:
        raise HTTPException(status_code=400, detail="conversationId and message required")
    now = datetime.now(timezone.utc)
    async with db.begin() as conn:
        # verify ownership
        conv = await conn.execute(select(t_conversations.c.user_id).where(t_conversations.c.id == conv_id).limit(1))
        conv_row = conv.first()
        if not conv_row:
            raise HTTPException(status_code=404, detail="Conversation not found")
        if u.get("role") != "admin" and conv_row._mapping["user_id"] != u.get("id"):
            raise HTTPException(status_code=403, detail="Forbidden")
        # append user message
        await conn.execute(t_messages.insert().values(
            id=str(uuid.uuid4()), conversation_id=conv_id, role="user", content=message, timestamp=now
        ))
        await conn.execute(t_conversations.update().where(t_conversations.c.id == conv_id).values(last_updated=now))

    # Fetch user settings and connection URLs
    uid = u.get("id")
    engine_name = os.getenv("DEFAULT_LLM_ENGINE", "ollama").lower()
    model = os.getenv("LLM_MODEL", "llama3")
    ollama_url = OLLAMA_HOST
    lmstudio_url = LMSTUDIO_HOST
    try:
        async with db.connect() as conn:
            res = await conn.execute(select(t_chat_settings).where(t_chat_settings.c.user_id == uid).limit(1))
            row = res.first()
            if row:
                m = row._mapping
                engine_name = (m.get("llm_engine") or engine_name).lower()
                model = m.get("llm_model") or model
            res2 = await conn.execute(select(t_conn_settings).where(t_conn_settings.c.user_id == uid).limit(1))
            row2 = res2.first()
            if row2:
                m2 = row2._mapping
                ollama_url = m2.get("OllamaApiUrl") or ollama_url
                lmstudio_url = m2.get("LmStudioApiUrl") or lmstudio_url
    except Exception:
        pass

    # SSRF guard: validate URLs before making outbound requests
    if not await _validate_url_for_ssrf(ollama_url):
        ollama_url = None
    if not await _validate_url_for_ssrf(lmstudio_url):
        lmstudio_url = None

    # Call the selected engine
    reply: Optional[str] = None
    llm_error: Optional[str] = None
    try:
        if engine_name == "lmstudio" and lmstudio_url:
            payload = {
                "model": model or "auto",
                "messages": [{"role": "user", "content": message}],
                "temperature": 0.7,
                "stream": False,
            }
            async with httpx.AsyncClient(timeout=30.0) as client:
                r = await client.post(f"{lmstudio_url.rstrip('/')}/v1/chat/completions", json=payload)
                if r.status_code == 200:
                    j = r.json()
                    reply = (((j.get("choices") or [{}])[0]).get("message") or {}).get("content")
                else:
                    llm_error = f"LM Studio returned {r.status_code}"
        elif engine_name == "ollama" and ollama_url:
            payload = {"model": model or "llama3", "prompt": message, "stream": False}
            async with httpx.AsyncClient(timeout=30.0) as client:
                r = await client.post(f"{ollama_url.rstrip('/')}/api/generate", json=payload)
                if r.status_code == 200:
                    j = r.json()
                    reply = j.get("response")
                else:
                    llm_error = f"Ollama returned {r.status_code}"
        else:
            llm_error = f"Engine '{engine_name}' is unavailable or its URL is not configured"
    except Exception as e:
        logger.exception("LLM call failed (%s): %s", engine_name, e)
        llm_error = "LLM service error"

    if not reply:
        error_msg = llm_error or "LLM did not return a response"
        logger.warning("LLM no reply for conv %s: %s", conv_id, error_msg)
        raise HTTPException(status_code=502, detail=error_msg)

    # Store assistant reply
    async with db.begin() as conn:
        await conn.execute(t_messages.insert().values(
            id=str(uuid.uuid4()), conversation_id=conv_id, role="assistant", content=reply, timestamp=now
        ))
        await conn.execute(t_conversations.update().where(t_conversations.c.id == conv_id).values(last_updated=now))
    return {"response": reply}

@app.get("/api/chat/settings/{user_id}")
async def get_chat_settings(user_id: str, request: Request, db: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Chat settings endpoint used by chat and dashboard"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if u.get("id") != user_id and u.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Forbidden")

    # Base defaults
    s = dict(_chat_settings_store.get(user_id, _default_chat_settings(user_id)))

    # Merge persisted chat + connection settings
    if db is not None:
        try:
            async with db.connect() as conn:
                res = await conn.execute(select(t_chat_settings).where(t_chat_settings.c.user_id == user_id).limit(1))
                row = res.first()
                if row:
                    m = row._mapping
                    s["llmEngine"] = m.get("llm_engine") or s.get("llmEngine")
                    s["llmModel"] = m.get("llm_model") or s.get("llmModel")
                    s["ttsProvider"] = m.get("tts_provider") or s.get("ttsProvider")
                    s["ttsVoiceId"] = m.get("tts_voice_id") or s.get("ttsVoiceId")
                    # Merge JSON fields if present
                    try:
                        ee = m.get("enabled_engines")
                        if ee:
                            s["enabledEngines"] = json.loads(ee)
                    except Exception:
                        pass
                    try:
                        em = m.get("engine_models")
                        if em:
                            s["engineModels"] = json.loads(em)
                    except Exception:
                        pass
                # Enable engines based on connection URLs presence
                res2 = await conn.execute(select(t_conn_settings).where(t_conn_settings.c.user_id == user_id).limit(1))
                row2 = res2.first()
                if row2:
                    m2 = row2._mapping
                    enabled = dict(s.get("enabledEngines") or {})
                    if (m2.get("OllamaApiUrl") or "").strip():
                        enabled["ollama"] = True
                    if (m2.get("LmStudioApiUrl") or "").strip():
                        enabled["lmstudio"] = True
                    s["enabledEngines"] = enabled
        except Exception as e:
            logger.exception("get_chat_settings merge failed: %s", e)

    return s

@app.put("/api/chat/settings/{user_id}")
async def update_chat_settings(user_id: str, settings: dict, request: Request, db: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Update chat settings endpoint that frontend calls"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if u.get("id") != user_id and u.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Forbidden")

    # Update in-memory cache
    _chat_settings_store[user_id] = settings
    _chat_settings_ts[user_id] = time.time()

    # Persist to DB
    if db is not None:
        try:
            engine_name = (settings.get("llmEngine") or settings.get("llm_engine") or "ollama").lower()
            model = settings.get("llmModel") or settings.get("llm_model") or ""
            tts_provider = settings.get("ttsProvider") or settings.get("tts_provider") or ""
            tts_voice_id = settings.get("ttsVoiceId") or settings.get("tts_voice_id") or ""
            enabled_engines = settings.get("enabledEngines") or settings.get("enabled_engines")
            engine_models = settings.get("engineModels") or settings.get("engine_models")
            values: Dict[str, Any] = {
                "llm_engine": engine_name,
                "llm_model": model,
                "tts_provider": tts_provider,
                "tts_voice_id": tts_voice_id,
            }
            if enabled_engines is not None:
                values["enabled_engines"] = json.dumps(enabled_engines) if not isinstance(enabled_engines, str) else enabled_engines
            if engine_models is not None:
                values["engine_models"] = json.dumps(engine_models) if not isinstance(engine_models, str) else engine_models
            async with db.begin() as conn:
                res = await conn.execute(select(t_chat_settings).where(t_chat_settings.c.user_id == user_id).limit(1))
                if res.first() is not None:
                    await conn.execute(t_chat_settings.update().where(t_chat_settings.c.user_id == user_id).values(**values))
                else:
                    await conn.execute(t_chat_settings.insert().values(user_id=user_id, **values))
        except Exception as e:
            logger.exception("Failed to persist chat settings for user %s: %s", user_id, e)

    return {"success": True}

@app.get("/api/llm/models")
async def get_llm_models(request: Request, engine: str = Query(), userId: str = Query(), db: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Return available models for a given engine (ollama|lmstudio)."""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if userId != u.get("id") and u.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Forbidden")

    eng = (engine or "").strip().lower()
    ollama_url = OLLAMA_HOST
    lmstudio_url = LMSTUDIO_HOST

    # Load connection settings
    if db is not None:
        try:
            async with db.connect() as conn:
                res = await conn.execute(select(t_conn_settings).where(t_conn_settings.c.user_id == userId).limit(1))
                row = res.first()
                if row:
                    m = row._mapping
                    ollama_url = m.get("OllamaApiUrl") or ollama_url
                    lmstudio_url = m.get("LmStudioApiUrl") or lmstudio_url
        except Exception:
            pass

    # SSRF guard
    if not await _validate_url_for_ssrf(ollama_url):
        ollama_url = None
    if not await _validate_url_for_ssrf(lmstudio_url):
        lmstudio_url = None

    models: List[str] = []
    try:
        if eng == "ollama" and ollama_url:
            async with httpx.AsyncClient(timeout=15.0) as client:
                r = await client.get(f"{ollama_url.rstrip('/')}/api/tags")
                if r.status_code == 200:
                    j = r.json() or {}
                    models = [m.get("name") for m in (j.get("models") or []) if m.get("name")]
        elif eng == "lmstudio" and lmstudio_url:
            async with httpx.AsyncClient(timeout=15.0) as client:
                r = await client.get(f"{lmstudio_url.rstrip('/')}/v1/models")
                if r.status_code == 200:
                    j = r.json() or {}
                    data = j.get("data") or []
                    # LM Studio is OpenAI-compatible: each item has an id
                    models = [m.get("id") for m in data if m.get("id")]
    except Exception as e:
        logger.exception("get_llm_models error for %s: %s", eng, e)

    return {"models": models}

@app.get("/api/llm/lmstudio/model")
async def get_lmstudio_model(request: Request, userId: str = Query(), db: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Return the currently available/loaded LM Studio model (best-effort)."""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if userId != u.get("id") and u.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Forbidden")

    lmstudio_url = LMSTUDIO_HOST
    if db is not None:
        try:
            async with db.connect() as conn:
                res = await conn.execute(select(t_conn_settings).where(t_conn_settings.c.user_id == userId).limit(1))
                row = res.first()
                if row:
                    m = row._mapping
                    lmstudio_url = m.get("LmStudioApiUrl") or lmstudio_url
        except Exception:
            pass

    if not await _validate_url_for_ssrf(lmstudio_url):
        return {"model": None}

    model = None
    try:
        async with httpx.AsyncClient(timeout=10.0) as client:
            r = await client.get(f"{lmstudio_url.rstrip('/')}/v1/models")
            if r.status_code == 200:
                j = r.json() or {}
                data = j.get("data") or []
                if data:
                    model = (data[0] or {}).get("id")
    except Exception as e:
        logger.exception("get_lmstudio_model error: %s", e)

    return {"model": model}

class LoginRequest(BaseModel):
    username: Optional[str] = None
    email: Optional[str] = None
    password: str
    remember: bool = False


@app.post("/api/auth/login")
async def login(body: LoginRequest, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    client_ip = request.headers.get("x-forwarded-for", request.client.host if request.client else "unknown")
    _check_login_rate_limit(client_ip)

    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    identifier = (body.username or body.email or "").strip()
    if not identifier:
        raise HTTPException(status_code=400, detail="username or email required")

    async with engine.connect() as conn:
        stmt = select(users).where(
            (users.c.username == identifier) | (users.c.email == identifier)
        ).limit(1)
        res = await conn.execute(stmt)
        row = res.first()
        if not row:
            logger.warning("Login failed: unknown identifier from %s", client_ip)
            raise HTTPException(status_code=401, detail="Invalid credentials")
        r = row._mapping
        if not r["password_hash"] or not verify_password(body.password, r["password_hash"]):
            logger.warning("Login failed: wrong password for user %s from %s", r["id"], client_ip)
            raise HTTPException(status_code=401, detail="Invalid credentials")

    logger.info("Login success: user %s from %s", r["id"], client_ip)
    token = create_access_token({"sub": r["id"], "role": r["role"]})
    user_data = {"id": r["id"], "username": r["username"], "email": r["email"], "role": r["role"]}
    response = JSONResponse(content={"access_token": token, "token_type": "bearer", "user": user_data})

    # Set HTTPOnly cookie so the browser can't access the token via JS
    cookie_max_age = JWT_EXPIRES_MINUTES * 60 if body.remember else None
    is_https = request.url.scheme == "https" or request.headers.get("x-forwarded-proto") == "https"
    response.set_cookie(
        key="auth_token",
        value=token,
        httponly=True,
        secure=is_https,
        samesite="strict",
        max_age=cookie_max_age,
        path="/",
    )
    return response


@app.post("/api/auth/logout")
async def logout(request: Request):
    """Clear the HTTPOnly auth cookie."""
    response = JSONResponse(content={"success": True})
    response.delete_cookie(key="auth_token", path="/")
    logger.info("Logout: user %s", getattr(getattr(request, "state", None), "user", {}).get("id", "unknown"))
    return response

@app.get("/api/auth/me")
async def auth_me(request: Request):
    return getattr(request.state, "user", None)

class PasswordChange(BaseModel):
    old_password: str
    new_password: str


@app.post("/api/auth/change-password")
async def change_password(body: PasswordChange, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    u = getattr(request.state, "user", None)
    if not u or not u.get("id"):
        raise HTTPException(status_code=401, detail="Unauthorized")
    _validate_password_complexity(body.new_password)
    uid = u.get("id")
    async with engine.begin() as conn:
        res = await conn.execute(select(users).where(users.c.id == uid).limit(1))
        row = res.first()
        if not row:
            raise HTTPException(status_code=404, detail="User not found")
        r = row._mapping
        if not r["password_hash"] or not verify_password(body.old_password, r["password_hash"]):
            logger.warning("Password change failed: wrong old password for user %s", uid)
            raise HTTPException(status_code=401, detail="Invalid credentials")
        pw_hash = bcrypt.hashpw(body.new_password.encode(), bcrypt.gensalt()).decode()
        await conn.execute(users.update().where(users.c.id == uid).values(password_hash=pw_hash))
    logger.info("Password changed for user %s", uid)
    return {"success": True}

# Admin purge: delete conversations/messages
@app.delete("/api/admin/conversations")
async def admin_purge_conversations(
    request: Request,
    confirm: bool = Query(False, description="Must be true to perform delete"),
    userId: Optional[str] = Query(None, description="Delete only this user's conversations"),
    legacyOnly: bool = Query(False, description="Delete conversations with missing/invalid owners"),
    engine: Optional[AsyncEngine] = Depends(get_engine_dep),
):
    u = getattr(request.state, "user", None)
    require_admin(u)
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    if not confirm:
        raise HTTPException(status_code=400, detail="Set confirm=true to perform purge")

    async with engine.begin() as conn:
        # Build ORM-level filter predicate
        if userId:
            predicate = t_conversations.c.user_id == userId
        elif legacyOnly:
            user_exists = exists().where(users.c.id == t_conversations.c.user_id)
            predicate = (
                t_conversations.c.user_id.is_(None)
                | (t_conversations.c.user_id == "")
                | (t_conversations.c.user_id == "00000000-0000-0000-0000-000000000001")
                | not_(user_exists)
            )
        else:
            predicate = None

        count_query = select(func.count()).select_from(t_conversations)
        if predicate is not None:
            count_query = count_query.where(predicate)
        try:
            conv_count = (await conn.execute(count_query)).scalar()
        except Exception:
            conv_count = None

        # Delete messages for matching conversations first (FK)
        conv_ids_query = select(t_conversations.c.id)
        if predicate is not None:
            conv_ids_query = conv_ids_query.where(predicate)
        await conn.execute(
            t_messages.delete().where(t_messages.c.conversation_id.in_(conv_ids_query))
        )
        del_query = t_conversations.delete()
        if predicate is not None:
            del_query = del_query.where(predicate)
        await conn.execute(del_query)

    logger.info("Admin %s purged %s conversations (userId=%s, legacyOnly=%s)", u.get("id"), conv_count, userId, legacyOnly)
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
async def admin_list_users(
    request: Request,
    limit: int = Query(100, ge=1, le=500),
    offset: int = Query(0, ge=0),
    engine: Optional[AsyncEngine] = Depends(get_engine_dep),
):
    u = getattr(request.state, "user", None)
    require_admin(u)
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    async with engine.connect() as conn:
        res = await conn.execute(select(users).limit(limit).offset(offset))
        rows = [dict(row._mapping) for row in res.fetchall()]
        for r in rows:
            r.pop("password_hash", None)
            r.pop("recovery_codes_hash", None)
            r.pop("pin_hash", None)
        return rows

@app.post("/api/admin/users")
async def admin_create_user(body: UserCreate, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    u = getattr(request.state, "user", None)
    require_admin(u)
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    _validate_password_complexity(body.password)
    _assert_length(body.username, _MAX_NAME_LEN, "username")
    pw_hash = bcrypt.hashpw(body.password.encode(), bcrypt.gensalt()).decode()
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
async def get_user(user_id: str, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if u.get("id") != user_id and u.get("role") != "admin":
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
async def update_user(user_id: str, body: UserUpdate, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    u = getattr(request.state, "user", None)
    if not u or (u.get("id") != user_id and u.get("role") != "admin"):
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
async def update_user_pin(user_id: str, body: PinUpdate, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    u = getattr(request.state, "user", None)
    if not u or (u.get("id") != user_id and u.get("role") != "admin"):
        raise HTTPException(status_code=403, detail="Forbidden")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    pin_hash = bcrypt.hashpw(body.pin.encode(), bcrypt.gensalt()).decode()
    async with engine.begin() as conn:
        await conn.execute(users.update().where(users.c.id == user_id).values(pin_hash=pin_hash))
    return {"success": True}

@app.post("/api/user/{user_id}/recovery-codes")
async def generate_recovery_codes(user_id: str, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    u = getattr(request.state, "user", None)
    if not u or (u.get("id") != user_id and u.get("role") != "admin"):
        raise HTTPException(status_code=403, detail="Forbidden")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    # Generate 5 codes in XXXX-XXXX-XXXX format (12 hex chars each)
    codes = [
        f"{secrets.token_hex(2).upper()}-{secrets.token_hex(2).upper()}-{secrets.token_hex(2).upper()}"
        for _ in range(5)
    ]
    # Hash each code independently so individual codes can be verified and consumed
    hashes = [bcrypt.hashpw(c.encode(), bcrypt.gensalt()).decode() for c in codes]
    codes_hash = json.dumps(hashes)
    async with engine.begin() as conn:
        await conn.execute(users.update().where(users.c.id == user_id).values(recovery_codes_hash=codes_hash))
    return RecoveryCode(codes=codes)


class RecoveryCodeVerify(BaseModel):
    code: str


@app.post("/api/user/{user_id}/verify-recovery-code")
async def verify_recovery_code(user_id: str, body: RecoveryCodeVerify, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Verify a single recovery code and consume it (one-time use)."""
    u = getattr(request.state, "user", None)
    if not u or (u.get("id") != user_id and u.get("role") != "admin"):
        raise HTTPException(status_code=403, detail="Forbidden")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")
    async with engine.begin() as conn:
        res = await conn.execute(select(users.c.recovery_codes_hash).where(users.c.id == user_id).limit(1))
        row = res.first()
        if not row or not row.recovery_codes_hash:
            raise HTTPException(status_code=404, detail="No recovery codes found")
        try:
            hashes: List[str] = json.loads(row.recovery_codes_hash)
        except (json.JSONDecodeError, TypeError):
            raise HTTPException(status_code=400, detail="Recovery codes in invalid format; regenerate codes")
        matched_idx = None
        for i, h in enumerate(hashes):
            if bcrypt.checkpw(body.code.encode(), h.encode()):
                matched_idx = i
                break
        if matched_idx is None:
            raise HTTPException(status_code=401, detail="Invalid recovery code")
        # Consume the matched code so it cannot be reused
        remaining = [h for i, h in enumerate(hashes) if i != matched_idx]
        await conn.execute(users.update().where(users.c.id == user_id).values(
            recovery_codes_hash=json.dumps(remaining) if remaining else None
        ))
    return {"success": True, "remaining_codes": len(remaining)}

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
                logger.warning("Failed to parse JSON from chat settings value %r", value)
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
async def get_llm_settings(request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    uid = u.get("id") or "default"

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
            logger.exception("Database read failed in get_llm_settings: %s", e)

    s = _chat_settings_store.get(uid) or _default_chat_settings(uid)
    return {"engine": s.get("llmEngine", "ollama"), "model": s.get("llmModel", os.getenv("LLM_MODEL", "llama3"))}

@app.put("/api/settings/llm")
async def put_llm_settings(payload: dict, request: Request, db: Optional[AsyncEngine] = Depends(get_engine_dep)):
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    uid = u.get("id")
    if not uid:
        raise HTTPException(status_code=401, detail="Invalid user authentication")

    engine_name = (payload.get("engine") or "ollama").lower()
    model = payload.get("model") or ""

    # Update in-memory cache used by chat page fallback
    s = _chat_settings_store.get(uid) or _default_chat_settings(uid)
    s["llmEngine"] = engine_name
    s["llmModel"] = model
    _chat_settings_store[uid] = s

    # Persist to DB
    if db is not None:
        try:
            async with db.begin() as conn:
                res = await conn.execute(select(t_chat_settings).where(t_chat_settings.c.user_id == uid).limit(1))
                exists = res.first() is not None
                values = {"llm_engine": engine_name, "llm_model": model}
                if exists:
                    await conn.execute(t_chat_settings.update().where(t_chat_settings.c.user_id == uid).values(**values))
                else:
                    await conn.execute(t_chat_settings.insert().values(user_id=uid, **values))
        except Exception as e:
            logger.exception("Failed to persist LLM settings: %s", e)

    return {"success": True}

# ------------------------- Character Management -------------------------

@app.get("/api/characters")
async def list_characters(request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """List characters accessible to the current user (their own + shared characters)"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    uid = u.get("id")
    is_admin = u.get("role") == "admin"

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
async def create_character(body: CharacterCreate, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Create a new character"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    uid = u.get("id")
    is_admin = u.get("role") == "admin"

    # Only admins can create shared characters
    if body.is_shared and not is_admin:
        raise HTTPException(status_code=403, detail="Only admins can create shared characters")

    _assert_length(body.name, _MAX_NAME_LEN, "name")
    _assert_length(body.system_prompt, _MAX_SYSTEM_PROMPT_LEN, "system_prompt")

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
async def get_character(character_id: str, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Get a specific character"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    uid = u.get("id")
    is_admin = u.get("role") == "admin"

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
async def update_character(character_id: str, body: CharacterUpdate, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Update a character"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    uid = u.get("id")
    is_admin = u.get("role") == "admin"

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
async def delete_character(character_id: str, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Delete a character"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    uid = u.get("id")
    is_admin = u.get("role") == "admin"

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
async def list_user_characters(user_id: str, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """List characters for a specific user (legacy endpoint)"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    current_uid = u.get("id")
    is_admin = u.get("role") == "admin"

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
async def create_character_legacy(body: CharacterCreate, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Create a new character (legacy endpoint)"""
    return await create_character(body, request, engine)

@app.put("/api/character/{character_id}")
async def update_character_legacy(character_id: str, body: CharacterUpdate, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Update a character (legacy endpoint)"""
    return await update_character(character_id, body, request, engine)

@app.delete("/api/character/{character_id}")
async def delete_character_legacy(character_id: str, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Delete a character (legacy endpoint)"""
    return await delete_character(character_id, request, engine)

# Character YAML endpoints for the CharacterEditor frontend
@app.post("/api/character/yaml")
async def create_character_yaml(body: dict, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Create a new character from YAML format"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    yaml_content = body.get("yamlProfile", "")
    if not yaml_content:
        raise HTTPException(status_code=400, detail="YAML profile required")
    if len(yaml_content.encode()) > _MAX_YAML_BYTES:
        raise HTTPException(status_code=400, detail=f"YAML too large (max {_MAX_YAML_BYTES // 1024} KB).")

    try:
        data = yaml.safe_load(yaml_content)
        if not isinstance(data, dict):
            raise HTTPException(status_code=400, detail="Invalid YAML format")

        # Extract character data from YAML - user from middleware only
        name = data.get("name", "New Character")
        character_id = str(uuid.uuid4())

        # Insert character using authenticated user ID only
        async with engine.begin() as conn:
            await conn.execute(
                t_characters.insert(),
                {
                    "id": character_id,
                    "user_id": u["id"],  # Enforce server-side user ownership
                    "name": name,
                    "yaml_profile": yaml_content,
                    "created_at": datetime.now(timezone.utc),
                    "last_modified": datetime.now(timezone.utc)
                }
            )

        return {"id": character_id, "name": name, "success": True}

    except HTTPException:
        raise
    except Exception as e:
        logger.exception("create_character_yaml failed for user %s: %s", u.get("id"), e)
        raise HTTPException(status_code=400, detail="Failed to create character")

@app.put("/api/character/{character_id}/yaml")
async def update_character_yaml(character_id: str, body: dict, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Update an existing character from YAML format"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    yaml_content = body.get("yamlProfile", "")
    if not yaml_content:
        raise HTTPException(status_code=400, detail="YAML profile required")
    if len(yaml_content.encode()) > _MAX_YAML_BYTES:
        raise HTTPException(status_code=400, detail=f"YAML too large (max {_MAX_YAML_BYTES // 1024} KB).")

    try:
        data = yaml.safe_load(yaml_content)
        if not isinstance(data, dict):
            raise HTTPException(status_code=400, detail="Invalid YAML format")

        # Extract character data from YAML
        name = data.get("name", "Updated Character")

        # Verify ownership and update character
        async with engine.begin() as conn:
            # Check ownership - critical security check
            result = await conn.execute(
                t_characters.select().where(
                    (t_characters.c.id == character_id) &
                    (t_characters.c.user_id == u["id"])  # Enforce ownership
                )
            )
            existing = result.fetchone()

            if not existing:
                raise HTTPException(status_code=404, detail="Character not found or access denied")

            # Update the character
            await conn.execute(
                t_characters.update().where(t_characters.c.id == character_id),
                {
                    "name": name,
                    "yaml_profile": yaml_content,
                    "last_modified": datetime.now(timezone.utc)
                }
            )

        return {"id": character_id, "name": name, "success": True}

    except HTTPException:
        raise
    except Exception as e:
        logger.exception("update_character_yaml failed for character %s: %s", character_id, e)
        raise HTTPException(status_code=400, detail="Failed to update character")

@app.post("/api/character/import-yaml")
async def import_character_yaml(body: dict, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Import a character from YAML format"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    yaml_content = body.get("yaml", "")
    if not yaml_content:
        raise HTTPException(status_code=400, detail="YAML content required")
    if len(yaml_content.encode()) > _MAX_YAML_BYTES:
        raise HTTPException(status_code=400, detail=f"YAML too large (max {_MAX_YAML_BYTES // 1024} KB).")

    try:
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

        return await create_character(char_create, request, engine)

    except HTTPException:
        raise
    except Exception as e:
        logger.exception("import_character_yaml failed for user %s: %s", u.get("id"), e)
        raise HTTPException(status_code=400, detail="Failed to parse YAML")

# ------------------------- External Agent Management -------------------------

class AgentRegistration(BaseModel):
    agent_id: str
    name: str
    description: Optional[str] = None
    capabilities: List[str] = []
    version: Optional[str] = None
    health_endpoint: Optional[str] = None
    api_key: Optional[str] = None

class TaskCreate(BaseModel):
    agent_id: str
    name: str
    description: Optional[str] = None
    input_data: Optional[dict] = None
    priority: str = "normal"

class TaskUpdate(BaseModel):
    status: Optional[str] = None
    progress: Optional[str] = None
    current_step: Optional[str] = None
    output_data: Optional[dict] = None
    error_message: Optional[str] = None
    estimated_completion: Optional[str] = None

class ResultCreate(BaseModel):
    result_type: str
    name: str
    description: Optional[str] = None
    content: Optional[dict] = None
    file_path: Optional[str] = None
    file_size: Optional[str] = None
    mime_type: Optional[str] = None
    metadata: Optional[dict] = None

def current_timestamp():
    """Get current UTC timestamp as a timezone-aware datetime object"""
    return datetime.now(timezone.utc)

def _hash_api_key(api_key: str) -> str:
    """Hash API key for secure storage"""
    return bcrypt.hashpw(api_key.encode(), bcrypt.gensalt()).decode()

def _verify_api_key(api_key: str, hashed: str) -> bool:
    """Verify API key against hash"""
    return bcrypt.checkpw(api_key.encode(), hashed.encode())

async def _validate_agent_auth(agent_id: str, api_key: str, engine: AsyncEngine) -> bool:
    """Validate agent authentication"""
    async with engine.connect() as conn:
        res = await conn.execute(
            select(agent_registry.c.api_key)
            .where(agent_registry.c.id == agent_id)
            .where(agent_registry.c.status == "available")
            .limit(1)
        )
        row = res.first()
        if not row or not row.api_key:
            return False
        return _verify_api_key(api_key, row.api_key)

# Agent Registration (admin-only for security)
@app.post("/api/agents/register")
async def register_agent(body: AgentRegistration, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Register an external agent service (admin only)"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    require_admin(u)
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    now = current_timestamp()
    async with engine.begin() as conn:
        # Use upsert - try update first, then insert
        res = await conn.execute(
            agent_registry.update()
            .where(agent_registry.c.id == body.agent_id)
            .values(
                name=body.name,
                description=body.description,
                capabilities=json.dumps(body.capabilities),
                version=body.version,
                health_endpoint=body.health_endpoint,
                api_key=_hash_api_key(body.api_key) if body.api_key else None,
                status="available",
                updated_at=now,
            )
        )

        if res.rowcount == 0:  # type: ignore[attr-defined]
            await conn.execute(
                agent_registry.insert().values(
                    id=body.agent_id,
                    name=body.name,
                    description=body.description,
                    capabilities=json.dumps(body.capabilities),
                    version=body.version,
                    health_endpoint=body.health_endpoint,
                    api_key=_hash_api_key(body.api_key) if body.api_key else None,
                    status="available",
                    created_at=now,
                    updated_at=now,
                )
            )

    return {"success": True, "message": f"Agent {body.agent_id} registered successfully"}

# Agent Discovery (for users to see available agents)
@app.get("/api/agents/available")
async def list_available_agents(request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """List all available external agents"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    async with engine.connect() as conn:
        res = await conn.execute(
            select(agent_registry)
            .where(agent_registry.c.status == "available")
            .order_by(agent_registry.c.name)
        )

        agents = []
        for row in res.fetchall():
            agent_dict = dict(row._mapping)
            # Parse capabilities JSON
            try:
                agent_dict["capabilities"] = json.loads(agent_dict["capabilities"] or "[]")
            except (json.JSONDecodeError, TypeError):
                agent_dict["capabilities"] = []
            # Remove sensitive data
            agent_dict.pop("api_key", None)
            agents.append(agent_dict)

        return agents

# Task Creation (user-scoped)
@app.post("/api/agents/tasks")
async def create_agent_task(body: TaskCreate, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Create a new agent task for the current user"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    uid = u.get("id")
    if not uid:
        raise HTTPException(status_code=401, detail="Invalid user authentication")

    # Verify agent exists
    async with engine.connect() as conn:
        res = await conn.execute(
            select(agent_registry.c.id)
            .where(agent_registry.c.id == body.agent_id)
            .where(agent_registry.c.status == "available")
            .limit(1)
        )
        if not res.first():
            raise HTTPException(status_code=404, detail="Agent not found or unavailable")

    # Create task
    task_id = str(uuid.uuid4())
    now = current_timestamp()

    async with engine.begin() as conn:
        await conn.execute(
            agent_tasks.insert().values(
                id=task_id,
                agent_id=body.agent_id,
                user_id=uid,
                name=body.name,
                description=body.description,
                status="pending",
                input_data=json.dumps(body.input_data) if body.input_data else None,
                priority=body.priority,
                created_at=now,
                updated_at=now,
            )
        )

    return {"task_id": task_id, "success": True}

# Task Status Updates (from external agents)
@app.patch("/api/agents/tasks/{task_id}")
async def update_agent_task(task_id: str, body: TaskUpdate, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Update task status (called by external agents)"""
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    # Verify agent authentication
    agent_id = request.headers.get("X-Agent-ID")
    api_key = request.headers.get("X-Agent-API-Key")

    if not agent_id or not api_key:
        raise HTTPException(status_code=401, detail="Agent authentication required")

    if not await _validate_agent_auth(agent_id, api_key, engine):
        raise HTTPException(status_code=403, detail="Invalid agent credentials")

    now = current_timestamp()

    async with engine.begin() as conn:
        # Check if task exists and verify agent assignment
        res = await conn.execute(
            select(agent_tasks).where(agent_tasks.c.id == task_id).limit(1)
        )
        task = res.first()
        if not task:
            raise HTTPException(status_code=404, detail="Task not found")

        # Verify agent is assigned to this task
        if task.agent_id != agent_id:
            raise HTTPException(status_code=403, detail="Agent not assigned to this task")

        # Build update values
        update_values = {"updated_at": now}

        if body.status is not None:
            update_values["status"] = body.status
            if body.status in ("completed", "failed", "cancelled"):
                update_values["completed_at"] = now
            elif body.status == "working" and not task.started_at:
                update_values["started_at"] = now

        if body.progress is not None:
            update_values["progress"] = body.progress
        if body.current_step is not None:
            update_values["current_step"] = body.current_step
        if body.output_data is not None:
            update_values["output_data"] = json.dumps(body.output_data)
        if body.error_message is not None:
            update_values["error_message"] = body.error_message
        if body.estimated_completion is not None:
            update_values["estimated_completion"] = body.estimated_completion

        await conn.execute(
            agent_tasks.update()
            .where(agent_tasks.c.id == task_id)
            .values(**update_values)
        )

    return {"success": True}

# User Task Management (user-scoped viewing)
@app.get("/api/agents/tasks/my")
async def list_my_agent_tasks(request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """List current user's agent tasks"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    uid = u.get("id")
    is_admin = u.get("role") == "admin"

    async with engine.connect() as conn:
        if is_admin:
            # Admins can see all tasks
            res = await conn.execute(
                select(agent_tasks, agent_registry.c.name.label("agent_name"))
                .join(agent_registry, agent_tasks.c.agent_id == agent_registry.c.id)
                .order_by(agent_tasks.c.created_at.desc())
            )
        else:
            # Regular users only see their own tasks
            res = await conn.execute(
                select(agent_tasks, agent_registry.c.name.label("agent_name"))
                .join(agent_registry, agent_tasks.c.agent_id == agent_registry.c.id)
                .where(agent_tasks.c.user_id == uid)
                .order_by(agent_tasks.c.created_at.desc())
            )

        tasks = []
        for row in res.fetchall():
            task_dict = dict(row._mapping)
            # Parse JSON fields
            try:
                task_dict["input_data"] = json.loads(task_dict["input_data"] or "{}")
                task_dict["output_data"] = json.loads(task_dict["output_data"] or "{}")
            except (json.JSONDecodeError, TypeError):
                task_dict["input_data"] = {}
                task_dict["output_data"] = {}
            tasks.append(task_dict)

        return tasks

# Get specific task (user-scoped)
@app.get("/api/agents/tasks/{task_id}")
async def get_agent_task(task_id: str, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Get a specific agent task"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    uid = u.get("id")
    is_admin = u.get("role") == "admin"

    async with engine.connect() as conn:
        res = await conn.execute(
            select(agent_tasks, agent_registry.c.name.label("agent_name"))
            .join(agent_registry, agent_tasks.c.agent_id == agent_registry.c.id)
            .where(agent_tasks.c.id == task_id)
            .limit(1)
        )
        row = res.first()

        if not row:
            raise HTTPException(status_code=404, detail="Task not found")

        task_dict = dict(row._mapping)

        # Check permissions: users can only see their own tasks, admins can see all
        if not is_admin and task_dict["user_id"] != uid:
            raise HTTPException(status_code=403, detail="Access denied")

        # Parse JSON fields
        try:
            task_dict["input_data"] = json.loads(task_dict["input_data"] or "{}")
            task_dict["output_data"] = json.loads(task_dict["output_data"] or "{}")
        except (json.JSONDecodeError, TypeError):
            task_dict["input_data"] = {}
            task_dict["output_data"] = {}

        return task_dict

# Results Storage (user-scoped)
@app.post("/api/agents/tasks/{task_id}/results")
async def create_task_result(task_id: str, body: ResultCreate, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Create a result for a task (called by external agents)"""
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    # Verify agent authentication
    agent_id = request.headers.get("X-Agent-ID")
    api_key = request.headers.get("X-Agent-API-Key")

    if not agent_id or not api_key:
        raise HTTPException(status_code=401, detail="Agent authentication required")

    if not await _validate_agent_auth(agent_id, api_key, engine):
        raise HTTPException(status_code=403, detail="Invalid agent credentials")

    async with engine.begin() as conn:
        # Get task to verify it exists, get user_id for isolation, and verify agent assignment
        res = await conn.execute(
            select(agent_tasks.c.user_id, agent_tasks.c.agent_id)
            .where(agent_tasks.c.id == task_id)
            .limit(1)
        )
        task = res.first()
        if not task:
            raise HTTPException(status_code=404, detail="Task not found")

        # Verify agent is assigned to this task
        if task.agent_id != agent_id:
            raise HTTPException(status_code=403, detail="Agent not assigned to this task")

        user_id = task.user_id
        result_id = str(uuid.uuid4())
        now = current_timestamp()

        await conn.execute(
            agent_results.insert().values(
                id=result_id,
                task_id=task_id,
                user_id=user_id,  # Ensure user isolation
                result_type=body.result_type,
                name=body.name,
                description=body.description,
                content=json.dumps(body.content) if body.content else None,
                file_path=body.file_path,
                file_size=body.file_size,
                mime_type=body.mime_type,
                metadata=json.dumps(body.metadata) if body.metadata else None,
                created_at=now,
            )
        )

    return {"result_id": result_id, "success": True}

# Get task results (user-scoped)
@app.get("/api/agents/tasks/{task_id}/results")
async def get_task_results(task_id: str, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Get results for a task (user-scoped access)"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if engine is None:
        raise HTTPException(status_code=503, detail="Database not configured")

    uid = u.get("id")
    is_admin = u.get("role") == "admin"

    async with engine.connect() as conn:
        # First verify user has access to this task
        res = await conn.execute(
            select(agent_tasks.c.user_id)
            .where(agent_tasks.c.id == task_id)
            .limit(1)
        )
        task = res.first()
        if not task:
            raise HTTPException(status_code=404, detail="Task not found")

        # Check permissions
        if not is_admin and task.user_id != uid:
            raise HTTPException(status_code=403, detail="Access denied")

        # Get results
        res = await conn.execute(
            select(agent_results)
            .where(agent_results.c.task_id == task_id)
            .order_by(agent_results.c.created_at.desc())
        )

        results = []
        for row in res.fetchall():
            result_dict = dict(row._mapping)
            # Parse JSON fields
            try:
                result_dict["content"] = json.loads(result_dict["content"] or "{}")
                result_dict["metadata"] = json.loads(result_dict["metadata"] or "{}")
            except (json.JSONDecodeError, TypeError):
                result_dict["content"] = {}
                result_dict["metadata"] = {}
            results.append(result_dict)

        return results

# ------------------------- TTS Endpoints -------------------------

@app.get("/api/tts/settings")
async def get_tts_settings(request: Request, user_id: Optional[str] = Query(None), db: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Get TTS settings for the user, including provider and saved voice."""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")

    uid = user_id or u.get("id")
    if uid != u.get("id") and u.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Forbidden")

    provider = "fishspeech"
    voice_id = "glados"

    # Pull from chat_settings if present
    if db is not None:
        try:
            async with db.connect() as conn:
                res = await conn.execute(select(t_chat_settings).where(t_chat_settings.c.user_id == uid).limit(1))
                row = res.first()
                if row:
                    m = row._mapping
                    provider = (m.get("tts_provider") or provider)
                    voice_id = (m.get("tts_voice_id") or voice_id)
        except Exception as e:
            logger.exception("get_tts_settings DB error: %s", e)

    # Providers list: mark fishspeech available if TTS proxy is healthy
    tts_url = os.getenv("FISHSPEECH_URL", "http://localhost:8081").rstrip("/")
    available = False
    try:
        async with httpx.AsyncClient(timeout=3) as client:
            r = await client.get(f"{tts_url}/health")
            available = (r.status_code == 200)
    except Exception:
        available = False

    providers = [
        {"id": "fishspeech", "name": "OpenAudio S1 Mini (FishSpeech)", "available": bool(available)}
    ]

    return {
        "apiKey": "",
        "voiceId": voice_id,
        "fishSpeechApiKey": "",
        "ttsProvider": provider,
        "providers": providers,
    }

@app.post("/api/tts/settings")
async def save_tts_settings(request: Request, body: dict, db: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Save TTS settings for the user (provider, voiceId)."""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")

    uid = (body or {}).get("userId") or u.get("id")
    if uid != u.get("id") and u.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Forbidden")

    provider = (body or {}).get("ttsProvider") or "fishspeech"
    voice_id = (body or {}).get("voiceId") or "glados"

    # Update cache
    s = _chat_settings_store.get(uid) or _default_chat_settings(uid)
    s["ttsProvider"] = provider
    s["ttsVoiceId"] = voice_id
    _chat_settings_store[uid] = s

    # Persist to DB
    if db is not None:
        try:
            async with db.begin() as conn:
                res = await conn.execute(select(t_chat_settings).where(t_chat_settings.c.user_id == uid).limit(1))
                exists = res.first() is not None
                values = {"tts_provider": provider, "tts_voice_id": voice_id}
                if exists:
                    await conn.execute(t_chat_settings.update().where(t_chat_settings.c.user_id == uid).values(**values))
                else:
                    await conn.execute(t_chat_settings.insert().values(user_id=uid, **values))
        except Exception as e:
            logger.exception("save_tts_settings DB error: %s", e)

    return {"success": True}

@app.get("/api/tts/voices")
async def get_tts_voices(request: Request, provider: str = Query(...)):
    """Get available voices from OpenAudio S1 Mini proxy (FishSpeech)."""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")

    # For now we use the FishSpeech proxy regardless of provider
    tts_url = os.getenv("FISHSPEECH_URL", "http://localhost:8081").rstrip("/")
    voices: List[str] = []
    try:
        async with httpx.AsyncClient(timeout=5) as client:
            r = await client.get(f"{tts_url}/voices")
            if r.status_code == 200:
                data = r.json() or {}
                voices = data.get("voices") or []
    except Exception as e:
        logger.exception("get_tts_voices error: %s", e)

    return {"voices": voices}

@app.get("/api/tts/health")
async def tts_health(request: Request):
    """Proxy TTS proxy /health so the frontend can display GPU/CPU status.
    Always routes through the backend to keep the frontend free of direct service calls.
    """
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")

    tts_url = os.getenv("FISHSPEECH_URL", "http://localhost:8081").rstrip("/")
    try:
        async with httpx.AsyncClient(timeout=5) as client:
            r = await client.get(f"{tts_url}/health")
            if r.status_code == 200:
                return r.json()
    except Exception as e:
        logger.exception("tts_health error: %s", e)
    return {"status": "degraded"}


@app.post("/api/tts/synthesize")
async def tts_synthesize(request: Request, body: Optional[dict] = None):
    """Synthesize speech using OpenAudio S1 Mini proxy.
    - If voiceId is provided, use /tts/clone (reference voice)
    - Otherwise, use /tts (default voice)
    Falls back to a short WAV tone if the proxy is unavailable.
    """
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")

    payload = body or {}
    text = (payload.get("text") or payload.get("message") or "").strip()
    provider = (payload.get("provider") or "fishspeech").lower()
    voice_id = payload.get("voiceId") or payload.get("voice")

    if not text:
        raise HTTPException(status_code=400, detail="text is required")

    tts_url = os.getenv("FISHSPEECH_URL", "http://localhost:8081").rstrip("/")
    try:
        async with httpx.AsyncClient(timeout=90) as client:
            if provider.startswith("fish") and voice_id:
                r = await client.post(f"{tts_url}/tts/clone", data={"text": text, "voice_name": voice_id})
            else:
                r = await client.post(f"{tts_url}/tts", data={"text": text})
            if r.status_code == 200 and r.content:
                ctype = r.headers.get("content-type", "audio/wav")
                return Response(content=r.content, media_type=ctype)
    except Exception as e:
        logger.exception("tts_synthesize proxy error: %s", e)

    # Fallback: generate a 0.25s 440Hz sine wave WAV so the UI can complete successfully.
    sample_rate = 22050
    duration_s = 0.25
    freq = 440.0
    num_samples = int(sample_rate * duration_s)
    wav = io.BytesIO()
    byte_rate = sample_rate * 2
    block_align = 2
    data_bytes = num_samples * 2
    wav.write(b"RIFF")
    wav.write(struct.pack('<I', 36 + data_bytes))
    wav.write(b"WAVEfmt ")
    wav.write(struct.pack('<IHHIIHH', 16, 1, 1, sample_rate, byte_rate, block_align, 16))
    wav.write(b"data")
    wav.write(struct.pack('<I', data_bytes))
    for n in range(num_samples):
        t = n / sample_rate
        val = int(32767 * 0.2 * math.sin(2 * math.pi * freq * t))
        wav.write(struct.pack('<h', val))
    audio_bytes = wav.getvalue()
    return Response(content=audio_bytes, media_type="audio/wav")

# ------------------------- Memory Endpoints -------------------------

@app.get("/api/memory/{user_id}")
async def get_memory(user_id: str, request: Request):
    """Get memory for user"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if user_id != u.get("id") and u.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Access denied")
    return {"memories": [], "count": 0}

@app.get("/api/memorysyncstatus/status/{user_id}")
async def get_memory_sync_status(user_id: str, request: Request):
    """Get memory sync status"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    if user_id != u.get("id") and u.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Access denied")
    return {"status": "synced", "lastSync": "2024-01-01T00:00:00Z"}

# ------------------------- Upload Endpoints -------------------------

@app.get("/api/upload/documents")
async def get_uploaded_documents(request: Request):
    """Get uploaded documents"""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    return {"documents": []}

_ALLOWED_UPLOAD_TYPES = {
    "application/pdf", "text/plain", "text/markdown",
    "application/json", "text/csv",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    "application/msword",
}
_MAX_UPLOAD_BYTES = int(os.getenv("MAX_UPLOAD_BYTES", str(20 * 1024 * 1024)))  # 20 MB


@app.post("/api/upload/documents")
async def upload_document(request: Request, file: UploadFile = File(...)):
    """Upload a document with type and size validation."""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")

    content_type = file.content_type or ""
    if content_type not in _ALLOWED_UPLOAD_TYPES:
        raise HTTPException(
            status_code=400,
            detail=f"File type '{content_type}' is not allowed. Allowed: {', '.join(sorted(_ALLOWED_UPLOAD_TYPES))}",
        )

    uid = u.get("id")
    safe_name = os.path.basename(file.filename or "upload")
    dest_dir = get_user_upload_dir(uid)
    dest_path = os.path.join(dest_dir, f"{uuid.uuid4()}_{safe_name}")

    # Stream upload to disk with size guard to avoid buffering the entire file in memory
    total = 0
    try:
        with open(dest_path, "wb") as fh:
            while True:
                chunk = await file.read(65536)
                if not chunk:
                    break
                new_total = total + len(chunk)
                if new_total > _MAX_UPLOAD_BYTES:
                    # Remove partially written file before raising
                    fh.close()
                    try:
                        os.remove(dest_path)
                    except OSError:
                        pass
                    raise HTTPException(
                        status_code=413,
                        detail=f"File too large (max {_MAX_UPLOAD_BYTES // (1024 * 1024)} MB).",
                    )
                fh.write(chunk)
                total = new_total
    except HTTPException:
        # Propagate size errors as-is
        raise
    logger.info("Document uploaded by user %s: %s (%d bytes)", uid, safe_name, total)
    return {"filename": safe_name, "size": total, "success": True}

# ------------------------- Settings Endpoints -------------------------

@app.get("/api/settings/connections")
async def get_connection_settings(request: Request, userId: Optional[str] = Query(None), user_id: Optional[str] = Query(None), engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Get per-user connection settings. User is derived from auth; optional userId/user_id allowed for admin."""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    uid = u.get("id")
    requested = userId or user_id or uid
    if requested != uid and u.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Forbidden")

    defaults = {
        "OpenAiApiKey": None,
        "ClaudeApiKey": None,
        "ClaudeApiUrl": None,
        "OllamaApiUrl": "http://localhost:11434",
        "LmStudioApiUrl": "http://localhost:1234",
        "EnableStreaming": True,
    }

    _evict_stale_cache_entries()
    # Prefer cache; fall back to DB if available
    data = _connections_store.get(requested)
    if not data and engine is not None:
        try:
            async with engine.connect() as conn:
                res = await conn.execute(select(t_conn_settings).where(t_conn_settings.c.user_id == requested).limit(1))
                row = res.first()
                if row:
                    data = dict(row._mapping)
                    _connections_store[requested] = data
                    _connections_ts[requested] = time.time()
        except Exception:
            data = None

    result = {**defaults, **(data or {})}
    # Decrypt API key fields before returning
    for field in _API_KEY_FIELDS:
        if result.get(field):
            result[field] = _decrypt_api_key(result[field])
    return result

@app.put("/api/settings/connections")
async def put_connection_settings(body: dict, request: Request, engine: Optional[AsyncEngine] = Depends(get_engine_dep)):
    """Upsert per-user connection settings from authenticated user."""
    u = getattr(request.state, "user", None)
    if not u:
        raise HTTPException(status_code=401, detail="Authentication required")
    uid = u.get("id")
    allowed = {"OpenAiApiKey", "ClaudeApiKey", "ClaudeApiUrl", "OllamaApiUrl", "LmStudioApiUrl", "EnableStreaming", "TtsGpu", "SttGpu"}
    values = {k: v for k, v in (body or {}).items() if k in allowed}

    # Validate SSRF-sensitive URL fields before storing
    for url_field in ("OllamaApiUrl", "LmStudioApiUrl", "ClaudeApiUrl"):
        url_val = values.get(url_field)
        if url_val and not await _validate_url_for_ssrf(url_val):
            raise HTTPException(status_code=400, detail=f"Invalid or unsafe URL for {url_field}.")

    # Encrypt API key fields before storing
    db_values = dict(values)
    for field in _API_KEY_FIELDS:
        if field in db_values and db_values[field]:
            db_values[field] = _encrypt_api_key(db_values[field])

    # Update cache with unencrypted values for in-memory use
    existing = dict(_connections_store.get(uid, {}))
    existing.update(values)
    existing["user_id"] = uid
    _connections_store[uid] = existing
    _connections_ts[uid] = time.time()

    if engine is not None:
        async with engine.begin() as conn:
            res = await conn.execute(t_conn_settings.update().where(t_conn_settings.c.user_id == uid).values(**db_values))
            if getattr(res, "rowcount", 0) == 0:
                await conn.execute(t_conn_settings.insert().values(user_id=uid, **db_values))
    return {"success": True}

# This ends the main.py file - all imports and endpoints are complete
