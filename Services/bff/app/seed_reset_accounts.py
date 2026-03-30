from __future__ import annotations

"""
Force-seed the core user accounts (admin, user1, user2) and the default character.

Safe to run multiple times. It will:
 - Ensure tables exist
 - Upsert (delete+insert) users with IDs: admin, user1, user2
 - Upsert the shared default character

Environment:
 - Uses DATABASE_URL if set (e.g. postgresql+asyncpg://postgres:pwd@localhost:5432/swai)
 - If not set, and POSTGRES_PASSWORD is present, it will build DATABASE_URL to local Postgres.

Run:
  (from Services/bff)
  1) . ./.venv/Scripts/Activate.ps1  (Windows PowerShell)
  2) python -m app.seed_reset_accounts
"""

import asyncio
import os
from typing import Sequence

import bcrypt
from sqlalchemy import select, delete
from sqlalchemy.ext.asyncio import AsyncEngine

from .db import create_engine
from .models import metadata, users, characters


SEED_USERS = [
    {
        "id": "admin",
        "username": "admin",
        "email": "admin@example.com",
        "password": "admin1234",
        "role": "admin",
        "language": "en",
        "theme": "dark",
        "default_character": "default",
        "is_default": False,
    },
    {
        "id": "user1",
        "username": "user1",
        "email": "user1@example.com",
        "password": "user11234",
        "role": "user",
        "language": "en",
        "theme": "light",
        "default_character": "default",
        "is_default": False,
    },
    {
        "id": "user2",
        "username": "user2",
        "email": "user2@example.com",
        "password": "user21234",
        "role": "user",
        "language": "en",
        "theme": "dark",
        "default_character": "default",
        "is_default": True,  # default profile for single-user startup
    },
]


async def _ensure_engine() -> AsyncEngine:
    # Build DATABASE_URL from POSTGRES_PASSWORD if missing, matching dev-bff.ps1 behavior
    if not os.getenv("DATABASE_URL"):
        pg_pwd = os.getenv("POSTGRES_PASSWORD")
        if pg_pwd:
            os.environ["DATABASE_URL"] = f"postgresql+asyncpg://postgres:{pg_pwd}@localhost:5432/swai"
    engine = create_engine()
    if engine is None:
        raise SystemExit("DATABASE_URL not set and POSTGRES_PASSWORD missing; cannot connect to DB.")
    return engine


async def force_seed_accounts() -> None:
    engine = await _ensure_engine()
    async with engine.begin() as conn:
        # Ensure tables exist
        await conn.run_sync(metadata.create_all)

        # Delete existing seed user IDs to allow clean re-insert
        ids: Sequence[str] = [u["id"] for u in SEED_USERS]
        await conn.execute(delete(users).where(users.c.id.in_(ids)))

        # Insert users with fresh password hashes
        for u in SEED_USERS:
            pw_hash = bcrypt.hashpw(u["password"].encode(), bcrypt.gensalt()).decode()
            await conn.execute(
                users.insert().values(
                    id=u["id"],
                    username=u["username"],
                    email=u["email"],
                    password_hash=pw_hash,
                    role=u["role"],
                    language=u["language"],
                    theme=u["theme"],
                    default_character=u["default_character"],
                    is_default=u["is_default"],
                )
            )

        # Upsert shared default character
        existing = await conn.execute(select(characters.c.id).where(characters.c.id == "default").limit(1))
        if existing.first():
            # Replace content
            await conn.execute(
                characters.update().where(characters.c.id == "default").values(
                    user_id=None,
                    name="Default",
                    system_prompt="You are a helpful AI assistant.",
                    image_path="",
                )
            )
        else:
            await conn.execute(
                characters.insert().values(
                    id="default",
                    user_id=None,
                    name="Default",
                    system_prompt="You are a helpful AI assistant.",
                    image_path="",
                )
            )

    await engine.dispose()
    print("Seeded users: admin, user1, user2 (passwords reset) and default character.")


def main() -> None:
    asyncio.run(force_seed_accounts())


if __name__ == "__main__":
    main()

