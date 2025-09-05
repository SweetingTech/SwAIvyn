import asyncio
import os

from sqlalchemy.ext.asyncio import create_async_engine
from sqlalchemy import text


SQL = (
    'ALTER TABLE connection_settings '
    'ADD COLUMN IF NOT EXISTS "TtsGpu" VARCHAR(8), '
    'ADD COLUMN IF NOT EXISTS "SttGpu" VARCHAR(8)'
)


async def main() -> None:
    url = os.getenv("DATABASE_URL")
    if not url:
        raise SystemExit("DATABASE_URL is not set")
    engine = create_async_engine(url, echo=False, future=True)
    async with engine.begin() as conn:
        await conn.execute(text(SQL))
    await engine.dispose()
    print("GPU columns ensured on connection_settings")


if __name__ == "__main__":
    asyncio.run(main())
