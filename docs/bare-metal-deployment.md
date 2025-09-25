# Bare-Metal Deployment Guide

This guide describes how to run SwAIvyn without Docker. You'll run databases and services directly on the host and manage the app tiers as native processes or services.

> Recommendation: Use the hybrid dev setup for day-to-day development. Bare metal is best for long-running deployments where Docker is not desired.

## Components

- App tiers (host):
  - BFF (FastAPI)
  - Orchestrator (Temporal worker)
  - Frontend (static build served by nginx or any static server)
- Infra (host):
  - Postgres 16
  - Temporal Server
  - Qdrant
  - Neo4j 5
  - STT (Whisper service) [optional]
  - ElevenLabs adapter (FastAPI) [optional]
  - Local TTS (heavy) [optional]

## System Requirements

- Windows 10/11 or Linux
- Python 3.11+
- Node 18+
- Postgres 16, Neo4j 5, Qdrant, Temporal Server

## Install Infra

Windows (choco examples):

```
choco install postgresql16 neo4j-community
# Qdrant: https://qdrant.tech/documentation/quick_start/
# Temporal: https://docs.temporal.io/
```

Linux (Ubuntu examples):

```
sudo apt-get update
sudo apt-get install postgresql neo4j
# Qdrant: https://qdrant.tech/documentation/quick_start/
# Temporal: https://docs.temporal.io/
```

Configure:
- Postgres DB `swai` with user `postgres` and your `POSTGRES_PASSWORD`
- Temporal listening on `localhost:7233`
- Qdrant on `localhost:6333`
- Neo4j on `bolt://localhost:7687` (set `NEO4J_PASSWORD`)

## App Tiers (Host)

### BFF

```
cd Services/bff
python -m venv .venv
. .venv/Scripts/activate  # Windows: .\.venv\Scripts\Activate.ps1
pip install -r requirements.txt

set DATABASE_URL=postgresql+asyncpg://postgres:<pwd>@localhost:5432/swai
set TEMPORAL_HOST=localhost:7233
set QDRANT_URL=http://localhost:6333
set NEO4J_URL=bolt://localhost:7687
set NEO4J_PASSWORD=<pwd>
# Optional local LLMs/TTS:
set OLLAMA_HOST=http://localhost:11434
set LMSTUDIO_HOST=http://localhost:1234
set TTS_ADAPTER_URL=http://localhost:8082
set FISHSPEECH_URL=http://localhost:8081

uvicorn app.main:app --host 0.0.0.0 --port 5000 --reload
```

### Orchestrator

```
cd Services/orchestrator
python -m venv .venv
. .venv/Scripts/activate
pip install -r requirements.txt

set TEMPORAL_HOST=localhost:7233
set ACTIVITY_THREADS=32
set TTS_ADAPTER_URL=http://localhost:8082
set FISHSPEECH_URL=http://localhost:8081

python -m app.worker
```

### Frontend

```
cd frontend
npm ci
npm run build

# Serve dist/ with nginx, Caddy, or any static server
# Example (serve, for dev):
npx serve -s dist -l 5173
```

## Services (Production)

Use systemd (Linux) or NSSM/Task Scheduler (Windows) to run the BFF and Orchestrator as services.

### Linux (systemd examples)

`/etc/systemd/system/swaivyn-bff.service`
```
[Unit]
Description=SwAIvyn BFF
After=network.target

[Service]
WorkingDirectory=/opt/SwAIvyn/Services/bff
Environment=DATABASE_URL=postgresql+asyncpg://postgres:<pwd>@localhost:5432/swai
Environment=TEMPORAL_HOST=localhost:7233
Environment=QDRANT_URL=http://localhost:6333
Environment=NEO4J_URL=bolt://localhost:7687
Environment=NEO4J_PASSWORD=<pwd>
ExecStart=/opt/SwAIvyn/Services/bff/.venv/bin/uvicorn app.main:app --host 0.0.0.0 --port 5000
Restart=always

[Install]
WantedBy=multi-user.target
```

`/etc/systemd/system/swaivyn-orchestrator.service`
```
[Unit]
Description=SwAIvyn Orchestrator
After=network.target

[Service]
WorkingDirectory=/opt/SwAIvyn/Services/orchestrator
Environment=TEMPORAL_HOST=localhost:7233
Environment=ACTIVITY_THREADS=32
ExecStart=/opt/SwAIvyn/Services/orchestrator/.venv/bin/python -m app.worker
Restart=always

[Install]
WantedBy=multi-user.target
```

Reload and start:

```
sudo systemctl daemon-reload
sudo systemctl enable --now swaivyn-bff swaivyn-orchestrator
```

## Notes

- The local TTS service is very heavy (multi-GB). Prefer ElevenLabs adapter if available.
- For performance on Windows, avoid cross-filesystem I/O; keep the repo and venvs on the same drive.
- TLS/Reverse proxy: terminate at nginx/Caddy and proxy `/api` to `localhost:5000`.

