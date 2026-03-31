from __future__ import annotations

from dataclasses import dataclass
from datetime import timedelta
from typing import Optional, Dict, Any

from temporalio import workflow


@dataclass
class ChatRequest:
    message: str
    user_id: Optional[str] = None
    session_id: Optional[str] = None
    engine: Optional[str] = None
    model: Optional[str] = None


@workflow.defn(name="ReplyWorkflow")
class ReplyWorkflow:
    @workflow.run
    async def run(self, req: Dict[str, Any]) -> Dict[str, Any]:
        # Filter to only valid ChatRequest fields
        chat_fields = {k: v for k, v in req.items() if k in ['message', 'user_id', 'session_id', 'engine', 'model']}
        cr = ChatRequest(**chat_fields)

        # Pass through routing and connection details to activity
        reply = await workflow.execute_activity(
            "generate_reply",
            {
                "message": cr.message,
                "user_id": cr.user_id,
                "engine": cr.engine,
                "model": cr.model,
                "conn": req.get("conn") if isinstance(req, dict) else None,
            },
            schedule_to_close_timeout=timedelta(seconds=30),
        )

        tts_url = await workflow.execute_activity(
            "synthesize_tts",
            {"text": reply.get("reply_text", "")},
            schedule_to_close_timeout=timedelta(seconds=30),
        )

        # Fire-and-forget for memory operations (best-effort)
        workflow.start_activity(
            "upsert_vector_memory",
            {"text": cr.message, "reply": reply.get("reply_text")},
            schedule_to_close_timeout=timedelta(seconds=30),
        )
        workflow.start_activity(
            "update_graph",
            {"text": cr.message, "reply": reply.get("reply_text")},
            schedule_to_close_timeout=timedelta(seconds=30),
        )

        return {"reply_text": reply.get("reply_text"), "tts_url": tts_url}


# Engine-specific workflows for explicit routing

@workflow.defn(name="ReplyWorkflow_Ollama")
class ReplyWorkflowOllama:
    @workflow.run
    async def run(self, req: Dict[str, Any]) -> Dict[str, Any]:
        chat_fields = {k: v for k, v in req.items() if k in ['message', 'user_id', 'session_id', 'engine', 'model']}
        cr = ChatRequest(**chat_fields)
        reply = await workflow.execute_activity(
            "generate_reply",
            {
                "message": cr.message,
                "user_id": cr.user_id,
                "engine": "ollama",
                "model": cr.model,
                "conn": req.get("conn") if isinstance(req, dict) else None,
            },
            schedule_to_close_timeout=timedelta(seconds=30),
        )
        return {"reply_text": reply.get("reply_text"), "tts_url": None}


@workflow.defn(name="ReplyWorkflow_LMStudio")
class ReplyWorkflowLMStudio:
    @workflow.run
    async def run(self, req: Dict[str, Any]) -> Dict[str, Any]:
        # Filter to only valid ChatRequest fields
        chat_fields = {k: v for k, v in req.items() if k in ['message', 'user_id', 'session_id', 'engine', 'model']}
        cr = ChatRequest(**chat_fields)
        reply = await workflow.execute_activity(
            "generate_reply",
            {
                "message": cr.message,
                "user_id": cr.user_id,
                "engine": "lmstudio",
                "model": cr.model,
                "conn": req.get("conn") if isinstance(req, dict) else None,
            },
            schedule_to_close_timeout=timedelta(seconds=30),
        )
        return {"reply_text": reply.get("reply_text"), "tts_url": None}


@workflow.defn(name="ReplyWorkflow_OpenAI")
class ReplyWorkflowOpenAI:
    @workflow.run
    async def run(self, req: Dict[str, Any]) -> Dict[str, Any]:
        chat_fields = {k: v for k, v in req.items() if k in ['message', 'user_id', 'session_id', 'engine', 'model']}
        cr = ChatRequest(**chat_fields)
        reply = await workflow.execute_activity(
            "generate_reply",
            {
                "message": cr.message,
                "user_id": cr.user_id,
                "engine": "openai",
                "model": cr.model,
                "conn": req.get("conn") if isinstance(req, dict) else None,
            },
            schedule_to_close_timeout=timedelta(seconds=30),
        )
        return {"reply_text": reply.get("reply_text"), "tts_url": None}


@workflow.defn(name="ReplyWorkflow_Claude")
class ReplyWorkflowClaude:
    @workflow.run
    async def run(self, req: Dict[str, Any]) -> Dict[str, Any]:
        chat_fields = {k: v for k, v in req.items() if k in ['message', 'user_id', 'session_id', 'engine', 'model']}
        cr = ChatRequest(**chat_fields)
        reply = await workflow.execute_activity(
            "generate_reply",
            {
                "message": cr.message,
                "user_id": cr.user_id,
                "engine": "claude",
                "model": cr.model,
                "conn": req.get("conn") if isinstance(req, dict) else None,
            },
            schedule_to_close_timeout=timedelta(seconds=30),
        )
        return {"reply_text": reply.get("reply_text"), "tts_url": None}


@workflow.defn(name="ReplyWorkflow_VLLM")
class ReplyWorkflowVLLM:
    @workflow.run
    async def run(self, req: Dict[str, Any]) -> Dict[str, Any]:
        chat_fields = {k: v for k, v in req.items() if k in ['message', 'user_id', 'session_id', 'engine', 'model']}
        cr = ChatRequest(**chat_fields)
        reply = await workflow.execute_activity(
            "generate_reply",
            {
                "message": cr.message,
                "user_id": cr.user_id,
                "engine": "vllm",
                "model": cr.model,
                "conn": req.get("conn") if isinstance(req, dict) else None,
            },
            schedule_to_close_timeout=timedelta(seconds=30),
        )
        return {"reply_text": reply.get("reply_text"), "tts_url": None}
