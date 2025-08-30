from __future__ import annotations

from typing import Optional, Dict, Any
from dataclasses import dataclass

from temporalio import workflow


@dataclass
class ChatRequest:
    message: str
    user_id: Optional[str] = None
    session_id: Optional[str] = None


@workflow.defn(name="ReplyWorkflow")
class ReplyWorkflow:
    @workflow.run
    async def run(self, req: Dict[str, Any]) -> Dict[str, Any]:
        # Convert to object
        cr = ChatRequest(**req)

        reply = await workflow.execute_activity(
            "generate_reply",
            {"message": cr.message, "user_id": cr.user_id},
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

from datetime import timedelta

