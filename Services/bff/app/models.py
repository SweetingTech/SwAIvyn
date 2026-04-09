from __future__ import annotations

from sqlalchemy import MetaData, Table, Column, String, Boolean, Text, DateTime, Float, text, ForeignKey


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

# Per-user Tamagotchi-style avatar stats (3D avatar Phase 5)
avatar_stats = Table(
    "avatar_stats",
    metadata,
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), primary_key=True),
    Column("energy", Float, nullable=False, server_default=text("80")),
    Column("mood", Float, nullable=False, server_default=text("70")),
    Column("relationship_score", Float, nullable=False, server_default=text("50")),
    Column("updated_at", DateTime(timezone=True), nullable=True),
)

# Per-user room item selections (3D avatar Phase 5)
room_items = Table(
    "room_items",
    metadata,
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), primary_key=True),
    Column("items", Text, nullable=False, server_default=text("'[]'")),
    Column("updated_at", DateTime(timezone=True), nullable=True),
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

# ---------------------------------------------------------------------------
# Phase 4: Federation & Advanced Features
# ---------------------------------------------------------------------------

# Known SwAIvyn peer instances
federation_peers = Table(
    "federation_peers",
    metadata,
    Column("id", String(64), primary_key=True),
    Column("name", String(200), nullable=False),
    Column("url", String(500), nullable=False),           # Base URL of peer instance
    Column("api_key", String(256), nullable=True),        # Hashed shared key for auth
    Column("status", String(32), nullable=False, server_default=text("'pending'")),  # pending/connected/unreachable
    Column("discovered_via", String(32), nullable=True),  # 'manual' | 'mdns' | 'broadcast'
    Column("last_seen", DateTime(timezone=True), nullable=True),
    Column("created_at", DateTime(timezone=True), nullable=False),
    Column("updated_at", DateTime(timezone=True), nullable=False),
)

# Cross-instance user-to-user and AI-to-AI messages
federated_messages = Table(
    "federated_messages",
    metadata,
    Column("id", String(64), primary_key=True),
    Column("peer_id", String(64), ForeignKey("federation_peers.id", ondelete="SET NULL"), nullable=True),
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), nullable=False),
    Column("direction", String(8), nullable=False),       # 'in' | 'out'
    Column("message_type", String(32), nullable=False),   # 'user' | 'ai_task' | 'ai_result'
    Column("from_address", String(300), nullable=True),   # "username@peer-url"
    Column("to_address", String(300), nullable=True),
    Column("subject", String(500), nullable=True),
    Column("body", Text, nullable=False),
    Column("metadata", Text, nullable=True),              # JSON: context, task details, etc.
    Column("status", String(32), nullable=False, server_default=text("'sent'")),
    Column("created_at", DateTime(timezone=True), nullable=False),
)

# IMAP email accounts
email_accounts = Table(
    "email_accounts",
    metadata,
    Column("id", String(64), primary_key=True),
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), nullable=False),
    Column("label", String(200), nullable=False),
    Column("host", String(300), nullable=False),
    Column("port", String(8), nullable=False, server_default=text("'993'")),
    Column("username", String(300), nullable=False),
    Column("password", Text, nullable=True),              # Stored encrypted
    Column("use_ssl", Boolean, nullable=False, server_default=text("true")),
    Column("last_synced", DateTime(timezone=True), nullable=True),
    Column("status", String(32), nullable=False, server_default=text("'unchecked'")),
    Column("created_at", DateTime(timezone=True), nullable=False),
)

# Mirrored/cached email messages from IMAP
email_messages = Table(
    "email_messages",
    metadata,
    Column("id", String(128), primary_key=True),          # account_id + ':' + uid
    Column("account_id", String(64), ForeignKey("email_accounts.id", ondelete="CASCADE"), nullable=False),
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), nullable=False),
    Column("mailbox", String(200), nullable=False, server_default=text("'INBOX'")),
    Column("uid", String(64), nullable=False),
    Column("subject", String(500), nullable=True),
    Column("from_addr", String(500), nullable=True),
    Column("to_addr", Text, nullable=True),
    Column("date", DateTime(timezone=True), nullable=True),
    Column("body_text", Text, nullable=True),
    Column("is_read", Boolean, nullable=False, server_default=text("false")),
    Column("flags", Text, nullable=True),                 # JSON array of IMAP flags
    Column("synced_at", DateTime(timezone=True), nullable=False),
)

# CalDAV / iCal calendar accounts
calendar_accounts = Table(
    "calendar_accounts",
    metadata,
    Column("id", String(64), primary_key=True),
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), nullable=False),
    Column("label", String(200), nullable=False),
    Column("url", String(500), nullable=False),           # CalDAV principal or .ics URL
    Column("username", String(300), nullable=True),
    Column("password", Text, nullable=True),              # Stored encrypted
    Column("type", String(16), nullable=False, server_default=text("'caldav'")),  # 'caldav' | 'ical'
    Column("color", String(16), nullable=True),
    Column("last_synced", DateTime(timezone=True), nullable=True),
    Column("status", String(32), nullable=False, server_default=text("'unchecked'")),
    Column("created_at", DateTime(timezone=True), nullable=False),
)

# Synced calendar events
calendar_events = Table(
    "calendar_events",
    metadata,
    Column("id", String(128), primary_key=True),
    Column("account_id", String(64), ForeignKey("calendar_accounts.id", ondelete="CASCADE"), nullable=False),
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), nullable=False),
    Column("uid", String(300), nullable=False),
    Column("summary", String(500), nullable=True),
    Column("description", Text, nullable=True),
    Column("location", String(500), nullable=True),
    Column("start_dt", DateTime(timezone=True), nullable=True),
    Column("end_dt", DateTime(timezone=True), nullable=True),
    Column("all_day", Boolean, nullable=False, server_default=text("false")),
    Column("recurrence", Text, nullable=True),            # iCal RRULE string
    Column("raw_ical", Text, nullable=True),              # Raw VEVENT data
    Column("synced_at", DateTime(timezone=True), nullable=False),
)

# Web browsing history (Browsh / text-based)
browse_history = Table(
    "browse_history",
    metadata,
    Column("id", String(64), primary_key=True),
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), nullable=False),
    Column("url", Text, nullable=False),
    Column("title", String(500), nullable=True),
    Column("content_text", Text, nullable=True),          # Fetched text content
    Column("visited_at", DateTime(timezone=True), nullable=False),
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
    Column("manifest", Text, nullable=False),
    Column("entry_point", String(500), nullable=True),
    Column("permissions", Text, nullable=True),
    Column("status", String(32), nullable=False, server_default=text("'installed'")),
    Column("health_endpoint", String(500), nullable=True),
    Column("health_status", String(32), nullable=True),
    Column("installed_by", String(64), ForeignKey("users.id", ondelete="SET NULL"), nullable=True),
    Column("installed_at", DateTime(timezone=True), nullable=False),
    Column("updated_at", DateTime(timezone=True), nullable=False),
)

# Push notification subscriptions (Web Push / VAPID)
push_subscriptions = Table(
    "push_subscriptions",
    metadata,
    Column("id", String(128), primary_key=True),
    Column("user_id", String(64), ForeignKey("users.id", ondelete="CASCADE"), nullable=False),
    Column("endpoint", Text, nullable=False),
    Column("p256dh", Text, nullable=False),   # Diffie-Hellman public key
    Column("auth", Text, nullable=False),      # Auth secret
    Column("created_at", DateTime(timezone=True), nullable=False),
)
