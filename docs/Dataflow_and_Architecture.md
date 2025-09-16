# Dataflow & Architecture Overview

## Overview
This document consolidates the primary SwAIvyn data flows, diagrams, and persistence strategies. It combines the original architecture diagrams with the updated narrative describing how data moves through the system so that developers have a single source of truth.

## Core Data Entities
1. **Users** – Authentication, preferences, and ownership context for all resources.
2. **Folders** – Hierarchical organization of conversations with optional parent-child relationships.
3. **Conversations** – Chat sessions that contain metadata and link to chat index entries.
4. **Chat Index** – Lightweight references to message files with role and timestamp metadata for efficient lookup.
5. **Memories** – User-specific knowledge used for personalization and long-term recall.
6. **Vector Embeddings** – Semantic representations of text stored in SQLite-VSS for similarity search.
7. **Graph Relationships** – Memory relationships stored in Neo4j for graph queries and visualization.
8. **Settings** – Application and user preferences that control runtime behaviour.

## Data Persistence Strategy
1. **SQLite (WAL mode)** – Primary relational store for structured data such as users, folders, conversations, indices, and settings.
2. **SQLite-VSS Extension** – Embedding storage that enables HNSW-powered semantic search inside the main SQLite database.
3. **Neo4j Graph Database** – Persists memory nodes and relationships (embedded or remote deployment).
4. **File System** – Stores chat message JSON payloads and binary assets referenced by the database.
5. **In-Memory Cache** – Caches frequently accessed data to reduce I/O and improve responsiveness.

## Database Interactions
### Reading Data
```csharp
var folders = await _dbContext.Folders
    .Where(f => f.UserId == userId)
    .OrderBy(f => f.Name)
    .ToListAsync();

var conversations = await _dbContext.Conversations
    .Where(c => c.UserId == userId)
    .OrderByDescending(c => c.LastOpenUtc)
    .ToListAsync();

var conversation = await _dbContext.Conversations
    .Where(c => c.UserId == userId)
    .OrderByDescending(c => c.LastOpenUtc)
    .FirstOrDefaultAsync();

// Generate embedding for the query
var queryEmbedding = await _embeddingService.EmbedTextAsync(query);

// Search the vector store
var hits = await _vectorStore.SearchAsync(queryEmbedding, limit, scope);
```

### Writing Data
```csharp
var folder = new Folder
{
    Id = Guid.NewGuid(),
    UserId = userId,
    Name = name,
    ParentId = parentId,
    CreatedUtc = DateTime.UtcNow
};
_dbContext.Folders.Add(folder);
await _dbContext.SaveChangesAsync();

var conversation = new Conversation
{
    Id = Guid.NewGuid(),
    UserId = userId,
    FolderId = folderId,
    Title = title,
    CreatedUtc = DateTime.UtcNow,
    LastOpenUtc = DateTime.UtcNow
};
_dbContext.Conversations.Add(conversation);
await _dbContext.SaveChangesAsync();

var timestamp = DateTime.UtcNow;
var fileName = $"{timestamp:yyyyMMdd_HHmmss}.json";
var filePath = Path.Combine(conversationDir, fileName);
var message = new { role, content, timestamp = timestamp.ToString("o") };
await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(message));

var chatIndex = new ChatIndex
{
    Id = Guid.NewGuid(),
    ConversationId = conversationId,
    Role = role,
    FilePath = Path.Combine("sessions", conversationId.ToString(), fileName),
    CreatedUtc = timestamp
};
_dbContext.ChatIndices.Add(chatIndex);
await _dbContext.SaveChangesAsync();

var embedding = await _embeddingService.EmbedTextAsync(text);
var vectorStoreSuccess = await _vectorStore.StoreVectorAsync(id, embedding, metadata);
var node = await _neo4jService.CreateNodeAsync(new List<string> { "Memory" }, new()
{
    { "id", id.ToString() },
    { "text", text }
});
```

