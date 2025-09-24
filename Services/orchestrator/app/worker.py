import os
import asyncio
import logging
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer
from concurrent.futures import ThreadPoolExecutor
from typing import Mapping

from temporalio.client import Client
from temporalio.worker import Worker

from . import workflows
from . import activities as acts


logging.basicConfig(
    level=os.getenv("ORCHESTRATOR_LOG_LEVEL", "INFO").upper(),
    format="%(asctime)s %(levelname)s [orchestrator] %(message)s",
)
logger = logging.getLogger("swai.orchestrator")

REQUIRED_ENV_VARS: Mapping[str, str] = {
    "TEMPORAL_HOST": "Temporal frontend host:port for the worker to connect",
}

_READINESS_EVENT = threading.Event()


def _require_env(name: str) -> str:
    value = os.getenv(name)
    if value is None or value.strip() == "":
        logger.critical("Missing required environment variable %s", name)
        raise SystemExit(1)
    return value.strip()


def _parse_activity_threads() -> int:
    raw_value = os.getenv("ACTIVITY_THREADS", "8").strip()
    try:
        value = int(raw_value)
    except ValueError as exc:
        logger.critical("ACTIVITY_THREADS must be an integer, got %r", raw_value)
        raise SystemExit(1) from exc
    if value <= 0:
        logger.critical("ACTIVITY_THREADS must be greater than zero, got %s", value)
        raise SystemExit(1)
    return value


def _start_health_server() -> None:
    port_raw = os.getenv("ORCHESTRATOR_HEALTH_PORT", "8088").strip()
    try:
        port = int(port_raw)
    except ValueError as exc:
        logger.critical("ORCHESTRATOR_HEALTH_PORT must be an integer, got %r", port_raw)
        raise SystemExit(1) from exc
    if port <= 0 or port >= 65536:
        logger.critical("ORCHESTRATOR_HEALTH_PORT must be between 1 and 65535, got %s", port)
        raise SystemExit(1)

    class _HealthHandler(BaseHTTPRequestHandler):
        def do_GET(self):  # type: ignore[override]
            if self.path not in ("/healthz", "/readyz"):
                self.send_response(404)
                self.end_headers()
                return

            if self.path == "/readyz" and not _READINESS_EVENT.is_set():
                body = b'{"status":"starting"}'
                self.send_response(503)
            else:
                body = b'{"status":"ok"}' if self.path == "/healthz" else b'{"status":"ready"}'
                self.send_response(200)

            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, format: str, *args):  # type: ignore[override]
            logger.debug("Health server: " + format, *args)

    try:
        server = HTTPServer(("0.0.0.0", port), _HealthHandler)
    except OSError as exc:
        logger.critical("Failed to start health server on port %s: %s", port, exc)
        raise SystemExit(1) from exc

    thread = threading.Thread(target=server.serve_forever, name="orchestrator-health", daemon=True)
    thread.start()
    logger.info("Orchestrator health endpoint listening on 0.0.0.0:%s", port)


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
            logger.warning(
                "Temporal connect failed (attempt %s) to %s: %s",
                attempt,
                addr,
                e,
            )
            await asyncio.sleep(delay)
            # Exponential backoff up to 10s
            delay = min(delay * 2, 10)


async def main() -> None:
    env_values = {name: _require_env(name) for name in REQUIRED_ENV_VARS}
    temporal_host = env_values["TEMPORAL_HOST"]
    _start_health_server()
    activity_threads = _parse_activity_threads()
    client = await _connect_with_retry(temporal_host)
    _READINESS_EVENT.set()
    logger.info("Orchestrator worker marked ready")
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
    logger.info(
        "Temporal worker connected to %s with %s activity threads",
        temporal_host,
        activity_threads,
    )
    await worker.run()


if __name__ == "__main__":
    asyncio.run(main())
