from __future__ import annotations

from sqlalchemy import MetaData, Table, Column, String, Boolean, text


metadata = MetaData()

users = Table(
    "users",
    metadata,
    Column("id", String(64), primary_key=True),
    Column("username", String(100), nullable=False, unique=True),
    Column("email", String(200), nullable=False, unique=True),
    Column("password_hash", String(200), nullable=True),
    Column("role", String(32), nullable=False, server_default=text("'user'")),
    Column("language", String(8), nullable=False, server_default=text("'en'")),
    Column("theme", String(16), nullable=False, server_default=text("'light'")),
    Column("default_character", String(100), nullable=True),
    Column("is_default", Boolean, nullable=False, server_default=text("false")),
)

