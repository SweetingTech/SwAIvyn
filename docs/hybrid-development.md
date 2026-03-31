# Hybrid Development Guide

This is the **current** development flow for SwAIvyn.

## What runs where

- **Host (hot reload)**
  - Frontend (Vite): `http://localhost:5173`
  - BFF (FastAPI): `http://localhost:5000`
  - Orchestrator worker: background process (`python -m app.worker`)
- **Docker Swarm (infra + routing via Traefik)**
  - Traefik: `http://traefik.localhost:8088`
  - Postgres: `localhost:5432`
  - Temporal: `localhost:7233`
  - Qdrant: `localhost:6333`
  - Neo4j: `localhost:7474` (Bolt `7687`)
  - Whisper STT: `localhost:9000`
  - Fish Speech TTS (via host): `localhost:8081`
  - ElevenLabs adapter: `localhost:8082`

## Prerequisites

- Windows PowerShell (scripts are PowerShell-first)
- Docker Desktop with Swarm support
- Python 3.11+
- Node.js 18+

## Start / Stop

### Start everything

```powershell
.\scripts\dev-run.ps1
```

Useful options:

- `-FrontendOnly`
- `-BackendOnly`
- `-DisableTraefik`
- `-TraefikPort 8088`

### Stop everything

```powershell
.\scripts\dev-shutdown.ps1
```

Optional cleanup:

```powershell
.\scripts\dev-shutdown.ps1 -DownCompose -Prune
```

## Script behavior

`dev-run.ps1`:

1. Loads `.env`
2. Ensures Docker Swarm networking and deploys `docker-stack.yml`
3. Waits for infrastructure readiness (Traefik/Qdrant/Temporal)
4. Starts host app processes using scripts resolved from `scripts/` or `scripts/old-scripts/`:
   - `dev-frontend.ps1`
   - `dev-bff.ps1`
   - `dev-orchestrator.ps1`

`dev-shutdown.ps1`:

- Stops host processes (frontend/BFF/orchestrator)
- Removes stack services and known containers
- Optionally prunes Docker networks/resources

## Important environment variables

- `POSTGRES_PASSWORD` (required)
- `NEO4J_PASSWORD` (required)
- `DATABASE_URL` (auto-derived in host dev if unset)
- `TEMPORAL_HOST` (default `127.0.0.1:7233` for host worker)
- `QDRANT_URL` (default `http://localhost:6333`)
- `FISHSPEECH_URL` (default `http://localhost:8081`)
- `TTS_ADAPTER_URL` (default `http://localhost:8082`)
- `OLLAMA_HOST`, `LMSTUDIO_HOST`

## Health checks

- BFF: `GET /healthz`, `GET /readyz`, `GET /api/readyz`
- Orchestrator worker: `ORCHESTRATOR_HEALTH_PORT` (default `8088`) with `/healthz` and `/readyz`

## Notes

- Older docs/scripts that reference `scripts/dev-start.ps1`, `scripts/dev-stop.ps1`, or `scripts/infra-up.ps1` are obsolete.
- In current dev flow, `scripts/dev-run.ps1` is the entrypoint.
