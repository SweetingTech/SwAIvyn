# -*- coding: utf-8 -*-
# Merged and fixed: remove conflict markers, restore missing imports, add agent status store and endpoints,
# and keep clean LLM settings handlers.

import os
import asyncio
import sys
import json
import ipaddress
import urllib.parse
import uuid
from typing import Optional, Dict, Any, List, Mapping, Set

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
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel
from temporalio.client import Client
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from sqlalchemy.ext.asyncio import AsyncEngine
from sqlalchemy import select, text
from datetime import timedelta, datetime as _dt

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
    agent_status as t_agent_status,
    agent_events as t_agent_events,
)

# ------------------------- Config -------------------------

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

# ------------------------- Agent status helpers -------------------------

_AGENT_STATUS_VALUES: Set[str] = {
    "pending",
    "working",
    "running",
    "paused",
    "completed",
    "failed",
    "stopped",
    "idle",
    "cancelled",
    "error",
    "unknown",
}
_AGENT_STATUS_ALIASES = {
    "active": "working",
    "in-progress": "working",
    "in_progress": "working",
    "processing": "working",
    "queued": "pending",
    "waiting": "pending",
    "stopping": "paused",
    "canceled": "cancelled",
}
_ACTIVE_AGENT_STATUSES: Set[str] = {"running", "working"}
_TERMINAL_AGENT_STATUSES: Set[str] = {"completed", "failed", "stopped", "cancelled", "error"}

def _normalize_agent_status(value: Optional[str], default: str = "pending") -> str:
    base = default
    if value is not None:
        candidate = str(value).strip().lower()
        if candidate:
            base = candidate
    base = _AGENT_STATUS_ALIASES.get(base, base)
    if base not in _AGENT_STATUS_VALUES:
        return default
    return base

def _now_iso() -> str:
    return _dt.utcnow().isoformat() + "Z"

def _parse_meta(value: Any) -> Dict[str, Any]:
    if value is None:
        return {}
    if isinstance(value, dict):
        return value
    if isinstance(value, str):
        try:
            parsed = json.loads(value)
            if isinstance(parsed, dict):
                return parsed
        except Exception:
            return {}
    return {}

def _json_dumps_or_none(value: Any) -> Optional[str]:
    if value is None:
        return None
    if isinstance(value, str):
        return value
    try:
        return json.dumps(value)
    except (TypeError, ValueError):
        return None

def _serialize_agent_row(row_map: Mapping[str, Any]) -> Dict[str, Any]:
    return {
        "id": row_map.get("id"),
        "name": row_map.get("name") or row_map.get("id"),
        "status": _normalize_agent_status(row_map.get("status"), "pending"),
        "userId": row_map.get("user_id"),
        "meta": _parse_meta(row_map.get("meta")),
        "startedAt": row_map.get("started_at"),
        "finishedAt": row_map.get("finished_at"),
        "updatedAt": row_map.get("updated_at"),
    }

def _serialize_event_row(row_map: Mapping[str, Any]) -> Dict[str, Any]:
    return {
        "id": row_map.get("id"),
        "agentId": row_map.get("agent_id"),
        "timestamp": row_map.get("timestamp"),
        "status": _normalize_agent_status(row_map.get("status"), "pending"),
        "message": row_map.get("message") or "",
        "meta": _parse_meta(row_map.get("meta")),
    }

def _append_event_in_memory(agent_id: str, event: Dict[str, Any]) -> None:
    ag = _agents_store.setdefault(agent_id, {"id": agent_id})
    events: List[Dict[str, Any]] = ag.setdefault("events", [])  # type: ignore[assignment]
    events.append(event)
    if len(events) > 200:
        del events[:-200]

async def _fetch_agent(agent_id: str) -> Optional[Dict[str, Any]]:
    if _engine is not None:
        try:
            async with _engine.connect() as conn:
                res = await conn.execute(
                    select(t_agent_status).where(t_agent_status.c.id == agent_id).limit(1)
                )
                row = res.first()
                if row:
                    return _serialize_agent_row(row._mapping)
        except Exception as exc:
            print(f"Agent fetch failed: {exc}", file=sys.stderr, flush=True)

    ag = _agents_store.get(agent_id)
    if not ag:
        return None
    copy = {k: v for k, v in ag.items() if k != "events"}
    copy.setdefault("name", copy.get("id") or agent_id)
    copy["status"] = _normalize_agent_status(copy.get("status"), "pending")
    copy["meta"] = _parse_meta(copy.get("meta"))
    return copy

async def _fetch_agent_events(agent_id: str) -> List[Dict[str, Any]]:
    if _engine is not None:
        try:
            async with _engine.connect() as conn:
                res = await conn.execute(
                    select(t_agent_events)
                    .where(t_agent_events.c.agent_id == agent_id)
                    .order_by(t_agent_events.c.timestamp)
                )
                return [_serialize_event_row(row._mapping) for row in res.fetchall()]
        except Exception as exc:
            print(f"Agent events fetch failed: {exc}", file=sys.stderr, flush=True)
            return []

    events = _agents_store.get(agent_id, {}).get("events", [])
    return [dict(ev) for ev in events]

