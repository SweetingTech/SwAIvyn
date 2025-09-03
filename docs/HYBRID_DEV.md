# Hybrid Development Guide

Run fast with hot reload on the host while keeping heavy infra in Docker.

## Overview

- Host (hot reload):
  - BFF (FastAPI) on http://localhost:5000
  - Orchestrator (Temporal worker)
  - Frontend (Vite) on http://localhost:5173
- Docker (infra): Postgres, Temporal, Qdrant, Neo4j, STT, ElevenLabs adapter (and optional heavy TTS)

## Prerequisites

- Windows 10/11 or Linux
- Docker Desktop (or Docker Engine)
- Python 3.11+
- Node.js 18+

## Quickstart

1) Start everything (stops app containers to free ports):

```
scripts/dev-start.ps1
```

Options:
- `-IncludeTTS`: also build/run the heavy local TTS container
- `-ActivityThreads 64`: give the worker more threads
- `-NoStopAppContainers`: leave the `bff/orchestrator/frontend` containers running (use if you prefer them instead of host processes)

2) Stop:

```
scripts/dev-stop.ps1            # stop host apps + stop infra
scripts/dev-stop.ps1 -Down      # full teardown (remove containers/networks)
```

## What the scripts do

- `dev-start.ps1`:
  - Ensures infra containers are running and healthy (Postgres, Temporal, Qdrant, Neo4j, STT, ElevenLabs adapter, optional TTS)
  - Stops containerized `bff`, `orchestrator`, `frontend` by default to free ports
  - Launches three PowerShell windows:
    - BFF (`scripts/dev-bff.ps1`): creates/uses `.venv`, installs deps once, runs uvicorn with `--reload`
    - Orchestrator (`scripts/dev-orchestrator.ps1`): creates/uses `.venv`, installs deps once, runs worker
    - Frontend (`scripts/dev-frontend.ps1`): runs Vite
- `dev-stop.ps1`:
  - Stops the host app processes from `.dev-state.json`
  - Either stops infra or `down`s the compose project

## Ports

- BFF: 5000
- Frontend: 5173
- Temporal: 7233
- Postgres: 5432
- Qdrant: 6333
- Neo4j: 7474 (bolt 7687)
- STT: 9000
- ElevenLabs adapter: 8082

## Auth + Login

- Seeded users (default):
  - admin / admin1234
  - Mari / mari1234
  - DJay / djay1234
- “Remember me” persists login across reloads via localStorage.

## Environment

The BFF and Orchestrator scripts auto-load `.env` from repo root. Important keys:

- `POSTGRES_PASSWORD`, `NEO4J_PASSWORD`: used by infra containers and host apps
- `ELEVENLABS_API_KEY`: enables the adapter
- (Optional) `OLLAMA_HOST`, `LMSTUDIO_HOST`: local LLM endpoints

## Troubleshooting

- Frontend shows splash “Waiting for backend”
  - Ensure host BFF is listening on 5000; `Invoke-WebRequest http://localhost:5000/api/readyz`
  - Restart Vite to pick up latest bundle
- 401 /api/auth/me
  - Expected before login; go to `/login` and use seeded credentials
- Slow Docker builds (Windows)
  - Build from WSL filesystem and/or use the `docker` buildx driver; avoid the slow “sending tarball” path

