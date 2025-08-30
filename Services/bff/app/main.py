import os
import asyncio
import sys
from typing import Optional

import httpx
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from temporalio.client import Client
import httpx
from fastapi import Depends
from fastapi.security import HTTPBearer, HTTPAuthorizationCredentials
from sqlalchemy.ext.asyncio import AsyncEngine
from sqlalchemy import select
from datetime import timedelta
from pydantic import BaseModel
from .auth import create_access_token, verify_password, get_current_user, require_admin
from .models import users


TEMPORAL_HOST = os.getenv("TEMPORAL_HOST", "temporal:7233")
OLLAMA_HOST = os.getenv("OLLAMA_HOST", "http://host.docker.internal:11434")
LMSTUDIO_HOST = os.getenv("LMSTUDIO_HOST", "http://host.docker.internal:1234")
DATABASE_URL = os.getenv("DATABASE_URL")


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
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5173", "http://127.0.0.1:5173"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"]
)
_temporal_client: Optional[Client] = None
_engine: Optional[AsyncEngine] = None


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


@app.get("/api/llm/health")
async def llm_health():
    status = {"ollama": None, "lmstudio": None}
    async with httpx.AsyncClient(timeout=3) as client:
        # Ollama: list tags
        try:
            r = await client.get(f"{OLLAMA_HOST}/api/tags")
            status["ollama"] = {"ok": r.status_code == 200}
        except Exception as e:
            status["ollama"] = {"ok": False, "error": str(e)}

        # LM Studio: OpenAI-compatible models
        try:
            r = await client.get(f"{LMSTUDIO_HOST}/v1/models")
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
    # user can view self or admin can view anyone
    if not current or (current.get("id") != user_id and current.get("role") != "admin"):
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
    return {"engine": "ollama", "model": os.getenv("LLM_MODEL", "llama3")}


@app.get("/api/tts/settings")
async def get_tts_settings(userId: Optional[str] = None):
    # Minimal stub for TTS settings
    return {"provider": "fishspeech", "voiceId": "default"}


@app.post("/api/tts/settings")
async def set_tts_settings(body: dict):
    # Accept but do not persist (stub)
    return {"success": True}


@app.get("/api/settings/DefaultCharacterId")
async def get_default_character_id(userId: Optional[str] = None):
    return {"defaultCharacterId": "default"}


@app.get("/api/character/user/{user_id}")
async def get_user_characters(user_id: str):
    # Return an empty list for now; frontend will handle it gracefully
    return []


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


@app.get("/api/folder/user/{user_id}")
async def list_folders(user_id: str):
    # Stub empty folder list
    return []


@app.get("/api/conversation/user/{user_id}")
async def list_conversations(user_id: str):
    # Stub empty conversation list
    return []


@app.get("/api/dashboard/status")
async def dashboard_status():
    # Minimal status object
    return {"status": "ok", "services": {"bff": True, "temporal": True}}


@app.get("/api/memorysyncstatus/status/{user_id}")
async def memorysync_status(user_id: str):
    return {"status": "idle", "lastSync": None}


@app.post("/api/memorysyncstatus/repair/{user_id}")
async def memorysync_repair(user_id: str):
    return {"status": "queued"}


@app.post("/api/memorysyncstatus/full/{user_id}")
async def memorysync_full(user_id: str):
    return {"status": "queued"}


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
async def list_llm_models(engine: str = "ollama"):
    try:
        async with httpx.AsyncClient(timeout=5) as client:
            if engine.lower() == "ollama":
                r = await client.get(f"{OLLAMA_HOST}/api/tags")
                r.raise_for_status()
                data = r.json()
                models = [m.get("name") for m in data.get("models", []) if m.get("name")]
                return {"engine": "ollama", "models": models}
            elif engine.lower() == "lmstudio":
                r = await client.get(f"{LMSTUDIO_HOST}/v1/models")
                r.raise_for_status()
                data = r.json()
                models = [m.get("id") for m in data.get("data", []) if m.get("id")]
                return {"engine": "lmstudio", "models": models}
    except Exception as e:
        raise HTTPException(status_code=502, detail=f"Model listing failed: {e}")
    return {"engine": engine, "models": []}


# Back-compat aliases for LLm routes used by the frontend
@app.get("/api/llm/ollama/models")
async def list_ollama_models():
    data = await list_llm_models(engine="ollama")
    return data


@app.get("/api/llm/lmstudio/models")
async def list_lmstudio_models(userId: Optional[str] = None):
    data = await list_llm_models(engine="lmstudio")
    return data


@app.get("/api/llm/lmstudio/model")
async def get_lmstudio_selected_model(userId: Optional[str] = None):
    # Return current model from env/stub
    return {"model": os.getenv("LLM_MODEL", "llama3")}


@app.get("/api/chat/settings/{user_id}")
async def get_chat_settings(user_id: str):
    # Minimal compatible payload
    return {
        "llmEngine": "ollama",
        "llmModel": os.getenv("LLM_MODEL", "llama3"),
        "ttsProvider": "fishspeech",
        "ttsVoiceId": "",
    }


@app.put("/api/chat/settings/{user_id}")
async def update_chat_settings(user_id: str, payload: dict):
    # Accept and acknowledge; persistence can be added later
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
