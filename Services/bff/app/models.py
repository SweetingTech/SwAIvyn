from __future__ import annotations

from sqlalchemy import MetaData, Table, Column, String, Boolean, Text, text, ForeignKey


metadata = MetaData()

users = Table(
    "users",
    metadata,
    Column("id", String(64), primary_key=True),
    Column("username", String(100), nullable=False, unique=True),
    # Email can be optional; keep unique so non-null emails remain unique
    Column("email", String(200), nullable=True, unique=True),
    Column("password_hash", String(200), nullable=True),
    Column("role", String(32), nullable=False, server_default=text("'user'")),
    Column("language", String(8), nullable=False, server_default=text("'en'")),
    Column("theme", String(16), nullable=False, server_default=text("'light'")),
    Column("default_character", String(100), nullable=True),
    Column("is_default", Boolean, nullable=False, server_default=text("false")),
    Column("pin_hash", String(200), nullable=True),
    Column("recovery_codes_hash", Text, nullable=True),
)

# Per-user chat settings (persisted)
chat_settings = Table(
    "chat_settings",
    metadata,
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), primary_key=True),
    Column("llm_engine", String(32), nullable=False, server_default=text("'ollama'")),
    Column("llm_model", String(200), nullable=True),
    Column("tts_provider", String(64), nullable=True),
    Column("tts_voice_id", String(100), nullable=True),
    # JSON blobs stored as text for portability (SQLite)
    Column("enabled_engines", Text, nullable=True),
    Column("engine_models", Text, nullable=True),
)

# Per-user connection settings
connection_settings = Table(
    "connection_settings",
    metadata,
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), primary_key=True),
    Column("OpenAiApiKey", Text, nullable=True),
    Column("ClaudeApiKey", Text, nullable=True),
    Column("ClaudeApiUrl", Text, nullable=True),
    Column("OllamaApiUrl", Text, nullable=True),
    Column("LmStudioApiUrl", Text, nullable=True),
    Column("EnableStreaming", Boolean, nullable=False, server_default=text("true")),
    Column("TtsGpu", String(8), nullable=True),
    Column("SttGpu", String(8), nullable=True),
)

# Characters
characters = Table(
    "characters",
    metadata,
    Column("id", String(100), primary_key=True),
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), nullable=True),  # null means shared/global
    Column("name", String(200), nullable=False),
    Column("system_prompt", Text, nullable=True),
    Column("image_path", Text, nullable=True),
)

# Conversations
conversations = Table(
    "conversations",
    metadata,
    Column("id", String(64), primary_key=True),
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), nullable=False),
    Column("title", String(300), nullable=False),
    Column("folder_id", String(64), nullable=True),
    Column("created_at", String(40), nullable=False),
    Column("last_updated", String(40), nullable=False),
)

# Messages
messages = Table(
    "messages",
    metadata,
    Column("id", String(64), primary_key=True),
    Column("conversation_id", String(64), ForeignKey("conversations.id", ondelete="CASCADE"), nullable=False),
    Column("role", String(32), nullable=False),
    Column("content", Text, nullable=False),
    Column("timestamp", String(40), nullable=False),
)

# Workflow definitions (global)
workflows = Table(
    "workflows",
    metadata,
    Column("id", String(64), primary_key=True),
    Column("name", String(200), nullable=False),
    Column("version", String(32), nullable=False, server_default=text("'1'")),
    Column("definition", Text, nullable=False),  # JSON or YAML as text
)

# Agent runtime state (persisted)
agent_status = Table(
    "agent_status",
    metadata,
    Column("id", String(100), primary_key=True),
    Column("user_id", String(64), ForeignKey("users.id", ondelete="SET NULL"), nullable=True),
    Column("name", String(200), nullable=True),
    Column("status", String(32), nullable=False, server_default=text("'pending'")),
    Column("meta", Text, nullable=True),
    Column("started_at", String(40), nullable=True),
    Column("finished_at", String(40), nullable=True),
    Column("updated_at", String(40), nullable=True),
)

# Append-only log of agent status transitions / events
agent_events = Table(
    "agent_events",
    metadata,
    Column("id", String(64), primary_key=True),
    Column("agent_id", String(100), ForeignKey("agent_status.id", ondelete="CASCADE"), nullable=False),
    Column("timestamp", String(40), nullable=False),
    Column("status", String(32), nullable=False),
    Column("message", Text, nullable=True),
    Column("meta", Text, nullable=True),
)
