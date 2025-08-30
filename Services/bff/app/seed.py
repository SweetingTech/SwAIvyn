from __future__ import annotations

import bcrypt
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncEngine
from sqlalchemy.ext.asyncio import AsyncConnection

from .models import metadata, users


async def ensure_seed(engine: AsyncEngine) -> None:
    async with engine.begin() as conn:  # type: AsyncConnection
        await conn.run_sync(metadata.create_all)

        # Check if any users exist
        res = await conn.execute(select(users.c.id).limit(1))
        if res.first():
            return

        # Seed default users: admin, Mari, DJay
        seed_users = [
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
                "id": "mari",
                "username": "Mari",
                "email": "mari@example.com",
                "password": "mari1234",
                "role": "user",
                "language": "ja",
                "theme": "light",
                "default_character": "default",
                "is_default": False,
            },
            {
                "id": "djay",
                "username": "DJay",
                "email": "djay@example.com",
                "password": "djay1234",
                "role": "user",
                "language": "en",
                "theme": "dark",
                "default_character": "default",
                "is_default": True,  # default profile for single-user startup
            },
        ]

        for u in seed_users:
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