## System Topologies
### Hybrid Development Topology
```mermaid
flowchart LR
  subgraph Host
    FE[Frontend (Vite 5173)] -->|REST/WS| BFF[BFF (FastAPI 5000)]
    BFF -->|Temporal Client| TMP[Temporal Frontend (7233)]
    WKR[Orchestrator Worker] -.runs via dev-orchestrator.ps1.- TMP
    BFF -->|JDBC/asyncpg| PG[(Postgres)]
    BFF -->|HTTP| QD[(Qdrant)]
    BFF -->|Bolt| NEO[(Neo4j)]
    FE -->|HTTP| TTS[(Fish Speech TTS 8081)]
  end

  subgraph Docker (infra)
    PG
    QD
    NEO
    STT[Whisper STT 9000]
    TMP
    ADPT[11Labs Adapter 8082]
  end

  WKR -->|HTTP (engine-specific)| LLM1[LM Studio /v1]
  WKR -->|HTTP| OLL[Ollama /api]
  WKR -->|HTTP| OAI[OpenAI /v1]
  WKR -->|HTTP| CLD[Claude /v1]
  WKR -->|HTTP| VLLM[vLLM /v1]

  classDef host fill:#f0fff4,stroke:#16a34a,stroke-width:1px;
  classDef docker fill:#f8fafc,stroke:#334155,stroke-width:1px;
  class Host host;
  class Docker docker;
```

**Notes**
- Apps run on host with hot reload; infrastructure remains in Docker.
- Fish Speech TTS can run on host (`-UseHostTTS`) or inside Docker (profile `tts`).
- Compose profiles keep app-tier images optional: `orchestrator`, `tts`, `tts-adapter`.

### Network Boundaries & Ports
```mermaid
flowchart TB
  subgraph Browser
    VITE[Vite Dev 5173]
  end
  subgraph Host
    BFF[BFF FastAPI 5000]
    WORKER[Orchestrator Worker]
    TTS[(Fish Speech 8081)]
  end
  subgraph Docker
    TEMPORAL[Temporal 7233]
    PG[(Postgres 5432)]
    QDRANT[(Qdrant 6333)]
    NEO[(Neo4j 7474/7687)]
    STT[(Whisper 9000)]
    ADPT[(11Labs 8082)]
  end

  VITE -- REST/WS --> BFF
  BFF -- Temporal client --> TEMPORAL
  WORKER -. connects .-> TEMPORAL
  BFF --> PG
  BFF --> QDRANT
  BFF --> NEO
  VITE --> TTS
```

### Environment Keys
- `FISHSPEECH_URL` — `http://localhost:8081` for host TTS or `http://tts:8081` for container TTS.
- `OLLAMA_HOST`, `LMSTUDIO_HOST` — host URLs when the worker runs on the host.
- `OPENAI_API_KEY`, `CLAUDE_API_KEY` — required to enable those engines.

### Profiles & Scripts
- Compose profiles: `orchestrator`, `tts`, `tts-adapter`.
- Scripts:
  - `scripts/infra-up.ps1` — start only infrastructure (with optional profiles).
  - `scripts/dev-start.ps1 -UseHostTTS` — start host apps plus host TTS window.
  - `scripts/dev-tts.ps1` — run Fish Speech on host.
  - `scripts/dev-stop.ps1` — stop host apps (and host TTS).
  - `scripts/update.ps1` — update dependencies and container images.

## Application Flows
### Startup Flow
```mermaid
sequenceDiagram
    autonumber
    User->>UI: Launch SwAIvyn.exe
    UI-->>LocalStore: Load LastOpenConversation()
    alt first run OR user hit "New Chat"
        UI->>LocalStore: CreateConversation()
        LocalStore-->>UI: {conversationId}
    end
    User->>UI: starts typing
    UI->>ChatService: AppendMessage(conversationId, role="user", text)
    ChatService->>FileWriter: append {convId}/{timestamp}.json
    ChatService->>ChatIndex: INSERT row
    ChatService->>BrainRouter: maybeEmbedAndSync(scope)
```

### Folder & Conversation Management Flow
```mermaid
sequenceDiagram
    participant User
    participant UI
    participant FolderController
    participant FolderService
    participant ConversationService
    participant Database
    participant FileSystem

    User->>UI: Create folder
    UI->>FolderController: POST /api/folder
    FolderController->>FolderService: CreateFolderAsync()
    FolderService->>Database: Insert folder
    Database-->>FolderService: Confirmation
    FolderService-->>FolderController: Result
    FolderController-->>UI: Success/failure

    User->>UI: Create conversation
    UI->>ConversationController: POST /api/conversation
    ConversationController->>ConversationService: CreateConversationAsync()
    ConversationService->>Database: Insert conversation
    ConversationService->>FileSystem: Create directory
    Database-->>ConversationService: Confirmation
    ConversationService-->>ConversationController: Result
    ConversationController-->>UI: Success/failure
```

