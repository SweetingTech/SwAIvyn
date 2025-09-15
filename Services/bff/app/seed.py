from __future__ import annotations

import os
import shutil
from pathlib import Path
from typing import Optional

import bcrypt
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncEngine
from sqlalchemy.ext.asyncio import AsyncConnection

from .models import metadata, users, characters, connection_settings

try:
    import yaml  # type: ignore
    YAML_AVAILABLE = True
except ImportError:
    YAML_AVAILABLE = False


def _find_repo_ai_dir(start: Path) -> Optional[Path]:
    """Find the frontend/AI directory from the backend location"""
    # Try direct relative path first (Services/bff -> frontend/AI)
    probe = (start / ".." / ".." / "frontend" / "AI").resolve()
    if probe.exists() and probe.is_dir():
        return probe
    
    # Try walking up the directory tree
    cur = start
    for _ in range(6):
        probe = cur / "frontend" / "AI"
        probe = probe.resolve()
        if probe.exists() and probe.is_dir():
            return probe
        cur = cur.parent
    return None


def _extract_prompt_from_yaml(yaml_path: Path) -> tuple[str, str]:
    """Extract name and system prompt from character YAML file"""
    if not YAML_AVAILABLE:
        return yaml_path.stem, "You are a helpful AI assistant."
    
    try:
        data = yaml.safe_load(yaml_path.read_text(encoding="utf-8")) or {}
        name = str(data.get("name") or yaml_path.stem)
        parts = []
        for k in ("description", "personality", "scenario", "instructions"):
            v = data.get(k)
            if isinstance(v, str) and v.strip():
                parts.append(v.strip())
        prompt = "\n\n".join(parts) or "You are a helpful AI assistant."
        return name, prompt
    except Exception:
        return yaml_path.stem, "You are a helpful AI assistant."


def _find_character_image(dir_path: Path) -> Optional[Path]:
    """Find character image file in directory"""
    for ext in ("*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif", "*.bmp"):
        matches = list(dir_path.glob(ext))
        if matches:
            return matches[0]
    return None


async def _upsert_character_from_ai(conn: AsyncConnection, char_id: str, name: str, system_prompt: str, image_src: Optional[Path]) -> None:
    """Upsert a character from AI folder into the database"""
    # Use the Replit workspace uploads directory
    workspace_root = Path(__file__).resolve().parent.parent.parent  # Services/bff/app -> workspace root
    uploads_dir = Path(os.getenv("UPLOADS_DIR", str(workspace_root / "wwwroot" / "uploads")))
    char_dir = uploads_dir / "characters"
    char_dir.mkdir(parents=True, exist_ok=True)

    image_path_value = ""
    if image_src and image_src.exists():
        # Copy image to uploads/characters with deterministic filename
        dst_name = f"{char_id}{image_src.suffix.lower()}"
        dst_path = char_dir / dst_name
        try:
            shutil.copyfile(image_src, dst_path)
            image_path_value = f"/uploads/characters/{dst_name}"
        except Exception:
            image_path_value = ""

    # Simple upsert: try update, if 0 rows then insert
    res = await conn.execute(
        characters.update().where(characters.c.id == char_id).values(
            user_id=None,
            name=name,
            system_prompt=system_prompt,
            image_path=image_path_value,
        )
    )
    if res.rowcount == 0:  # type: ignore[attr-defined]
        await conn.execute(
            characters.insert().values(
                id=char_id,
                user_id=None,
                name=name,
                system_prompt=system_prompt,
                image_path=image_path_value,
            )
        )


async def _seed_characters_from_ai_folder(conn: AsyncConnection) -> None:
    """Auto-load Sam and Sherlock characters from frontend/AI folder if they exist"""
    if not YAML_AVAILABLE:
        print("PyYAML not available, skipping character card auto-loading", flush=True)
        return
    
    here = Path(__file__).resolve().parent
    ai_dir = _find_repo_ai_dir(here)
    if not ai_dir or not ai_dir.exists():
        print("Could not find frontend/AI directory, skipping character auto-loading", flush=True)
        return

    # Target all three characters from AI folder
    targets = [
        ("GLaDOS", "GLaDOS_Character_card.yaml", "glados"),
        ("Sam", "Sam_Character_card.yaml", "sam"),
        ("Sherlock", "Sherlock_Character_card.yaml", "sherlock"),
    ]

    for folder, yaml_name, char_id in targets:
        folder_path = ai_dir / folder
        yaml_path = folder_path / yaml_name
        if not yaml_path.exists():
            print(f"Skipping {folder}: YAML not found at {yaml_path}", flush=True)
            continue
        
        # Check if character already exists
        res = await conn.execute(select(characters.c.id).where(characters.c.id == char_id).limit(1))
        if res.first():
            print(f"Character {char_id} already exists, skipping", flush=True)
            continue
        
        name, prompt = _extract_prompt_from_yaml(yaml_path)
        img = _find_character_image(folder_path)
        print(f"Auto-loading character {char_id} ({name}) from {folder_path}", flush=True)
        await _upsert_character_from_ai(conn, char_id, name, prompt, img)

    print("Character auto-loading complete", flush=True)


async def _seed_default_connection_settings(conn: AsyncConnection) -> None:
    """Seed default connection settings for all users who don't have them"""
    # Get all users
    res = await conn.execute(select(users.c.id))
    user_ids = [row[0] for row in res.fetchall()]
    
    for user_id in user_ids:
        # Check if connection settings already exist
        res = await conn.execute(
            select(connection_settings.c.user_id)
            .where(connection_settings.c.user_id == user_id)
            .limit(1)
        )
        if res.first():
            continue  # User already has connection settings
        
        # Insert default connection settings
        await conn.execute(
            connection_settings.insert().values(
                user_id=user_id,
                OpenAiApiKey=None,
                ClaudeApiKey=None,
                ClaudeApiUrl="https://api.anthropic.com/v1",
                OllamaApiUrl="http://localhost:11434",
                LmStudioApiUrl="http://localhost:1234", 
                EnableStreaming=True,
                TtsGpu=None,
                SttGpu=None,
            )
        )
        print(f"Seeded default connection settings for user {user_id}", flush=True)


async def ensure_seed(engine: AsyncEngine) -> None:
    async with engine.begin() as conn:  # type: AsyncConnection
        await conn.run_sync(metadata.create_all)

        # Check if any users exist
        res = await conn.execute(select(users.c.id).limit(1))
        if res.first():
            # Users already exist, but still run character auto-loading and connection settings seeding
            await _seed_characters_from_ai_folder(conn)
            await _seed_default_connection_settings(conn)
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

        # Seed a default shared character
        try:
            await conn.execute(
                characters.insert().values(
                    id="default",
                    user_id=None,
                    name="Default",
                    system_prompt="You are a helpful AI assistant.",
                    image_path="",
                )
            )
        except Exception:
            # ignore if already exists
            pass

        # Auto-load character cards from frontend/AI folder
        await _seed_characters_from_ai_folder(conn)
        
        # Seed default connection settings for all users
        await _seed_default_connection_settings(conn)
