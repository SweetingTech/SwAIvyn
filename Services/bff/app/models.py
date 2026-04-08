from __future__ import annotations

from sqlalchemy import MetaData, Table, Column, String, Boolean, Text, DateTime, text, ForeignKey


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
    Column("created_at", DateTime(timezone=True), nullable=False),
    Column("last_updated", DateTime(timezone=True), nullable=False),
)

# Messages
messages = Table(
    "messages",
    metadata,
    Column("id", String(64), primary_key=True),
    Column("conversation_id", String(64), ForeignKey("conversations.id", ondelete="CASCADE"), nullable=False),
    Column("role", String(32), nullable=False),
    Column("content", Text, nullable=False),
    Column("timestamp", DateTime(timezone=True), nullable=False),
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
    Column("user_id", String(64), ForeignKey("users.id"), nullable=True),
    Column("name", String(200), nullable=True),
    Column("status", String(32), nullable=False, server_default=text("'pending'")),
    Column("meta", Text, nullable=True),
    Column("started_at", String(40), nullable=True),
    Column("finished_at", String(40), nullable=True),
    Column("updated_at", String(40), nullable=True),
)

# Runtime agent instances table - tracks active agent instances
agents = Table(
    "agents",
    metadata,
    Column("id", String(64), primary_key=True),
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), nullable=False),
    Column("name", String(300), nullable=False),
    Column("description", Text, nullable=True),
    Column("status", String(32), nullable=False, server_default=text("'pending'")),
    Column("agent_type", String(100), nullable=True),
    Column("config", Text, nullable=True),  # JSON configuration
    Column("created_at", DateTime(timezone=True), nullable=False),
    Column("updated_at", DateTime(timezone=True), nullable=False),
)

# External agent registry - tracks available agent services
agent_registry = Table(
    "agent_registry",
    metadata,
    Column("id", String(128), primary_key=True),
    Column("name", String(200), nullable=False),
    Column("description", Text, nullable=True),
    Column("capabilities", Text, nullable=True),  # JSON array
    Column("version", String(32), nullable=True),
    Column("health_endpoint", String(500), nullable=True),
    Column("api_key", String(200), nullable=True),  # For agent authentication
    Column("last_heartbeat", DateTime(timezone=True), nullable=True),
    Column("status", String(32), nullable=False, server_default=text("'available'")),
    Column("created_at", DateTime(timezone=True), nullable=False),
    Column("updated_at", DateTime(timezone=True), nullable=False),
)

# Agent tasks - user-scoped tasks for proper isolation
agent_tasks = Table(
    "agent_tasks", 
    metadata,
    Column("id", String(128), primary_key=True),
    Column("agent_id", String(128), ForeignKey("agent_registry.id", ondelete="CASCADE"), nullable=False),
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), nullable=False),  # Required for isolation
    Column("name", String(200), nullable=False),
    Column("description", Text, nullable=True),
    Column("status", String(32), nullable=False, server_default=text("'pending'")),
    Column("progress", String(16), nullable=True),  # "75%" or "3/10"
    Column("current_step", String(300), nullable=True),
    Column("input_data", Text, nullable=True),  # JSON
    Column("output_data", Text, nullable=True),  # JSON
    Column("error_message", Text, nullable=True),
    Column("priority", String(16), nullable=False, server_default=text("'normal'")),
    Column("created_at", DateTime(timezone=True), nullable=False),
    Column("started_at", DateTime(timezone=True), nullable=True),
    Column("completed_at", DateTime(timezone=True), nullable=True),
    Column("estimated_completion", DateTime(timezone=True), nullable=True),
    Column("updated_at", DateTime(timezone=True), nullable=False),
)

# Agent results - user-scoped results storage
agent_results = Table(
    "agent_results",
    metadata,
    Column("id", String(128), primary_key=True),
    Column("task_id", String(128), ForeignKey("agent_tasks.id", ondelete="CASCADE"), nullable=False),
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), nullable=False),  # Explicit user isolation
    Column("result_type", String(32), nullable=False),  # 'file', 'insight', 'metric', 'data'
    Column("name", String(300), nullable=False),
    Column("description", Text, nullable=True),
    Column("content", Text, nullable=True),  # JSON content
    Column("file_path", String(500), nullable=True),  # Path to uploaded file
    Column("file_size", String(16), nullable=True),  # File size in bytes
    Column("mime_type", String(100), nullable=True),  # MIME type for files
    Column("metadata", Text, nullable=True),  # Additional JSON metadata
    Column("created_at", DateTime(timezone=True), nullable=False),
)

# Plugin registry - tracks installed plugins
plugins = Table(
    "plugins",
    metadata,
    Column("id", String(128), primary_key=True),
    Column("name", String(200), nullable=False),
    Column("version", String(32), nullable=False),
    Column("description", Text, nullable=True),
    Column("author", String(200), nullable=True),
    Column("manifest", Text, nullable=False),  # Full JSON manifest
    Column("entry_point", String(500), nullable=True),  # URL or command
    Column("permissions", Text, nullable=True),  # JSON array of permission strings
    Column("status", String(32), nullable=False, server_default=text("'installed'")),  # installed, enabled, disabled, error
    Column("health_endpoint", String(500), nullable=True),
    Column("health_status", String(32), nullable=True),  # healthy, unhealthy, unknown
    Column("installed_by", String(64), ForeignKey("users.id", ondelete="SET NULL"), nullable=True),
    Column("installed_at", DateTime(timezone=True), nullable=False),
    Column("updated_at", DateTime(timezone=True), nullable=False),
)
