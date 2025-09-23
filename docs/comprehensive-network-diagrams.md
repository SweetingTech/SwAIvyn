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

## 🔄 Hybrid Development Architecture

```mermaid
flowchart TD
    subgraph "Client Layer"
        U[User Interaction]
        BROWSER[Web Browser]
    end
    
    subgraph "Host Services (Hot Reload)"
        FRONTEND[Frontend - React/Vite :5173]
        BFF[Backend/BFF - FastAPI :5000]
        ORCHESTRATOR[Orchestrator - Temporal Worker]
    end
    
    subgraph "Docker Infrastructure"
        TRAEFIK[Traefik Proxy :80]
        TTS[TTS Services :8081]
        POSTGRES[(PostgreSQL :5432)]
        NEO4J[(Neo4j :7474)]
        QDRANT[(Qdrant :6333)]
        TEMPORAL[Temporal Server :7233]
        ELEVENLABS[ElevenLabs Adapter :8082]
        WHISPER[Whisper STT :9000]
    end
    
    subgraph "External Services"
        LLM_APIS[LLM APIs - Ollama/OpenAI/Claude]
        EXTERNAL_AGENTS[External Agents]
    end
    
    %% Client connections
    U --> BROWSER
    BROWSER --> FRONTEND
    BROWSER --> TRAEFIK
    
    %% Host service connections
    FRONTEND --> BFF
    BFF --> POSTGRES
    BFF --> NEO4J  
    BFF --> QDRANT
    BFF --> TEMPORAL
    ORCHESTRATOR --> TEMPORAL
    ORCHESTRATOR --> LLM_APIS
    
    %% Docker infrastructure connections
    TRAEFIK --> FRONTEND
    TRAEFIK --> BFF
    TRAEFIK --> TTS
    TRAEFIK --> NEO4J
    TRAEFIK --> QDRANT
    BFF --> TTS
    BFF --> ELEVENLABS
    BFF --> WHISPER
    ORCHESTRATOR --> EXTERNAL_AGENTS
    
    %% Styling
    classDef host fill:#e8f5e8,stroke:#2e7d32,stroke-width:3px
    classDef docker fill:#e3f2fd,stroke:#1565c0,stroke-width:2px
    classDef external fill:#fff3e0,stroke:#ef6c00,stroke-width:2px
    classDef client fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    
    class FRONTEND,BFF,ORCHESTRATOR host
    class TRAEFIK,TTS,POSTGRES,NEO4J,QDRANT,TEMPORAL,ELEVENLABS,WHISPER docker
    class LLM_APIS,EXTERNAL_AGENTS external
    class U,BROWSER client
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
    subgraph "Hybrid Development (Local)"
        HD_USER[User] --> HD_FE[Frontend Host :5173]
        HD_USER --> HD_TRAEFIK[Traefik Docker :80]
        HD_FE --> HD_BE[Backend Host :5000]
        HD_TRAEFIK --> HD_FE
        HD_TRAEFIK --> HD_BE
        HD_BE --> HD_PG[(PostgreSQL Docker :5432)]
        HD_BE --> HD_NEO[(Neo4j Docker :7474)]
        HD_BE --> HD_QD[(Qdrant Docker :6333)]
        HD_BE --> HD_TEMP[Temporal Docker :7233]
        HD_BE --> HD_TTS[TTS Docker :8081]
        HD_TEMP --> HD_ORCH[Orchestrator Host]
        HD_ORCH --> HD_LLM[External LLM Services]
        Note over HD_USER,HD_LLM: Apps on host for hot reload, infrastructure in Docker
    end
    
    subgraph "Bare Metal Windows Deployment"
        BM_USER[User] --> BM_FE[Frontend Native :5000]
        BM_USER --> BM_BE[Backend Native :8000]
        BM_FE --> BM_BE
        BM_BE --> BM_PG[(PostgreSQL Native :5432)]
        BM_BE --> BM_NEO[(Neo4j Native :7474)]
        BM_BE --> BM_QD[(Qdrant Native :6333)]
        BM_BE --> BM_TEMP[Temporal Native :7233]
        BM_BE --> BM_TTS[Fish TTS Native :8081]
        BM_TEMP --> BM_ORCH[Orchestrator Native]
        BM_ORCH --> BM_LLM[LLM Services]
        Note over BM_USER,BM_LLM: All services run natively without containers
    end
    
    subgraph "Cloud Development (Replit)"
        CD_USER[User] --> CD_FE[Frontend :5000]
        CD_FE --> CD_BE[Backend :8000]
        CD_BE --> CD_PG[(Managed PostgreSQL)]
        CD_BE --> CD_LLM[External LLM APIs]
        Note over CD_USER,CD_LLM: Simplified deployment - core services only
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

These comprehensive diagrams provide a complete view of SwAIvyn's hybrid architecture including:

1. **Hybrid Development Architecture** - Host services (hot reload) + Docker infrastructure
2. **Complete API Call Flow** - REST endpoint patterns based on actual FastAPI routes
3. **Deployment Architecture Comparison** - Local hybrid vs bare metal vs cloud deployment
4. **Frontend Authentication State Flow** - Client-side authentication and navigation
5. **Status Polling Flow** - Real-time dashboard updates via polling
6. **Performance Monitoring** - System health and metrics collection

## 🚧 Known Architecture Notes

- **SignalR Frontend Code**: The frontend contains `useChatHub.ts` with SignalR client code, but no corresponding SignalR hubs exist in the FastAPI backend. This appears to be leftover from earlier architecture or planned future feature.
- **Current Communication**: All real-time updates use REST API polling rather than WebSocket/SignalR connections.
- **Port Mapping**: Local development uses Frontend :5173 + Backend :5000; Replit/cloud uses Frontend :5000 + Backend :8000.

These diagrams serve as a reference for:
- **Developers** understanding the actual hybrid system architecture
- **DevOps** planning deployments and scaling strategies
- **Troubleshooting** system issues and performance bottlenecks
- **Integration** planning for external agents and services