import os
from typing import Optional

from sqlalchemy.ext.asyncio import AsyncEngine, create_async_engine


def get_database_url() -> Optional[str]:
    return os.getenv("DATABASE_URL")


def create_engine() -> Optional[AsyncEngine]:
    url = get_database_url()
    if not url:
        return None
    return create_async_engine(url, echo=False, future=True)

