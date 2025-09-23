# SwAIvyn Comprehensive Network Diagrams

This document provides comprehensive dataflow and API call flow diagrams for the SwAIvyn system, covering all deployment scenarios and detailed API interactions.

## 🌐 Complete System Architecture Overview

```mermaid
graph TB
    subgraph "User Interface Layer"
        WEB[Web Browser]
        MOBILE[Mobile Browser]
        API_CLIENT[API Client]
    end
    
    subgraph "Frontend Layer (Port 5000)"
        REACT[React 18 + TypeScript]
        VITE[Vite Dev Server]
        ZUSTAND[Zustand State Management]
    end
    
    subgraph "Backend Layer (Port 8000)"
        FASTAPI[FastAPI Python Backend]
        JWT_AUTH[JWT Authentication]
        CORS[CORS Middleware]
    end
    
    subgraph "Database Layer"
        POSTGRES[(PostgreSQL Database)]
        NEO4J[(Neo4j Graph DB)]
        QDRANT[(Qdrant Vector DB)]
    end
    
    subgraph "AI Services Layer"
        LLM_OLLAMA[Ollama LLM]
        LLM_LMSTUDIO[LM Studio]
        LLM_OPENAI[OpenAI API]
        LLM_CLAUDE[Claude API]
        VLLM[vLLM Engine]
    end
    
    subgraph "Audio Services Layer"
        FISH_TTS[Fish Speech TTS]
        WHISPER_STT[Whisper STT]
        ELEVENLABS[ElevenLabs TTS]
        TTS_ADAPTER[TTS Adapter Proxy]
    end
    
    subgraph "Workflow Layer (Optional)"
        TEMPORAL[Temporal Workflow Engine]
        ORCHESTRATOR[Orchestrator Worker]
        AGENTS[External Agents]
    end
    
    subgraph "Infrastructure Layer"
        TRAEFIK[Traefik Reverse Proxy]
        DOCKER[Docker Swarm]
        BARE_METAL[Bare Metal Services]
    end
    
    %% User Interface Connections
    WEB --> REACT
    MOBILE --> REACT
    API_CLIENT --> FASTAPI
    
    %% Frontend Connections
    REACT --> FASTAPI
    VITE --> REACT
    ZUSTAND --> REACT
    
    %% Backend Database Connections
    FASTAPI --> POSTGRES
    FASTAPI --> NEO4J
    FASTAPI --> QDRANT
    
    %% Workflow Connections
    FASTAPI --> TEMPORAL
    TEMPORAL --> ORCHESTRATOR
    ORCHESTRATOR --> LLM_OLLAMA
    ORCHESTRATOR --> LLM_LMSTUDIO
    ORCHESTRATOR --> LLM_OPENAI
    ORCHESTRATOR --> LLM_CLAUDE
    ORCHESTRATOR --> VLLM
    
    %% Audio Service Connections
    FASTAPI --> FISH_TTS
    FASTAPI --> WHISPER_STT
    FASTAPI --> ELEVENLABS
    FASTAPI --> TTS_ADAPTER
    TTS_ADAPTER --> ELEVENLABS
    
    %% External Agent Connections
    FASTAPI --> AGENTS
    TEMPORAL --> AGENTS
    
    %% Infrastructure Connections
    TRAEFIK --> FASTAPI
    TRAEFIK --> REACT
    DOCKER --> TEMPORAL
    DOCKER --> POSTGRES
    DOCKER --> NEO4J
    DOCKER --> QDRANT
    
    %% Styling
    classDef frontend fill:#e1f5fe,stroke:#0277bd,stroke-width:2px
    classDef backend fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    classDef database fill:#e8f5e8,stroke:#388e3c,stroke-width:2px
    classDef ai fill:#fff3e0,stroke:#f57c00,stroke-width:2px
    classDef audio fill:#fce4ec,stroke:#c2185b,stroke-width:2px
    classDef workflow fill:#f1f8e9,stroke:#689f38,stroke-width:2px
    classDef infra fill:#fafafa,stroke:#616161,stroke-width:2px
    
    class REACT,VITE,ZUSTAND frontend
    class FASTAPI,JWT_AUTH,CORS backend
    class POSTGRES,NEO4J,QDRANT database
    class LLM_OLLAMA,LLM_LMSTUDIO,LLM_OPENAI,LLM_CLAUDE,VLLM ai
    class FISH_TTS,WHISPER_STT,ELEVENLABS,TTS_ADAPTER audio
    class TEMPORAL,ORCHESTRATOR,AGENTS workflow
    class TRAEFIK,DOCKER,BARE_METAL infra
```