async def _persist_agent(
    agent_id: str,
    data: Dict[str, Any],
    *,
    event_status: Optional[str] = None,
    event_message: Optional[str] = None,
    event_meta: Any = None,
    existing: Optional[Dict[str, Any]] = None,
) -> Dict[str, Any]:
    if existing is None:
        existing = await _fetch_agent(agent_id)

    now = data.get("updatedAt") or _now_iso()
    merged: Dict[str, Any] = existing.copy() if existing else {}
    merged.update({"id": agent_id})
    for key in ("name", "status", "userId", "meta", "startedAt", "finishedAt"):
        if key in data:
            merged[key] = data[key]
    merged.setdefault("name", agent_id)
    merged.setdefault("status", existing.get("status") if existing else "pending")
    merged["status"] = _normalize_agent_status(merged.get("status"), "pending")
    merged["meta"] = _parse_meta(merged.get("meta"))
    merged["updatedAt"] = now

    log_event = event_status is not None or event_message or event_meta is not None
    effective_event_status = _normalize_agent_status(event_status or merged.get("status"), merged.get("status"))
    event_payload = None
    if log_event:
        event_payload = {
            "id": str(uuid.uuid4()),
            "agentId": agent_id,
            "timestamp": now,
            "status": effective_event_status,
            "message": event_message or "",
            "meta": _parse_meta(event_meta) if event_meta is not None else {},
        }

    if _engine is not None:
        db_values = {
            "name": merged.get("name"),
            "status": merged.get("status"),
            "user_id": merged.get("userId"),
            "meta": _json_dumps_or_none(merged.get("meta")),
            "started_at": merged.get("startedAt"),
            "finished_at": merged.get("finishedAt"),
            "updated_at": merged.get("updatedAt"),
        }
        try:
            async with _engine.begin() as conn:
                res = await conn.execute(
                    select(t_agent_status.c.id).where(t_agent_status.c.id == agent_id).limit(1)
                )
                if res.first():
                    await conn.execute(
                        t_agent_status.update().where(t_agent_status.c.id == agent_id).values(**db_values)
                    )
                else:
                    await conn.execute(t_agent_status.insert().values(id=agent_id, **db_values))

                if event_payload is not None:
                    await conn.execute(
                        t_agent_events.insert().values(
                            id=event_payload["id"],
                            agent_id=agent_id,
                            timestamp=now,
                            status=event_payload["status"],
                            message=event_payload["message"],
                            meta=_json_dumps_or_none(event_payload["meta"]) if event_payload.get("meta") else None,
                        )
                    )
        except Exception as exc:
            print(f"Agent persistence failed: {exc}", file=sys.stderr, flush=True)

        updated = await _fetch_agent(agent_id)
        if updated:
            merged = updated

    cached = {k: v for k, v in merged.items() if k != "events"}
    _agents_store[agent_id] = cached
    if event_payload is not None:
        _append_event_in_memory(agent_id, event_payload)

    return merged

# ------------------------- Agent endpoints -------------------------

@app.get("/api/agents")
async def agents_list(current=Depends(current_user_dep)):
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")
    uid = current.get("id")
    viewer_is_admin = current.get("role") == "admin"
    agents: List[Dict[str, Any]] = []

    if _engine is not None:
        try:
            async with _engine.connect() as conn:
                stmt = select(t_agent_status)
                if not viewer_is_admin:
                    stmt = stmt.where(t_agent_status.c.user_id == uid)
                stmt = stmt.order_by(t_agent_status.c.updated_at.desc())
                res = await conn.execute(stmt)
                agents = [_serialize_agent_row(row._mapping) for row in res.fetchall()]
        except Exception as exc:
            print(f"Agents list query failed: {exc}", file=sys.stderr, flush=True)
            agents = []
    else:
        for ag in _agents_store.values():
            if viewer_is_admin or ag.get("userId") == uid:
                copy = {k: v for k, v in ag.items() if k != "events"}
                copy["status"] = _normalize_agent_status(copy.get("status"), "pending")
                copy["meta"] = _parse_meta(copy.get("meta"))
                agents.append(copy)
        agents.sort(key=lambda item: item.get("updatedAt") or "", reverse=True)

    return agents

@app.get("/api/agents/{agent_id}")
async def agents_get(agent_id: str, current=Depends(current_user_dep)):
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")

    agent = await _fetch_agent(agent_id)
    if not agent:
        raise HTTPException(status_code=404, detail="Agent not found")

    viewer_is_admin = current.get("role") == "admin"
    uid = current.get("id")
    if not viewer_is_admin and agent.get("userId") not in {uid, None}:
        raise HTTPException(status_code=403, detail="Forbidden")

    events = await _fetch_agent_events(agent_id)
    detail = dict(agent)
    detail["events"] = events
    return detail

