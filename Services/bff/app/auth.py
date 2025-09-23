from __future__ import annotations

import os
import time
from typing import Optional, Dict, Any

import bcrypt
import jwt
from fastapi import HTTPException, Depends
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncEngine

from .models import users
from .config import SETTINGS


JWT_SECRET = SETTINGS.jwt_secret
JWT_EXPIRES_MINUTES = int(os.getenv("JWT_EXPIRES_MINUTES", "60"))


def create_access_token(payload: Dict[str, Any], expires_minutes: Optional[int] = None) -> str:
    exp_mins = expires_minutes or JWT_EXPIRES_MINUTES
    to_encode = payload.copy()
    to_encode["exp"] = int(time.time()) + exp_mins * 60
    return jwt.encode(to_encode, JWT_SECRET, algorithm="HS256")


def verify_password(plain: str, hashed: str) -> bool:
    try:
        return bcrypt.checkpw(plain.encode(), hashed.encode())
    except Exception:
        return False


bearer_scheme = HTTPBearer(auto_error=False)


async def get_current_user(engine: Optional[AsyncEngine], creds: Optional[HTTPAuthorizationCredentials] = Depends(bearer_scheme)) -> Optional[Dict[str, Any]]:
    if creds is None:
        return None
    token = creds.credentials
    try:
        data = jwt.decode(token, JWT_SECRET, algorithms=["HS256"])
    except jwt.ExpiredSignatureError:
        raise HTTPException(status_code=401, detail="Token expired")
    except Exception:
        raise HTTPException(status_code=401, detail="Invalid token")

    sub = data.get("sub")
    if not sub:
        raise HTTPException(status_code=401, detail="Invalid token payload")

    if engine is None:
        # Minimal fallback
        return {"id": sub, "username": sub, "email": f"{sub}@example.com", "role": data.get("role", "user")}

    async with engine.connect() as conn:
        res = await conn.execute(select(users).where(users.c.id == sub).limit(1))
        row = res.first()
        if not row:
            raise HTTPException(status_code=401, detail="User not found")
        r = row._mapping
        return {"id": r["id"], "username": r["username"], "email": r["email"], "role": r["role"], "language": r["language"], "theme": r["theme"]}


def require_admin(user: Optional[Dict[str, Any]]):
    if not user or user.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Admin required")

