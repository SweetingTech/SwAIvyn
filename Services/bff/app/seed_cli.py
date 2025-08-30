import asyncio
from .db import create_engine
from .seed import ensure_seed


async def main():
    engine = create_engine()
    if engine is None:
        print("DATABASE_URL is not set")
        return
    await ensure_seed(engine)
    print("Seed complete")


if __name__ == "__main__":
    asyncio.run(main())

