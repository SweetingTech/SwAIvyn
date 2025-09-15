from __future__ import annotations

"""Seed helper for agent runtime records.

This module allows seeding existing agent execution records into the
database so dashboards retain history across restarts. Data is sourced
from a JSON file pointed to by ``AGENTS_SEED_FILE`` or the default
``Services/bff/app/seed_data/agents.json`` file if present.
"""

import asyncio
import json
import os
from pathlib import Path
from typing import Any, Dict, List, Optional

from sqlalchemy.ext.asyncio import AsyncConnection

from .db import create_engine
from .models import agents as t_agents, metadata


def _candidate_seed_paths() -> List[Path]:
    """Return possible seed file locations ordered by priority."""

    paths: List[Path] = []
    env_path = os.getenv("AGENTS_SEED_FILE")
    if env_path:
        paths.append(Path(env_path).expanduser())

    default_path = Path(__file__).resolve().parent / "seed_data" / "agents.json"
    paths.append(default_path)
    return paths


def _load_seed_data() -> List[Dict[str, Any]]:
    for path in _candidate_seed_paths():
        if not path.exists():
            continue
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
        except Exception as exc:  # pragma: no cover - defensive logging
            print(f"Failed to read agents seed file {path}: {exc}", flush=True)
            continue
        if isinstance(data, list):
            return [item for item in data if isinstance(item, dict)]
        print(f"Agents seed file {path} is not a list; skipping", flush=True)
    return []


def _normalize_record(raw: Dict[str, Any]) -> Optional[Dict[str, Any]]:
    agent_id = str(raw.get("id") or "").strip()
    if not agent_id:
        return None

    name = raw.get("name") or agent_id
    status = str(raw.get("status") or "pending")
    user_id = raw.get("userId") or None
    started_at = raw.get("startedAt") or None
    finished_at = raw.get("finishedAt") or None
    meta = raw.get("meta") if isinstance(raw.get("meta"), dict) else None

    meta_json = json.dumps(meta, ensure_ascii=False) if meta else None

    return {
        "id": agent_id,
        "name": str(name),
        "status": status,
        "user_id": user_id,
        "started_at": started_at,
        "finished_at": finished_at,
        "meta": meta_json,
    }


async def seed_agents_from_file(conn: AsyncConnection) -> None:
    """Seed agents from JSON definition file if available."""

    records = _load_seed_data()
    if not records:
        return

    for record in records:
        normalized = _normalize_record(record)
        if not normalized:
            continue
        agent_id = normalized.pop("id")
        res = await conn.execute(
            t_agents.update().where(t_agents.c.id == agent_id).values(**normalized)
        )
        if res.rowcount == 0:  # type: ignore[attr-defined]
            await conn.execute(t_agents.insert().values(id=agent_id, **normalized))


async def main() -> None:
    engine = create_engine()
    if engine is None:
        raise SystemExit("DATABASE_URL not set; cannot seed agents.")

    async with engine.begin() as conn:
        await conn.run_sync(metadata.create_all)
        await seed_agents_from_file(conn)

    await engine.dispose()
    print("Seeded agents from file (if provided).")


if __name__ == "__main__":
    asyncio.run(main())