### Chat & Messaging Flows
#### Per-User Chat Message Flow
```mermaid
sequenceDiagram
  autonumber
  participant U as User
  participant FE as Frontend (Settings/Chat)
  participant B as BFF (FastAPI)
  participant DB as Postgres
  participant T as Temporal
  participant W as Worker (Orchestrator)
  participant E as Engine API (LM Studio/Ollama/OpenAI/Claude/vLLM)
  participant S as TTS (Fish Speech/Adapter)

  U->>FE: Select Engine + Model (Settings)
  FE->>B: PUT /api/chat/settings/{userId}\n{ llmEngine, llmModel, enabledEngines, engineModels }
  B->>DB: Upsert chat_settings for user
  DB-->>B: OK
  B-->>FE: 200 { message: "Chat settings updated successfully." }

  U->>FE: Send Message
  FE->>B: POST /api/conversation/chat\n{ message, engine, model }
  B->>B: Resolve per-user connections\n(Connections + DB)
  B->>T: Start per-engine workflow\n(ReplyWorkflow_Engine)
  T->>W: Execute generate_reply(engine, model, conn)
  W->>E: HTTP call to selected engine\n(no cycling/fallback)
  E-->>W: LLM reply
  W->>S: (optional) synthesize_tts(text)
  S-->>W: audio url (optional)
  W-->>T: { reply_text, tts_url }
  T-->>B: Result
  B-->>FE: 200 { response, tts_url }
```

#### Chat Interaction Flow
```mermaid
sequenceDiagram
    participant User
    participant UI
    participant ConversationController
    participant ConversationService
    participant LLMConnector
    participant Database
    participant FileSystem

    User->>UI: Send message
    UI->>ConversationController: POST /api/conversation/message
    ConversationController->>ConversationService: AppendMessageAsync()
    ConversationService->>FileSystem: Write message JSON
    ConversationService->>Database: Insert chat index
    ConversationService->>LLMConnector: GetResponse()
    LLMConnector-->>ConversationService: AI response
    ConversationService->>FileSystem: Write AI response JSON
    ConversationService->>Database: Insert chat index
    ConversationService-->>ConversationController: Complete response
    ConversationController-->>UI: Update chat
    UI->>User: Display message
```

#### LLM Interaction Flow
```mermaid
sequenceDiagram
    participant User
    participant UI
    participant ConversationController
    participant AiChatService
    participant LlmConnectorService
    participant SettingsService
    participant ConversationService
    participant OllamaAPI
    participant LMStudioAPI

    User->>UI: Send message
    UI->>ConversationController: POST /api/conversation/chat
    ConversationController->>AiChatService: GenerateAndStoreResponseAsync()

    AiChatService->>ConversationService: AppendMessageAsync(userId, conversationId, "user", message)
    ConversationService->>AiChatService: Success

    AiChatService->>SettingsService: GetCurrentLlmSettingsAsync(userId)
    SettingsService-->>AiChatService: {engine, model}

    AiChatService->>LlmConnectorService: GenerateResponseAsync(message, engine, model, userId)

    alt Using Ollama
        LlmConnectorService->>OllamaAPI: POST {ollamaApiUrl}/v1/completions
        OllamaAPI-->>LlmConnectorService: AI response
    else Using LM Studio
        LlmConnectorService->>LMStudioAPI: POST {lmStudioApiUrl}/generate
        LMStudioAPI-->>LlmConnectorService: AI response
    end

    LlmConnectorService-->>AiChatService: AI response

    AiChatService->>ConversationService: AppendMessageAsync(userId, conversationId, "assistant", aiResponse)
    ConversationService->>AiChatService: Success

    AiChatService-->>ConversationController: AI response
    ConversationController-->>UI: Update chat
    UI->>User: Display message
```

