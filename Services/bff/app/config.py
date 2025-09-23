from __future__ import annotations

import os
from pathlib import Path
from typing import List, Optional

from pydantic import BaseModel, Field, ValidationError, validator


class Settings(BaseModel):
    """Validated runtime configuration for the BFF service."""

    database_url: str = Field(alias="DATABASE_URL")
    jwt_secret: str = Field(alias="JWT_SECRET")
    uploads_dir: Path = Field(default=Path("./wwwroot/uploads"), alias="UPLOADS_DIR")
    enable_temporal: bool = Field(default=False, alias="ENABLE_TEMPORAL")
    temporal_host: str = Field(default="127.0.0.1:7233", alias="TEMPORAL_HOST")
    allow_origins: List[str] = Field(default_factory=list, alias="ALLOWED_ORIGINS")

    @validator("database_url")
    def _require_database_url(cls, value: str) -> str:
        if not value:
            raise ValueError("DATABASE_URL is required for the BFF service to start")
        if not value.startswith("postgresql") and not value.startswith("sqlite"):
            raise ValueError("DATABASE_URL must be a PostgreSQL or SQLite connection string")
        return value

    @validator("jwt_secret")
    def _require_jwt_secret(cls, value: str) -> str:
        if not value or len(value) < 16:
            raise ValueError("JWT_SECRET must be at least 16 characters long")
        return value

    @validator("uploads_dir")
    def _ensure_uploads_dir(cls, value: Path) -> Path:
        if not value:
            raise ValueError("UPLOADS_DIR cannot be empty")
        return value

    @validator("temporal_host")
    def _normalize_temporal_host(cls, value: str) -> str:
        value = value.strip()
        if not value:
            return "127.0.0.1:7233"
        return value

    @validator("allow_origins", pre=True)
    def _split_allow_origins(cls, value: Optional[str]) -> List[str]:
        if value is None or value == "":
            return []
        if isinstance(value, str):
            return [v.strip() for v in value.split(",") if v.strip()]
        return value


def load_settings() -> Settings:
    """Load and validate environment configuration.

    Raises a `RuntimeError` with an actionable message when validation fails so
    startup can stop early instead of failing at runtime.
    """

    data = {}
    for field_name, field in Settings.model_fields.items():
        env_key = field.alias or field_name
        value = os.getenv(env_key)
        if value is None and field.default is not None:
            value = field.default
        data[field_name] = value
    try:
        settings = Settings(**data)
    except ValidationError as exc:
        errors = "; ".join(
            f"{err['loc'][0]}: {err['msg']}" for err in exc.errors()
        )
        raise RuntimeError(f"Invalid environment configuration: {errors}") from exc

    uploads_dir = settings.uploads_dir
    uploads_dir.mkdir(parents=True, exist_ok=True)

    return settings


SETTINGS = load_settings()
