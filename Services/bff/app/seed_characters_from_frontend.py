from __future__ import annotations

"""
Seed global characters (user_id=NULL) from frontend character card YAMLs.

Looks for YAML cards in the SwAIvyn frontend AI directory (Sam, Sherlock),
copies their images into the backend uploads folder, and upserts rows in the
`characters` table so the UI character selector lists them.

Run:
  (from Services/bff)
  1) . ./.venv/Scripts/Activate.ps1  (on Windows PowerShell)
  2) python -m app.seed_characters_from_frontend

Env overrides:
  - AI_SOURCE_DIR: override search path to the character cards
  - UPLOADS_DIR: already used by backend; images copied to /uploads/characters
"""

import asyncio
import os
import shutil
from pathlib import Path
from typing import Optional, Tuple

import yaml  # type: ignore

from .db import create_engine
from .models import metadata, characters as t_characters


def find_repo_ai_dir(start: Path) -> Optional[Path]:
    # Walk up to 5 levels up looking for frontend/AI
    cur = start
    for _ in range(6):
        probe = cur / ".." / ".." / ".." / "frontend" / "AI"
        probe = probe.resolve()
        if probe.exists() and probe.is_dir():
            return probe
        cur = cur.parent
    # Fallback: try common relative path
    probe = (start / "../../../../frontend/AI").resolve()
    if probe.exists() and probe.is_dir():
        return probe
    return None


def extract_prompt(yaml_path: Path) -> Tuple[str, str]:
    data = yaml.safe_load(yaml_path.read_text(encoding="utf-8")) or {}
    name = str(data.get("name") or yaml_path.stem)
    parts = []
    for k in ("description", "personality", "scenario", "instructions"):
        v = data.get(k)
        if isinstance(v, str) and v.strip():
            parts.append(v.strip())
    prompt = "\n\n".join(parts) or "You are a helpful AI assistant."
    return name, prompt


def find_image_file(dir_path: Path) -> Optional[Path]:
    for ext in ("*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif", "*.bmp"):
        matches = list(dir_path.glob(ext))
        if matches:
            return matches[0]
    return None


async def upsert_character(char_id: str, name: str, system_prompt: str, image_src: Optional[Path]) -> None:
    engine = create_engine()
    if engine is None:
        raise SystemExit("DATABASE_URL not set; cannot seed characters.")
    uploads_dir = Path(os.getenv("UPLOADS_DIR", "/app/wwwroot/uploads"))
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

    async with engine.begin() as conn:
        await conn.run_sync(metadata.create_all)
        # Simple upsert: try update, if 0 rows then insert
        res = await conn.execute(
            t_characters.update().where(t_characters.c.id == char_id).values(
                user_id=None,
                name=name,
                system_prompt=system_prompt,
                image_path=image_path_value,
            )
        )
        if res.rowcount == 0:  # type: ignore[attr-defined]
            await conn.execute(
                t_characters.insert().values(
                    id=char_id,
                    user_id=None,
                    name=name,
                    system_prompt=system_prompt,
                    image_path=image_path_value,
                )
            )
    await engine.dispose()


async def main() -> None:
    here = Path(__file__).resolve().parent
    ai_override = os.getenv("AI_SOURCE_DIR")
    ai_dir = Path(ai_override) if ai_override else find_repo_ai_dir(here)
    if not ai_dir or not ai_dir.exists():
        raise SystemExit("Could not locate frontend/AI directory. Set AI_SOURCE_DIR to override.")

    # GLaDOS is already present/hardcoded; focus on Sam and Sherlock if available
    targets = [
        ("Sam", "Sam_Character_card.yaml", "sam"),
        ("Sherlock", "Sherlock_Character_card.yaml", "sherlock"),
    ]

    for folder, yaml_name, cid in targets:
        folder_path = ai_dir / folder
        yaml_path = folder_path / yaml_name
        if not yaml_path.exists():
            print(f"Skipping {folder}: YAML not found at {yaml_path}")
            continue
        name, prompt = extract_prompt(yaml_path)
        img = find_image_file(folder_path)
        print(f"Upserting character {cid} ({name})")
        await upsert_character(cid, name, prompt, img)

    print("Character seed complete.")


if __name__ == "__main__":
    asyncio.run(main())