### Settings & Authentication Flows
#### Settings Save & Rehydrate (UI)
```mermaid
sequenceDiagram
  participant FE as Frontend (Settings)
  participant B as BFF
  participant DB as Postgres

  FE->>B: PUT /api/chat/settings/{userId}
  B->>DB: Upsert chat_settings
  DB-->>B: OK
  B-->>FE: 200
  FE->>B: GET /api/chat/settings/{userId}
  B-->>FE: JSON { llmEngine, llmModel, enabledEngines, engineModels }
  FE->>FE: Update selects + show toast
```

#### Authentication (Login) Flow
```mermaid
sequenceDiagram
  autonumber
  participant U as User
  participant FE as Frontend
  participant B as BFF (FastAPI)
  participant DB as Postgres

  U->>FE: Submit username/password
  FE->>B: POST /api/auth/login {identifier, password}
  B->>DB: SELECT users WHERE username/email
  DB-->>B: Row (id, password_hash, role)
  B-->>FE: 200 {access_token, user}
  FE->>FE: Save token (optional)
  FE->>FE: Set axios Authorization header
  FE->>B: GET /api/auth/me (boot check)
  B-->>FE: Current user (id, role, prefs)
  FE->>FE: Auth gate opens; render app
```

#### Initialization & Auth Gate (UI)
```mermaid
sequenceDiagram
  participant FE as Frontend
  participant B as BFF

  FE->>FE: InitializationContext mounts
  FE->>B: GET /api/readyz (retry with backoff)
  B-->>FE: 200 {status: ready}
  FE->>B: GET /api/auth/me (if token present)
  alt Authenticated
    B-->>FE: 200 {user}
    FE->>B: GET /api/settings/llm?userId
    FE->>B: GET /api/character/user/{userId}
    FE->>FE: isInitialized=true → render app
  else Unauthenticated
    B-->>FE: 401
    FE->>FE: isInitialized=true → redirect /login
  end
```

#### Authorization Matrix (Summary)
| Resource/Action                          | User (self) | Admin |
|------------------------------------------|-------------|-------|
| GET/PUT /api/chat/settings/{userId}      | ✓ (self)    | ✓ any |
| GET/POST/PUT /api/settings/connections   | ✓ (self)    | ✓ any |
| GET /api/conversation/user/{userId}      | ✓ (self)    | ✓ any |
| GET/PUT /api/conversation/{id}/(title|folder|messages) | ✓ owner | ✓ any |
| POST /api/conversation/message           | ✓ owner     | ✓ any |
| GET /api/llm/models (userId)            | ✓ (self)    | ✓ override |

### Search & Knowledge Flows
#### Brain Search Flow
```mermaid
sequenceDiagram
    participant User
    participant UI
    participant BrainController
    participant BrainService
    participant VectorStore
    participant Neo4jService

    User->>UI: Search query
    UI->>BrainController: GET /api/brain/search
    BrainController->>BrainService: SearchAsync()
    BrainService->>VectorStore: Search vectors
    VectorStore-->>BrainService: Vector results
    BrainService->>Neo4jService: Get relationships
    Neo4jService-->>BrainService: Graph data
    BrainService-->>BrainController: Combined results
    BrainController-->>UI: Search results
    UI->>User: Display results
```

#### Neo4j Interaction Flow
```mermaid
sequenceDiagram
    participant BrainService
    participant Neo4jService
    participant ConfigService
    participant Neo4jRuntimeService
    participant Neo4jProcess
    participant Neo4jHTTP
    participant Neo4jBolt

    BrainService->>Neo4jService: StoreMemoryNode()
    Neo4jService->>ConfigService: Get Neo4j configuration
    ConfigService-->>Neo4jService: URLs and credentials
    Neo4jService->>Neo4jRuntimeService: IsAvailableAsync()
    Neo4jRuntimeService->>Neo4jHTTP: GET {neo4jHttpUrl}/
    Neo4jHTTP-->>Neo4jRuntimeService: Status

    alt Neo4j Available
        Neo4jService->>Neo4jBolt: {neo4jBoltUrl} with credentials
        Neo4jBolt-->>Neo4jService: Connection
        Neo4jService->>Neo4jBolt: CREATE (n:Memory {id: $id, text: $text})
        Neo4jBolt-->>Neo4jService: Result
    else Neo4j Not Available
        Neo4jRuntimeService->>Neo4jProcess: Start Neo4j with configuration
        Neo4jProcess-->>Neo4jRuntimeService: Started
        Neo4jService->>Neo4jBolt: Retry connection
    end

    Neo4jService-->>BrainService: Operation result
```

