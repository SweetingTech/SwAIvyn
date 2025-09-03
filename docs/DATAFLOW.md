# Dataflow & Architecture

This document shows the high-level component topology and request flows.

## Topology

```mermaid
flowchart LR
  subgraph Client
    UI[Frontend (Vite/React)]
  end

  subgraph App
    BFF[FastAPI BFF :5000]
    ORC[Orchestrator Worker]
  end

  subgraph Infra
    PG[(Postgres 16)]
    TDB[(Qdrant)]
    GDB[(Neo4j 5)]
    TMP[Temporal Server]
    STT[STT (Whisper)]
    T11[11Labs Adapter]
    TTS[(Local TTS)]
    OLL[Ollama]
    LMS[LM Studio]
  end

  UI -- /api/*, /hubs/* --> BFF
  BFF -- Temporal SDK --> TMP
  TMP <--> ORC
  BFF <-- SQLAlchemy --> PG
  BFF <---> TDB
  ORC <---> TDB
  ORC <---> GDB
  ORC --> T11
  ORC --> TTS
  ORC --> STT
  ORC --> OLL
  ORC --> LMS
```

## Chat Request Flow

```mermaid
sequenceDiagram
  participant UI as Frontend
  participant BFF as BFF (FastAPI)
  participant TMP as Temporal
  participant ORC as Orchestrator
  participant TTS as TTS/Adapters
  participant DB as Postgres/Qdrant/Neo4j

  UI->>BFF: POST /api/conversation/chat { message }
  BFF->>TMP: Start Workflow (ReplyWorkflow)
  TMP->>ORC: Run Activities (generate_reply, synthesize_tts, upsert_vector_memory, update_graph)
  ORC->>TTS: Synthesize speech (if enabled)
  ORC->>DB: Upsert vector memory / update graph
  ORC-->>TMP: Return reply_text (+tts_url)
  TMP-->>BFF: Workflow result
  BFF-->>UI: { reply_text, tts_url }
```

## Health & Readiness

- BFF: `/healthz`, `/api/readyz`
- Temporal: server on `localhost:7233`
- Qdrant: `http://localhost:6333`
- Neo4j: HTTP `localhost:7474` / Bolt `localhost:7687`
- STT: `http://localhost:9000`
- 11Labs adapter: `http://localhost:8082`