## 🔄 Complete Data Flow Architecture

```mermaid
flowchart TD
    subgraph "Client Layer"
        U[User Interaction]
        BROWSER[Web Browser]
    end
    
    subgraph "Presentation Layer"
        UI[React Components]
        STATE[Zustand Store]
        HOOKS[Custom Hooks]
        AUTH_HOOK[useEffectiveUser Hook]
    end
    
    subgraph "API Layer"
        REST[REST API Endpoints]
        MIDDLEWARE[FastAPI Middleware]
        VALIDATION[Request Validation]
    end
    
    subgraph "Business Logic Layer"
        AUTH_SERVICE[Authentication Service]
        CHAT_SERVICE[Chat Service]
        CHARACTER_SERVICE[Character Service]
        AGENT_SERVICE[Agent Service]
        USER_SERVICE[User Service]
        MEMORY_SERVICE[Memory Service]
    end
    
    subgraph "Data Processing Layer"
        WORKFLOW_ENGINE[Temporal Workflows]
        LLM_ORCHESTRATOR[LLM Orchestrator]
        TTS_PROCESSOR[TTS Processor]
        VECTOR_PROCESSOR[Vector Processor]
        MEMORY_PROCESSOR[Memory Processor]
    end
    
    subgraph "External Integration Layer"
        LLM_APIS[LLM APIs]
        TTS_APIS[TTS APIs]
        EXTERNAL_AGENTS[External Agents]
        WEBHOOKS[Webhook Handlers]
    end
    
    subgraph "Data Storage Layer"
        USER_DATA[(User & Auth Data)]
        CHAT_DATA[(Conversations)]
        VECTOR_DATA[(Vector Embeddings)]
        GRAPH_DATA[(Memory Relationships)]
        FILE_DATA[(File Storage)]
    end
    
    %% Data Flow Connections
    U --> BROWSER
    BROWSER --> UI
    UI --> STATE
    UI --> HOOKS
    HOOKS --> AUTH_HOOK
    
    STATE --> REST
    HOOKS --> REST
    AUTH_HOOK --> REST
    
    REST --> MIDDLEWARE
    MIDDLEWARE --> VALIDATION
    VALIDATION --> AUTH_SERVICE
    VALIDATION --> CHAT_SERVICE
    VALIDATION --> CHARACTER_SERVICE
    VALIDATION --> AGENT_SERVICE
    VALIDATION --> USER_SERVICE
    VALIDATION --> MEMORY_SERVICE
    
    CHAT_SERVICE --> WORKFLOW_ENGINE
    WORKFLOW_ENGINE --> LLM_ORCHESTRATOR
    LLM_ORCHESTRATOR --> LLM_APIS
    
    CHAT_SERVICE --> TTS_PROCESSOR
    TTS_PROCESSOR --> TTS_APIS
    
    MEMORY_SERVICE --> VECTOR_PROCESSOR
    VECTOR_PROCESSOR --> VECTOR_DATA
    
    MEMORY_SERVICE --> MEMORY_PROCESSOR
    MEMORY_PROCESSOR --> GRAPH_DATA
    
    AGENT_SERVICE --> EXTERNAL_AGENTS
    EXTERNAL_AGENTS --> WEBHOOKS
    
    AUTH_SERVICE --> USER_DATA
    CHAT_SERVICE --> CHAT_DATA
    CHARACTER_SERVICE --> CHAT_DATA
    USER_SERVICE --> USER_DATA
    MEMORY_SERVICE --> FILE_DATA
    
    %% Return flows
    USER_DATA --> AUTH_SERVICE
    CHAT_DATA --> CHAT_SERVICE
    VECTOR_DATA --> MEMORY_SERVICE
    GRAPH_DATA --> MEMORY_SERVICE
    FILE_DATA --> MEMORY_SERVICE
    
    LLM_APIS --> LLM_ORCHESTRATOR
    TTS_APIS --> TTS_PROCESSOR
    EXTERNAL_AGENTS --> AGENT_SERVICE
    
    %% Styling
    classDef client fill:#e3f2fd,stroke:#1976d2,stroke-width:2px
    classDef presentation fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    classDef api fill:#e8f5e8,stroke:#388e3c,stroke-width:2px
    classDef business fill:#fff3e0,stroke:#f57c00,stroke-width:2px
    classDef processing fill:#fce4ec,stroke:#c2185b,stroke-width:2px
    classDef external fill:#f1f8e9,stroke:#689f38,stroke-width:2px
    classDef storage fill:#fafafa,stroke:#616161,stroke-width:2px
    
    class U,BROWSER client
    class UI,STATE,HOOKS,AUTH_HOOK presentation
    class REST,MIDDLEWARE,VALIDATION api
    class AUTH_SERVICE,CHAT_SERVICE,CHARACTER_SERVICE,AGENT_SERVICE,USER_SERVICE,MEMORY_SERVICE business
    class WORKFLOW_ENGINE,LLM_ORCHESTRATOR,TTS_PROCESSOR,VECTOR_PROCESSOR,MEMORY_PROCESSOR processing
    class LLM_APIS,TTS_APIS,EXTERNAL_AGENTS,WEBHOOKS external
    class USER_DATA,CHAT_DATA,VECTOR_DATA,GRAPH_DATA,FILE_DATA storage
```