### Voice & Agent Flows
#### TTS Playback Pipeline
```mermaid
sequenceDiagram
  participant FE as Frontend
  participant B as BFF
  participant T as Temporal
  participant W as Worker
  participant S as TTS (Fish Speech)

  FE->>B: POST /api/conversation/chat {message, engine, model}
  B->>T: Start ReplyWorkflow_<engine>
  T->>W: generate_reply(...)
  W-->>W: reply_text
  W->>S: synthesize_tts(text)
  S-->>W: { url }
  W-->>B: { reply_text, tts_url }
  B-->>FE: 200 { response, tts_url }
  FE->>FE: audio.play(tts_url)
```

#### STT Ingestion (Voice Room – current + hooks)
```mermaid
sequenceDiagram
  participant FE as Frontend (mic)
  participant STT as Whisper Webservice (9000)
  participant B as BFF (hooks)

  FE->>STT: POST /asr?task=transcribe (audio)
  STT-->>FE: { text }
  Note over FE,B: Hook: send transcripts to /api/voice/ingest (future)
```

#### Agents / Federation Hooks (Future)
```mermaid
sequenceDiagram
  participant FE as Frontend
  participant B as BFF
  participant AG as External Agent API (FastAPI)

  FE->>B: POST /api/agents/{agent_id}/start
  B->>AG: POST /tasks { goal, context }
  AG-->>B: { task_id }
  loop Poll status
    B->>AG: GET /tasks/{task_id}
    AG-->>B: { status, result? }
  end
  B-->>FE: { status/result }
```

### Additional Architecture Views
#### LLM Routing (Strict, Per-Engine)
```mermaid
flowchart LR
  IN[POST /api/conversation/chat\n{engine, model, message}] --> VALIDATE{engine & model?}
  VALIDATE -- no --> ERR[400 Invalid engine/model]
  VALIDATE -- yes --> WF[Pick ReplyWorkflow_<engine>]
  WF --> ACT[generate_reply(engine, model, conn)]
  ACT --> CALL[HTTP call to selected engine]
  CALL --> OK{200?}
  OK -- no --> FAIL[Error: LLM request failed]
  OK -- yes --> RESP[reply_text]
```

#### Error & Retry Surfaces
- **Frontend**: Initialization retries `/api/readyz`; saves show toasts on success/failure.
- **Backend**: `/api/llm/models` falls back to empty model list; `/api/conversation/chat` returns `400` (invalid engine/model), `503` (Temporal not ready), or `500` on workflow error.
- **Worker**: Strict routing returns descriptive errors when engine calls fail.

#### Database Schema (ER)
```mermaid
erDiagram
  USERS ||--o{ CONVERSATIONS : owns
  USERS ||--o{ CHAT_SETTINGS : has
  USERS ||--o{ CONNECTION_SETTINGS : has
  USERS ||--o{ CHARACTERS : creates
  CONVERSATIONS ||--o{ MESSAGES : contains

  USERS {
    string id PK
    string username
    string email
    string password_hash
    string role
    string language
    string theme
    string default_character
    bool   is_default
  }
  CHAT_SETTINGS {
    string user_id PK,FK
    string llm_engine
    string llm_model
    text   enabled_engines
    text   engine_models
    string tts_provider
    string tts_voice_id
  }
  CONNECTION_SETTINGS {
    string user_id PK,FK
    text   OpenAiApiKey
    text   ClaudeApiKey
    text   ClaudeApiUrl
    text   OllamaApiUrl
    text   LmStudioApiUrl
    bool   EnableStreaming
    text   TtsGpu
    text   SttGpu
  }
  CHARACTERS {
    string id PK
    string user_id FK
    string name
    text   system_prompt
    text   image_path
  }
  CONVERSATIONS {
    string id PK
    string user_id FK
    string title
    string folder_id
    string created_at
    string last_updated
  }
  MESSAGES {
    string id PK
    string conversation_id FK
    string role
    text   content
    string timestamp
  }
```
