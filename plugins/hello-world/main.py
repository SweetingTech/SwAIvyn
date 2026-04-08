"""Hello World reference plugin for SwAIvyn.

This is the minimal server that satisfies the SwAIvyn plugin contract
(docs/plugin-sdk.md).  Run with:

    uvicorn main:app --port 8080
"""
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Any, Dict, Optional

app = FastAPI(title="Hello World Plugin", version="1.0.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["GET", "POST"],
    allow_headers=["*"],
)


# ── Health ──────────────────────────────────────────────────────────────────

@app.get("/health")
async def health():
    """SwAIvyn polls this endpoint to determine plugin health."""
    return {"status": "ok", "plugin": "hello-world", "version": "1.0.0"}


# ── Tool invocation ─────────────────────────────────────────────────────────

class InvokeRequest(BaseModel):
    input: Dict[str, Any] = {}
    context: Optional[Dict[str, Any]] = None


class InvokeResponse(BaseModel):
    output: Dict[str, Any]


@app.post("/invoke", response_model=InvokeResponse)
async def invoke(body: InvokeRequest):
    """Execute the plugin's tool-use capability.

    Accepts any input dict and returns a greeting incorporating the
    optional ``name`` field, demonstrating round-trip data flow.
    """
    name = body.input.get("name", "World")
    return InvokeResponse(
        output={
            "greeting": f"Hello, {name}! 👋 This response was produced by the SwAIvyn Hello World reference plugin.",
            "echo": body.input,
        }
    )


# ── Plugin metadata ─────────────────────────────────────────────────────────

@app.get("/info")
async def info():
    """Return plugin metadata (mirrors plugin.json)."""
    return {
        "manifest_version": "1",
        "id": "hello-world",
        "name": "Hello World",
        "version": "1.0.0",
        "description": "Reference plugin demonstrating the SwAIvyn plugin lifecycle.",
        "author": "SwAIvyn Team",
        "capabilities": ["tool-use"],
    }