## 📊 Complete API Call Flow Diagram

```mermaid
sequenceDiagram
    participant U as User
    participant FE as Frontend (React)
    participant MW as Middleware
    participant AUTH as Auth Service
    participant BFF as FastAPI Backend
    participant DB as PostgreSQL
    participant TEMP as Temporal
    participant ORCH as Orchestrator
    participant LLM as LLM Engine
    participant TTS as TTS Service
    participant VDB as Vector DB
    participant NEO as Neo4j
    
    Note over U,NEO: 🔐 Authentication Flow
    U->>FE: Login (username/password)
    FE->>BFF: POST /api/auth/login
    BFF->>MW: Validate request
    MW->>AUTH: Authenticate user
    AUTH->>DB: Verify credentials
    DB-->>AUTH: User data
    AUTH-->>MW: JWT token
    MW-->>BFF: Authenticated
    BFF-->>FE: JWT token + user info
    FE->>FE: Store token in Zustand
    
    Note over U,NEO: 👤 User Data Flow
    U->>FE: Load dashboard
    FE->>BFF: GET /api/user/{user_id} (with JWT)
    BFF->>MW: Validate JWT
    MW->>AUTH: Verify token
    AUTH-->>MW: Valid user
    MW->>BFF: User context
    BFF->>DB: Get user data
    DB-->>BFF: User profile
    BFF-->>FE: User data
    FE->>FE: Update Zustand state
    
    Note over U,NEO: 💬 Chat Settings Flow
    U->>FE: Configure chat settings
    FE->>BFF: PUT /api/chat/settings/{user_id}
    BFF->>MW: Validate request + JWT
    MW->>BFF: Authorized
    BFF->>DB: Save settings
    DB-->>BFF: Settings saved
    BFF-->>FE: Success response
    FE->>FE: Update settings UI
    
    U->>FE: Load chat settings
    FE->>BFF: GET /api/chat/settings/{user_id}
    BFF->>MW: Validate JWT
    MW->>BFF: Authorized
    BFF->>DB: Get settings
    DB-->>BFF: User settings
    BFF-->>FE: Settings data
    FE->>FE: Display settings
    
    Note over U,NEO: 📂 Conversation Management Flow
    U->>FE: Load conversations
    FE->>BFF: GET /api/conversation/user/{user_id}
    BFF->>MW: Validate JWT
    MW->>BFF: Authorized
    BFF->>DB: Get conversations
    DB-->>BFF: Conversation list
    BFF-->>FE: Conversations
    FE->>FE: Display conversation list
    
    Note over U,NEO: 🤖 Character Management Flow
    U->>FE: Load characters
    FE->>BFF: GET /api/characters
    BFF->>MW: Validate JWT
    MW->>BFF: Authorized
    BFF->>DB: Get characters
    DB-->>BFF: Character list
    BFF-->>FE: Characters data
    FE->>FE: Display characters
    
    Note over U,NEO: 🔧 Settings Management Flow
    U->>FE: Update LLM settings
    FE->>BFF: PUT /api/chat/settings/{user_id}
    BFF->>MW: Validate JWT
    MW->>BFF: Authorized
    BFF->>DB: Update settings
    DB-->>BFF: Settings saved
    BFF-->>FE: Success response
    FE->>FE: Update UI state
    
    Note over U,NEO: 📤 Agent Catalog Flow
    U->>FE: View available agents
    FE->>BFF: GET /api/agents/catalog
    BFF->>MW: Validate JWT
    MW->>BFF: Authorized
    BFF->>External: Proxy to orchestrator
    External-->>BFF: Agent catalog
    BFF-->>FE: Available agents
    FE->>FE: Display agent list
    
    Note over U,NEO: 🔧 LLM Settings Flow
    U->>FE: Get LLM settings
    FE->>BFF: GET /api/settings/llm
    BFF->>MW: Validate JWT
    MW->>BFF: Authorized
    BFF->>DB: Get LLM configuration
    DB-->>BFF: LLM settings
    BFF-->>FE: Settings data
    
    U->>FE: Update LLM settings
    FE->>BFF: PUT /api/settings/llm
    BFF->>MW: Validate request
    MW->>BFF: Authorized
    BFF->>DB: Save settings
    DB-->>BFF: Settings saved
    BFF-->>FE: Success response
```

