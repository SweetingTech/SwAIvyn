from __future__ import annotations

import json
from typing import Any, Dict, List, Optional

from sqlalchemy import delete, select, update
from sqlalchemy.ext.asyncio import AsyncConnection, AsyncEngine

from .models import agents as t_agents


class AgentStore:
    """Persistence helper for runtime agent activity."""

    def __init__(self) -> None:
        self._memory: Dict[str, Dict[str, Any]] = {}

    async def list_agents(
        self,
        engine: Optional[AsyncEngine],
        *,
        viewer_id: Optional[str],
        viewer_is_admin: bool,
    ) -> List[Dict[str, Any]]:
        """Return agents visible to the given viewer."""

        if engine is None:
            agents = list(self._memory.values())
            if viewer_is_admin:
                return [agent.copy() for agent in agents]
            return [agent.copy() for agent in agents if agent.get("userId") == viewer_id]

        async with engine.connect() as conn:
            query = select(t_agents)
            if not viewer_is_admin:
                if not viewer_id:
                    return []
                query = query.where(t_agents.c.user_id == viewer_id)
            result = await conn.execute(query)
            rows = result.fetchall()
            return [self._row_to_agent(row) for row in rows]

    async def start_agent(
        self,
        engine: Optional[AsyncEngine],
        agent_id: str,
        updates: Dict[str, Any],
    ) -> Dict[str, Any]:
        """Record/merge agent state when it starts running."""

        return await self._upsert_agent(engine, agent_id, updates, preserve_started_at=True)

    async def stop_agent(
        self,
        engine: Optional[AsyncEngine],
        agent_id: str,
        updates: Dict[str, Any],
    ) -> Dict[str, Any]:
        """Record/merge agent state when it finishes."""

        return await self._upsert_agent(engine, agent_id, updates, preserve_started_at=True)

    async def delete_agent(self, engine: Optional[AsyncEngine], agent_id: str) -> None:
        """Remove agent record."""

        if engine is None:
            self._memory.pop(agent_id, None)
            return

        async with engine.begin() as conn:
            await conn.execute(delete(t_agents).where(t_agents.c.id == agent_id))

    async def _upsert_agent(
        self,
        engine: Optional[AsyncEngine],
        agent_id: str,
        updates: Dict[str, Any],
        *,
        preserve_started_at: bool = False,
    ) -> Dict[str, Any]:
        if engine is None:
            existing = self._memory.get(agent_id)
            merged = self._merge_agent(agent_id, existing, updates, preserve_started_at=preserve_started_at)
            self._memory[agent_id] = merged
            return merged.copy()

        async with engine.begin() as conn:
            existing = await self._fetch_agent(conn, agent_id)
            merged = self._merge_agent(agent_id, existing, updates, preserve_started_at=preserve_started_at)
            payload = self._to_db_payload(merged)
            update_values = payload.copy()
            update_values.pop("id", None)
            if existing is not None:
                await conn.execute(update(t_agents).where(t_agents.c.id == agent_id).values(update_values))
            else:
                await conn.execute(t_agents.insert().values(payload))
            return merged

    async def _fetch_agent(self, conn: AsyncConnection, agent_id: str) -> Optional[Dict[str, Any]]:
        result = await conn.execute(select(t_agents).where(t_agents.c.id == agent_id).limit(1))
        row = result.first()
        if row is None:
            return None
        return self._row_to_agent(row)

    def _merge_agent(
        self,
        agent_id: str,
        existing: Optional[Dict[str, Any]],
        updates: Dict[str, Any],
        *,
        preserve_started_at: bool,
    ) -> Dict[str, Any]:
        base = existing or {}
        merged: Dict[str, Any] = {
            "id": agent_id,
            "name": updates.get("name") or base.get("name") or agent_id,
            "status": updates.get("status") or base.get("status") or "pending",
            "userId": updates.get("userId") or base.get("userId"),
            "meta": self._normalize_meta(updates.get("meta") if "meta" in updates else base.get("meta")),
        }

        if preserve_started_at:
            merged["startedAt"] = base.get("startedAt") or updates.get("startedAt")
        else:
            merged["startedAt"] = updates.get("startedAt") if "startedAt" in updates else base.get("startedAt")

        if "finishedAt" in updates:
            merged["finishedAt"] = updates.get("finishedAt")
        else:
            merged["finishedAt"] = base.get("finishedAt")

        return merged

    def _normalize_meta(self, meta: Any) -> Dict[str, Any]:
        if isinstance(meta, dict):
            return meta
        return {}

    def _row_to_agent(self, row: Any) -> Dict[str, Any]:
        mapping = row._mapping if hasattr(row, "_mapping") else row
        raw_meta = mapping.get("meta")
        meta: Dict[str, Any] = {}
        if isinstance(raw_meta, dict):
            meta = raw_meta
        elif isinstance(raw_meta, str) and raw_meta:
            try:
                parsed = json.loads(raw_meta)
                if isinstance(parsed, dict):
                    meta = parsed
            except json.JSONDecodeError:
                meta = {}

        return {
            "id": mapping.get("id"),
            "name": mapping.get("name"),
            "status": mapping.get("status"),
            "userId": mapping.get("user_id"),
            "startedAt": mapping.get("started_at"),
            "finishedAt": mapping.get("finished_at"),
            "meta": meta,
        }

    def _to_db_payload(self, agent: Dict[str, Any]) -> Dict[str, Any]:
        meta = agent.get("meta") or {}
        meta_json = json.dumps(meta, ensure_ascii=False) if meta else None
        return {
            "id": agent.get("id"),
            "name": agent.get("name") or agent.get("id"),
            "status": agent.get("status") or "pending",
            "user_id": agent.get("userId"),
            "started_at": agent.get("startedAt"),
            "finished_at": agent.get("finishedAt"),
            "meta": meta_json,
        }
