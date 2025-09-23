import os
import asyncio
import sys
from contextlib import suppress
from concurrent.futures import ThreadPoolExecutor
import uvicorn
from fastapi import FastAPI
from temporalio.client import Client
from temporalio.worker import Worker

from . import workflows
from . import activities as acts


health_app = FastAPI(title="SwAIvyn Orchestrator")
_worker_ready = False


@health_app.get("/healthz")
async def healthz() -> dict[str, str]:
    return {"status": "ok"}


@health_app.get("/readyz")
async def readyz() -> dict[str, str]:
    return {"status": "ready" if _worker_ready else "starting"}


async def _connect_with_retry(addr: str) -> Client:
    """Keep trying to connect to Temporal until success.
    Dev quality-of-life: do not crash the worker if Temporal starts slowly.
    """
    delay = 1
    attempt = 0
    while True:
        attempt += 1
        try:
            return await Client.connect(addr)
        except Exception as e:
            print(f"Temporal connect failed (attempt {attempt}) to {addr}: {e}", file=sys.stderr, flush=True)
            await asyncio.sleep(delay)
            # Exponential backoff up to 10s
            delay = min(delay * 2, 10)


async def _run_health_server(port: int) -> None:
    config = uvicorn.Config(health_app, host="0.0.0.0", port=port, log_level="info")
    server = uvicorn.Server(config)
    await server.serve()


async def _run_worker(client: Client, activity_threads: int) -> None:
    global _worker_ready
    worker = Worker(
        client,
        task_queue="reply-queue",
        workflows=[
            workflows.ReplyWorkflow,
            workflows.ReplyWorkflowOllama,
            workflows.ReplyWorkflowLMStudio,
            workflows.ReplyWorkflowOpenAI,
            workflows.ReplyWorkflowClaude,
            workflows.ReplyWorkflowVLLM,
        ],
        activities=[
            acts.generate_reply,
            acts.synthesize_tts,
            acts.upsert_vector_memory,
            acts.update_graph,
        ],
        activity_executor=ThreadPoolExecutor(max_workers=activity_threads),
    )
    _worker_ready = True
    await worker.run()


async def main() -> None:
    client = await _connect_with_retry(os.getenv("TEMPORAL_HOST", "temporal:7233"))
    activity_threads = int(os.getenv("ACTIVITY_THREADS", "8"))
    health_port = int(os.getenv("PORT", "9000"))

    health_task = asyncio.create_task(_run_health_server(health_port))
    try:
        await _run_worker(client, activity_threads)
    finally:
        health_task.cancel()
        with suppress(asyncio.CancelledError):
            await health_task


if __name__ == "__main__":
    asyncio.run(main())
