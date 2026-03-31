# Architecture and Dataflow

This document reflects the current app structure and routing behavior.

## Runtime topology

```mermaid
flowchart LR
  U[Browser UI] --> FE[Frontend Vite :5173]
  FE -->|/api| BFF[FastAPI BFF :5000]
  BFF --> TMP[Temporal :7233]
  WRK[Orchestrator Worker] --> TMP

  BFF --> PG[(Postgres :5432)]
  BFF --> QD[(Qdrant :6333)]
  BFF --> NEO[(Neo4j :7687)]

  WRK --> LLM[LLM providers\nOllama / LM Studio / OpenAI / Claude / vLLM]
  WRK --> TTS[TTS endpoints\nFish Speech / 11Labs adapter]
```

## Chat request dataflow

```mermaid
sequenceDiagram
  autonumber
  participant FE as Frontend
  participant B as BFF
  participant T as Temporal
  participant W as Worker
  participant E as LLM Engine

  FE->>B: POST /api/conversation/chat {message, engine, model}
  B->>B: Validate auth + user scope
  B->>T: Start engine-specific workflow
  T->>W: Execute workflow activities
  W->>E: Call selected engine/model
  E-->>W: Reply text
  W-->>T: Activity/workflow result
  T-->>B: Final response
  B-->>FE: {response, ...}
```

### Routing guarantees

- Requests are scoped to authenticated users.
- Selected `engine`/`model` drive workflow selection.
- Admin users can access cross-user resources where explicitly allowed.

## External agent dataflow

```mermaid
sequenceDiagram
  participant U as User
  participant B as BFF
  participant A as External Agent Service

  U->>B: POST /api/agents/tasks
  B-->>U: {task_id}
  A->>B: PATCH /api/agents/tasks/{task_id}\nX-Agent-ID + X-Agent-API-Key
  A->>B: POST /api/agents/tasks/{task_id}/results\nX-Agent-ID + X-Agent-API-Key
  U->>B: GET /api/agents/tasks/{task_id}
  U->>B: GET /api/agents/tasks/{task_id}/results
```

## Health/readiness

- BFF exposes:
  - `/healthz`
  - `/readyz`
  - `/api/healthz`
  - `/api/readyz`
- Worker health server listens on `ORCHESTRATOR_HEALTH_PORT` (default `8088`) with `/healthz` and `/readyz`.
