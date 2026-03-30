import os
from typing import Optional

from sqlalchemy.ext.asyncio import AsyncEngine, create_async_engine


def get_database_url() -> Optional[str]:
    return os.getenv("DATABASE_URL")


def create_engine() -> Optional[AsyncEngine]:
    url = get_database_url()
    if not url:
        return None
    # Convert postgresql:// to postgresql+asyncpg:// for async support
    if url.startswith("postgresql://"):
        url = url.replace("postgresql://", "postgresql+asyncpg://")
    # Remove sslmode parameter for asyncpg compatibility (asyncpg uses connect_args instead)
    if "sslmode=" in url:
        url = url.replace("?sslmode=require", "").replace("&sslmode=require", "")
    # Enable SSL by default for production safety. Set DB_SSL=false only for local dev.
    use_ssl = os.getenv("DB_SSL", "true").lower() not in ("0", "false", "no")
    if not use_ssl:
        import logging as _logging
        _logging.getLogger("swai.db").warning(
            "Database SSL is DISABLED (DB_SSL=false). Do not use this setting in production."
        )
    connect_args = {"ssl": True} if use_ssl else {}
    
    # Connection pool configuration to prevent timeout issues
    return create_async_engine(
        url, 
        echo=False, 
        future=True, 
        connect_args=connect_args,
        pool_size=20,           # Maintain 20 connections in pool
        pool_recycle=3600,      # Recreate connections every hour (prevents timeouts)
        pool_pre_ping=True,     # Test connections before use
        pool_timeout=30,        # Wait 30 seconds for connection
        max_overflow=20         # Allow 20 additional connections if needed
    )