## 🖥️ Deployment Architecture Comparison

```mermaid
graph TB
    subgraph "Bare Metal Windows Deployment"
        BM_USER[User] --> BM_FE[Frontend :5000]
        BM_USER --> BM_BE[Backend :8000]
        BM_FE --> BM_BE
        BM_BE --> BM_PG[(PostgreSQL :5432)]
        BM_BE --> BM_NEO[(Neo4j :7474)]
        BM_BE --> BM_QD[(Qdrant :6333)]
        BM_BE --> BM_TEMP[Temporal :7233]
        BM_BE --> BM_TTS[Fish TTS :8081]
        BM_TEMP --> BM_ORCH[Orchestrator]
        BM_ORCH --> BM_LLM[LLM Services]
    end
    
    subgraph "Docker Swarm Deployment"
        DS_USER[User] --> DS_TRAEFIK[Traefik :80]
        DS_TRAEFIK --> DS_FE[Frontend Container]
        DS_TRAEFIK --> DS_BE[Backend Container]
        DS_BE --> DS_PG[(PostgreSQL Container)]
        DS_BE --> DS_NEO[(Neo4j Container)]
        DS_BE --> DS_QD[(Qdrant Container)]
        DS_BE --> DS_TEMP[Temporal Container]
        DS_BE --> DS_TTS[TTS Container]
        DS_TEMP --> DS_ORCH[Orchestrator Container]
        DS_ORCH --> DS_LLM[External LLM Services]
    end
    
    subgraph "Cloud Development (Replit)"
        CD_USER[User] --> CD_FE[Frontend :5000]
        CD_FE --> CD_BE[Backend :8000]
        CD_BE --> CD_PG[(Managed PostgreSQL)]
        CD_BE --> CD_LLM[External LLM APIs]
        
    Note over CD_USER,CD_LLM: Simplified cloud deployment with core services only
    end
    
    %% Styling
    classDef baremetal fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    classDef docker fill:#e3f2fd,stroke:#1565c0,stroke-width:2px
    classDef cloud fill:#fff3e0,stroke:#ef6c00,stroke-width:2px
    
    class BM_USER,BM_FE,BM_BE,BM_PG,BM_NEO,BM_QD,BM_TEMP,BM_TTS,BM_ORCH,BM_LLM baremetal
    class DS_USER,DS_TRAEFIK,DS_FE,DS_BE,DS_PG,DS_NEO,DS_QD,DS_TEMP,DS_TTS,DS_ORCH,DS_LLM docker
    class CD_USER,CD_FE,CD_BE,CD_PG,CD_LLM cloud
```

## 📱 Frontend Authentication & State Flow

```mermaid
stateDiagram-v2
    [*] --> Loading
    Loading --> Unauthenticated : No token
    Loading --> Authenticated : Valid token
    
    Unauthenticated --> LoginForm : User clicks login
    LoginForm --> Authenticating : Submit credentials
    Authenticating --> Authenticated : Success
    Authenticating --> LoginError : Failed
    LoginError --> LoginForm : Retry
    
    Authenticated --> Dashboard : Load main app
    Dashboard --> Chat : Navigate to chat
    Dashboard --> Settings : Navigate to settings
    Dashboard --> Characters : Navigate to characters
    Dashboard --> Agents : Navigate to agents
    Dashboard --> Memory : Navigate to memory
    
    Chat --> ChatLoading : Send message
    ChatLoading --> Chat : Response received
    
    Settings --> SettingsSaving : Update settings
    SettingsSaving --> Settings : Settings saved
    
    Characters --> CharacterForm : Create/Edit character
    CharacterForm --> Characters : Character saved
    
    Agents --> AgentForm : Register agent
    AgentForm --> Agents : Agent registered
    
    Memory --> MemoryViewer : View memories
    MemoryViewer --> Memory : Back to list
    
    Authenticated --> Unauthenticated : Logout
    Authenticated --> Unauthenticated : Token expired
```

