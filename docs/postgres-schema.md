# PostgreSQL Schema Overview

This document captures the authoritative PostgreSQL schema used by the SwAIvyn Backend For Frontend (BFF). The SQLAlchemy table definitions live in `Services/bff/app/models.py` and are synchronized through Alembic migrations. Use this reference when validating migrations, onboarding contributors, or troubleshooting database issues.

## Key Characteristics

- **Engine**: PostgreSQL 16 (see `docker-compose.yml` / `docker-stack.yml`).
- **Migration Tooling**: Alembic (invoked through `scripts/alembic` helpers).
- **ORM**: SQLAlchemy Core models defined in `Services/bff/app/models.py`.
- **Connection String**: Supplied via the required `DATABASE_URL` environment variable (`postgresql+asyncpg://...`).

## Core Tables

| Table | Purpose | Notable Columns |
|-------|---------|-----------------|
| `users` | Accounts with UI preferences | `username` (unique), `email` (unique nullable), `role`, `language`, `theme`, `is_default` |
| `chat_settings` | Per-user chat + TTS selections | `llm_engine`, `enabled_engines`, `engine_models` |
| `connection_settings` | API keys and host overrides | `OpenAiApiKey`, `ClaudeApiKey`, `OllamaApiUrl`, `EnableStreaming` |
| `characters` | Prompt templates and avatars | `user_id` (nullable for shared), `system_prompt`, `image_path` |
| `conversations` | Conversation metadata | `title`, `folder_id`, `created_at`, `last_updated` |
| `messages` | Conversation message log | `conversation_id`, `role`, `content`, `timestamp` |
| `workflows` | Stored workflow definitions | `name`, `version`, `definition` |
| `agent_status` | Tracks orchestrated agent executions | `status`, `meta`, timestamps |
| `agents` | User-facing agent definitions | `description`, `status`, `goal` |
| `agent_registry` | Published agent catalog | `file_path`, `capabilities` |
| `agent_tasks` | Work items for agents | `status`, `payload`, `assigned_agent_id` |
| `agent_results` | Outputs from completed tasks | `result`, `error`, `completed_at` |

> The SQLAlchemy module also defines additional helper tables (e.g., `folders`) which follow the same conventions—string primary keys and cascading foreign keys.

## Validating the Schema

1. **Generate a Schema Snapshot**
   ```bash
   poetry run alembic revision --autogenerate -m "check schema"
   ```
   Inspect the generated migration to confirm no unintended diffs. Delete the revision if not applying.

2. **Apply Latest Migrations**
   ```bash
   poetry run alembic upgrade head
   ```

3. **Regenerate TypeScript Types (Optional)**
   Use `@hey-api/openapi-ts` against the FastAPI OpenAPI document to ensure frontend types stay synchronized.

## Database Health Checklist

- The `DATABASE_URL` env var must be set before the BFF starts (enforced by `Services/bff/app/config.py`).
- The default admin account is created by `Services/bff/app/seed.py` on startup if missing.
- Temporal and orchestrator services interact with PostgreSQL through the BFF API only; no other service writes directly to the database.
- Use `scripts/dev-seed-accounts.ps1` for local development seeding; it targets PostgreSQL, not SQLite.

## Syncing Documentation

- Whenever the SQLAlchemy models change, update this document and run `npm run db:push` (see project tooling) to verify migrations remain reversible.
- Legacy SQLite notes now live in `docs/database-implementation.md (Archived)` for historical reference only.