@app.post("/api/agents/{agent_id}/start")
async def agents_start(agent_id: str, body: dict = {}, current=Depends(current_user_dep)):
    existing = await _fetch_agent(agent_id)
    uid = (current or {}).get("id") or body.get("userId") or (existing or {}).get("userId") or "default"
    status = _normalize_agent_status(body.get("status"), "working")
    started_at = body.get("startedAt") or (existing or {}).get("startedAt") or _now_iso()
    updates = {
        "name": body.get("name") or (existing or {}).get("name") or agent_id,
        "status": status,
        "userId": uid,
        "startedAt": started_at,
        "finishedAt": None,
        "meta": body.get("meta") if "meta" in body else (existing or {}).get("meta"),
    }
    agent = await _persist_agent(
        agent_id,
        updates,
        event_status=status,
        event_message=body.get("message") or "Agent started",
        event_meta=body.get("eventMeta"),
        existing=existing,
    )
    return {"success": True, "agent": agent}

@app.post("/api/agents/{agent_id}/stop")
async def agents_stop(agent_id: str, body: dict = {}, current=Depends(current_user_dep)):
    existing = await _fetch_agent(agent_id)
    status = _normalize_agent_status(body.get("status"), "completed")
    finished_at = body.get("finishedAt") if "finishedAt" in body else _now_iso()
    updates: Dict[str, Any] = {
        "status": status,
        "finishedAt": finished_at,
    }
    if "meta" in body:
        updates["meta"] = body.get("meta")

    agent = await _persist_agent(
        agent_id,
        updates,
        event_status=status,
        event_message=body.get("message") or f"Agent marked as {status}",
        event_meta=body.get("eventMeta"),
        existing=existing,
    )
    return {"success": True, "agent": agent}

@app.patch("/api/agents/{agent_id}")
async def agents_update(agent_id: str, body: dict = {}, current=Depends(current_user_dep)):
    if not current:
        raise HTTPException(status_code=401, detail="Authentication required")

    existing = await _fetch_agent(agent_id)
    if not existing:
        raise HTTPException(status_code=404, detail="Agent not found")

    viewer_is_admin = current.get("role") == "admin"
    uid = current.get("id")
    requested_user = body.get("userId")
    if not viewer_is_admin and existing.get("userId") not in {uid, None} and requested_user not in {uid, None}:
        raise HTTPException(status_code=403, detail="Forbidden")

    updates: Dict[str, Any] = {}
    if "name" in body:
        updates["name"] = body.get("name")
    if "userId" in body:
        updates["userId"] = requested_user

    status_updated = False
    if "status" in body:
        new_status = _normalize_agent_status(body.get("status"), existing.get("status") or "pending")
        updates["status"] = new_status
        status_updated = True
        if new_status in _ACTIVE_AGENT_STATUSES and not existing.get("startedAt") and "startedAt" not in body:
            updates["startedAt"] = _now_iso()
        if new_status in _TERMINAL_AGENT_STATUSES and "finishedAt" not in body:
            updates.setdefault("finishedAt", _now_iso())

    if "startedAt" in body:
        updates["startedAt"] = body.get("startedAt")
    if "finishedAt" in body:
        updates["finishedAt"] = body.get("finishedAt")
    if body.get("clearFinished") or body.get("resetFinished"):
        updates["finishedAt"] = None
    if "meta" in body:
        updates["meta"] = body.get("meta")

    event_status = updates.get("status") if status_updated else existing.get("status")
    message = body.get("message")
    if not message:
        message = f"Status set to {event_status}" if status_updated else "Agent updated"

    if not updates and not (message or body.get("eventMeta") is not None):
        return {"success": True, "agent": existing}

    agent = await _persist_agent(
        agent_id,
        updates,
        event_status=event_status,
        event_message=message,
        event_meta=body.get("eventMeta"),
        existing=existing,
    )
    return {"success": True, "agent": agent}

@app.delete("/api/agents/{agent_id}")
async def agents_delete(agent_id: str, current=Depends(current_user_dep)):
    agent = await _fetch_agent(agent_id)
    viewer_is_admin = (current or {}).get("role") == "admin"
    uid = (current or {}).get("id")
    if agent and not viewer_is_admin and agent.get("userId") not in {uid, None}:
        raise HTTPException(status_code=403, detail="Forbidden")

    if _engine is not None:
        try:
            async with _engine.begin() as conn:
                await conn.execute(t_agent_events.delete().where(t_agent_events.c.agent_id == agent_id))
                await conn.execute(t_agent_status.delete().where(t_agent_status.c.id == agent_id))
        except Exception as exc:
            print(f"Agent delete failed: {exc}", file=sys.stderr, flush=True)

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