## 🔄 Status Polling Flow

```mermaid
sequenceDiagram
    participant FE as Frontend
    participant BFF as FastAPI Backend
    participant DB as Database
    participant EXT as External Services
    
    Note over FE,EXT: Dashboard Status Polling
    loop Every 30 seconds
        FE->>BFF: GET /api/dashboard/status
        BFF->>DB: Check service health
        BFF->>EXT: Ping external services
        EXT-->>BFF: Service status
        DB-->>BFF: Database status
        BFF-->>FE: Status summary
        FE->>FE: Update dashboard indicators
    end
    
    Note over FE,EXT: LLM Health Monitoring
    loop Every 60 seconds
        FE->>BFF: GET /api/llm/health
        BFF->>EXT: Check LLM endpoints
        EXT-->>BFF: Model availability
        BFF-->>FE: LLM status
        FE->>FE: Update model selectors
    end
```

## 📊 Performance Monitoring Flow

```mermaid
flowchart TD
    subgraph "Monitoring Sources"
        APP_METRICS[Application Metrics]
        DB_METRICS[Database Metrics]
        LLM_METRICS[LLM Response Times]
        TTS_METRICS[TTS Generation Times]
        AGENT_METRICS[Agent Task Metrics]
    end
    
    subgraph "Collection Layer"
        HEALTH_ENDPOINTS[Health Endpoints]
        LOG_AGGREGATION[Log Aggregation]
        METRICS_COLLECTOR[Metrics Collector]
    end
    
    subgraph "Processing Layer"
        TEMPORAL_ANALYTICS[Temporal Analytics]
        PERFORMANCE_ANALYZER[Performance Analyzer]
        ALERT_PROCESSOR[Alert Processor]
    end
    
    subgraph "Presentation Layer"
        DASHBOARD[Admin Dashboard]
        STATUS_INDICATORS[Status Indicators]
        ALERT_NOTIFICATIONS[Alert Notifications]
    end
    
    APP_METRICS --> HEALTH_ENDPOINTS
    DB_METRICS --> METRICS_COLLECTOR
    LLM_METRICS --> LOG_AGGREGATION
    TTS_METRICS --> LOG_AGGREGATION
    AGENT_METRICS --> METRICS_COLLECTOR
    
    HEALTH_ENDPOINTS --> PERFORMANCE_ANALYZER
    LOG_AGGREGATION --> TEMPORAL_ANALYTICS
    METRICS_COLLECTOR --> PERFORMANCE_ANALYZER
    
    TEMPORAL_ANALYTICS --> DASHBOARD
    PERFORMANCE_ANALYZER --> STATUS_INDICATORS
    ALERT_PROCESSOR --> ALERT_NOTIFICATIONS
    
    PERFORMANCE_ANALYZER --> ALERT_PROCESSOR
    TEMPORAL_ANALYTICS --> ALERT_PROCESSOR
    
    %% Styling
    classDef source fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    classDef collection fill:#e3f2fd,stroke:#1565c0,stroke-width:2px
    classDef processing fill:#fff3e0,stroke:#ef6c00,stroke-width:2px
    classDef presentation fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    
    class APP_METRICS,DB_METRICS,LLM_METRICS,TTS_METRICS,AGENT_METRICS source
    class HEALTH_ENDPOINTS,LOG_AGGREGATION,METRICS_COLLECTOR collection
    class TEMPORAL_ANALYTICS,PERFORMANCE_ANALYZER,ALERT_PROCESSOR processing
    class DASHBOARD,STATUS_INDICATORS,ALERT_NOTIFICATIONS presentation
```

---

## 📋 Summary

These comprehensive diagrams provide a complete view of SwAIvyn's architecture including:

1. **System Architecture Overview** - High-level component relationships
2. **Complete Data Flow** - End-to-end data processing flow
3. **API Call Flow** - Detailed request/response patterns for all major operations
4. **Deployment Comparison** - Architecture differences across deployment types
5. **Frontend State Flow** - Client-side authentication and navigation flow
6. **Real-time Communication** - WebSocket and streaming interactions
7. **Performance Monitoring** - System health and metrics collection

These diagrams serve as a reference for:
- **Developers** understanding the system architecture
- **DevOps** planning deployments and scaling
- **Troubleshooting** system issues and performance bottlenecks
- **Integration** planning for external agents and services
- **Documentation** for comprehensive system understanding