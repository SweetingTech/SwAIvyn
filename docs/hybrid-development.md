# Hybrid Development Guide

Run fast with hot reload on the host while keeping heavy infra in Docker. You can also run TTS on bare metal for best dev performance on Windows.

## Overview

- Host (hot reload):
  - BFF (FastAPI) on http://localhost:5000
  - Orchestrator (Temporal worker)
  - Frontend (Vite) on http://localhost:5173
- Docker (infra): Postgres, Temporal, Qdrant, Neo4j, STT, ElevenLabs adapter
- Optional host services: TTS (Fish Speech) on http://localhost:8081 when using `-UseHostTTS`

## Prerequisites

- Windows 10/11 or Linux
- Docker Desktop (or Docker Engine)
- Python 3.11+
- Node.js 18+

## Quickstart

1) Start infra, then apps (recommended hybrid flow):

```
# Infra only (no app-tier images)
scripts/infra-up.ps1                # add -WithTTSAdapter/-WithTTS/-WithOrchestrator if needed

# Apps on host with hot reload
scripts/dev-start.ps1 -UseHostTTS   # spawns BFF, Orchestrator, Frontend + host TTS window
```

Or, the classic one-liner (includes infra checks and spawns app windows):

```
scripts/dev-start.ps1
```

Options:
- `-UseHostTTS`: run Fish Speech TTS on the host (recommended in dev)
- `-IncludeTTS`: also build/run the TTS container (skip when using `-UseHostTTS`)
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
  - If `-UseHostTTS`, also launches `scripts/dev-tts.ps1` (Fish Speech on http://localhost:8081)
- `dev-stop.ps1`:
  - Stops the host app processes from `.dev-state.json`
  - Either stops infra or `down`s the compose project

### Compose Profiles

- Orchestrator, TTS, and the 11Labs adapter are behind profiles so they don't build/run by default:
  - `orchestrator`: app worker image
  - `tts`: Fish Speech TTS container
  - `tts-adapter`: ElevenLabs adapter
- Examples:
  - `docker compose --profile orchestrator up -d orchestrator`
  - `docker compose --profile tts up -d tts`
  - `docker compose --profile tts-adapter up -d tts-11labs-adapter`

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
- "Remember me" persists login across reloads via localStorage.

## Environment

The BFF and Orchestrator scripts auto-load `.env` from repo root. Important keys:

- `POSTGRES_PASSWORD`, `NEO4J_PASSWORD`: used by infra containers and host apps
- `ELEVENLABS_API_KEY`: enables the adapter
- (Optional) `OLLAMA_HOST`, `LMSTUDIO_HOST`: local LLM endpoints
- (Dev) `FISHSPEECH_URL`: when running containers that should call host TTS, set to `http://host.docker.internal:8081`

## Dataflow (per user)

- Settings -> Chat Settings (per user)
  - Engine/Model saves to `PUT /api/chat/settings/{userId}`
  - Connections (LM Studio/Ollama/OpenAI/Claude/vLLM endpoints/keys) save to `PUT /api/settings/connections`
- Chat -> Send
  - Client includes `engine` + `model` in `POST /api/conversation/chat`
  - BFF resolves per-user connections and launches the engine-specific workflow
  - Workflows (one per engine) call only that engine with the selected model - no cycling
  - TTS synthesis uses host TTS (http://localhost:8081) or adapter URLs based on env

Per-user isolation:
- All reads/writes for chat settings, connections, and conversations are authorized per user; only admin can access others' settings/data.

## Update & Maintenance

- Update dependencies and pull infra images:

```
scripts/update.ps1 -All
# or
scripts/update.ps1 -FrontendDeps -BackendDeps -InfraPull
```

## Troubleshooting

- Frontend shows splash "Waiting for backend"
  - Ensure host BFF is listening on 5000; `Invoke-WebRequest http://localhost:5000/api/readyz`
  - Restart Vite to pick up latest bundle
- 401 /api/auth/me
  - Expected before login; go to `/login` and use seeded credentials
- Slow Docker builds (Windows)
  - Prefer host apps + `scripts/infra-up.ps1` in dev. If you must build, build specific services and avoid the all-services tar export path.
