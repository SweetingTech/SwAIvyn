import os
import asyncio
import sys
from concurrent.futures import ThreadPoolExecutor
from temporalio.client import Client
from temporalio.worker import Worker

from . import workflows
from . import activities as acts


async def _connect_with_retry(addr: str) -> Client:
    delay = 1
    last_err = None
    for attempt in range(1, 31):
        try:
            return await Client.connect(addr)
        except Exception as e:
            last_err = e
            print(f"Temporal connect failed (attempt {attempt}): {e}", file=sys.stderr, flush=True)
            await asyncio.sleep(delay)
            delay = min(delay * 2, 10)
    raise RuntimeError(f"Unable to connect to Temporal at {addr}") from last_err


async def main() -> None:
    client = await _connect_with_retry(os.getenv("TEMPORAL_HOST", "temporal:7233"))
    activity_threads = int(os.getenv("ACTIVITY_THREADS", "8"))
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
    await worker.run()


if __name__ == "__main__":
    asyncio.run(main())
