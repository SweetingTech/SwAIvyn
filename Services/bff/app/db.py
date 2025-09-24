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
    # Remove sslmode parameter for asyncpg compatibility (we don't use SSL in dev)
    if "sslmode=" in url:
        url = url.replace("?sslmode=require", "").replace("&sslmode=require", "")
    # Disable SSL by default for local/dev Postgres; enable only if explicitly asked via DB_SSL=true
    use_ssl = os.getenv("DB_SSL", "false").lower() in ("1","true","yes")
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

