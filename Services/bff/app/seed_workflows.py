from __future__ import annotations

"""
Seed the default chat workflow into the DB (global definition).

Run:
  (from Services/bff)
  1) . ./.venv/Scripts/Activate.ps1
  2) python -m app.seed_workflows
"""

import asyncio
import json
from typing import Any

from .db import create_engine
from .models import metadata, workflows


DEFAULT_CHAT_WORKFLOW: dict[str, Any] = {
    "id": "default-chat",
    "name": "Default Chat",
    "version": 1,
    "steps": [
        {"id": "select_llm", "type": "select_llm", "from": "chat_settings"},
        {"id": "build_conn", "type": "build_connections", "from": "connection_settings"},
        {"id": "temporal", "type": "start_temporal", "queue": "reply-queue"},
        {"id": "format_reply", "type": "format"},
    ],
}


async def main() -> None:
    engine = create_engine()
    if engine is None:
        raise SystemExit("DATABASE_URL not set; cannot seed workflows.")
    async with engine.begin() as conn:
        await conn.run_sync(metadata.create_all)
        # Upsert by id
        payload = json.dumps(DEFAULT_CHAT_WORKFLOW, ensure_ascii=False)
        res = await conn.execute(
            workflows.update().where(workflows.c.id == DEFAULT_CHAT_WORKFLOW["id"]).values(
                name=DEFAULT_CHAT_WORKFLOW["name"],
                version=str(DEFAULT_CHAT_WORKFLOW["version"]),
                definition=payload,
            )
        )
        if res.rowcount == 0:  # type: ignore[attr-defined]
            await conn.execute(
                workflows.insert().values(
                    id=DEFAULT_CHAT_WORKFLOW["id"],
                    name=DEFAULT_CHAT_WORKFLOW["name"],
                    version=str(DEFAULT_CHAT_WORKFLOW["version"]),
                    definition=payload,
                )
            )
    await engine.dispose()
    print("Seeded default chat workflow.")


if __name__ == "__main__":
    asyncio.run(main())

