# Bare-Metal Deployment Guide (No Docker)

This guide documents the **container-free** deployment path using native host services.

> For daily development, use `scripts/dev-run.ps1` (hybrid mode). Bare-metal is best for operators who do not want Docker in production.

## Components

### App tiers (host)

- BFF (FastAPI)
- Orchestrator worker (Temporal worker process)
- Frontend (static files served by nginx/Caddy or a Node static host)

### Infrastructure (host)

- PostgreSQL 16+
- Temporal Server
- Qdrant
- Neo4j 5+
- Optional: Whisper STT
- Optional: Fish Speech TTS / ElevenLabs adapter

## Recommended host ports

- Frontend: `5173` (or your reverse-proxy port)
- BFF API: `5000`
- Temporal: `7233`
- Postgres: `5432`
- Qdrant: `6333`
- Neo4j: `7474` / Bolt `7687`
- Whisper STT: `9000`
- TTS: `8081`
- TTS Adapter: `8082`

## BFF setup

```powershell
cd Services/bff
python -m venv .venv
. .venv\Scripts\Activate.ps1
pip install -r requirements.txt

$env:DATABASE_URL = "postgresql+asyncpg://postgres:<pwd>@localhost:5432/swai"
$env:TEMPORAL_HOST = "127.0.0.1:7233"
$env:QDRANT_URL = "http://localhost:6333"
$env:NEO4J_URL = "bolt://localhost:7687"
$env:NEO4J_PASSWORD = "<pwd>"
$env:FISHSPEECH_URL = "http://localhost:8081"
$env:TTS_ADAPTER_URL = "http://localhost:8082"

python -m uvicorn app.main:app --host 0.0.0.0 --port 5000
```

## Orchestrator setup

```powershell
cd Services/orchestrator
python -m venv .venv
. .venv\Scripts\Activate.ps1
pip install -r requirements.txt

$env:TEMPORAL_HOST = "127.0.0.1:7233"
$env:ACTIVITY_THREADS = "32"
$env:FISHSPEECH_URL = "http://localhost:8081"
$env:TTS_ADAPTER_URL = "http://localhost:8082"

python -m app.worker
```

## Frontend setup

```powershell
cd frontend
npm ci
npm run build

# Example static serving option
npx serve -s dist -l 5173
```

## Operational notes

- Configure TLS/reverse proxy at nginx/Caddy and route `/api` to `http://localhost:5000`.
- Ensure your firewall allows required service ports.
- Legacy helper scripts for older bare-metal flow are kept in `scripts/old-scripts/` and are not the recommended entrypoint.
