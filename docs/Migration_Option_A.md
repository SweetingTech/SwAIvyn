# Migration to Option A (Python + Temporal)

This repo now uses a Python/Temporal backend replacing the previous .NET service.

What changed:
- docker-compose.yml now starts `bff` (FastAPI), `orchestrator` (Temporal worker), `temporal`, `ollama`, `qdrant`, and keeps `frontend`, `neo4j`, and `stt`/`tts`.
- The old `.NET` backend directory `backend/` was removed to prevent conflicts. The previous Compose is saved as `docker-compose.legacy.yml` for reference.

Quick start:
1. Copy `.env.example` to `.env` and set passwords/keys.
2. Start the stack:
   - Windows: `scripts/start-stack.ps1`
   - Or run: `docker compose up --build -d`
3. Health checks:
   - BFF: http://localhost:5000/healthz
   - Temporal Web (if enabled separately): use port 8233 (not included by default).

Endpoints:
- `POST /api/chat` with `{ "message": "Hello" }` starts a Temporal workflow and returns the reply (stubbed) and optional TTS URL.

Next steps (recommended):
- Flesh out BFF endpoints to mirror the prior REST contract (import prior OpenAPI if available).
- Implement Qdrant integration for vector memory and Neo4j graph updates in `services/orchestrator/app/activities.py`.
- Wire FishSpeech and/or ElevenLabs calls for real TTS output.
- Add OpenTelemetry traces and metrics.

