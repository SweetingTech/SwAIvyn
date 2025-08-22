## SwAIvyn Setup and Integration Plan

### Scope and goals
- Keep all paths portable (no absolute paths persisted in configs or code)
- Respect existing Docker services (Weaviate, Whisper) and avoid port conflicts
- Ensure all network requests originate from the backend (frontend talks only to /api)
- Add Redis and GraphRAG readiness checks and expose a single aggregated /api/health
- Fix current API 500s caused by SQLite path mismatch without hard-coding absolute paths
- Make the search service (GraphRAG) configurable and easy to run locally
- Update Windows service install script to be net8/dist aware and path-safe

### Current environment (from docker ps)
- Weaviate (semitechnologies/weaviate:1.27.0): host 8080 -> 8080/tcp
- Frontend dev (swaivyn-frontend): host 5173 -> 5173/tcp
- FishSpeech TTS (swaivyn-swai-tts): host 5002 -> 5002/tcp
- Whisper STT (whisper-api:latest): container 8000/tcp (no published host port listed)
- Weaviate sidecar modules: various (no host ports)

### Port map (single source of truth)
- Frontend (Vite dev): 5173
- Backend API (Kestrel): 5000
- Weaviate (Docker): 8080
- FishSpeech TTS: 5002
- Redis: 6379
- Neo4j (optional): 7474 HTTP, 7687 Bolt
- GraphRAG Search service (FastAPI): 8001 (backend-only)
- Whisper STT (Docker): 8002 (backend will call http://localhost:8002 per your cheat sheet)

Notes:
- All requests come from the backend. The frontend will not call services directly.
- Whisper is reachable at http://localhost:8002 from the backend (per your cheat sheet). We will not change Whisper ports.

### Planned changes (no edits performed yet)

1) Backend: fix DB path mismatch (still relative)
- File: backend/appsettings.json
  - Change ConnectionStrings.DefaultConnection to "../data/swai-vyn.db" (was ../Sqldatabase/swai-vyn.db), so both DbContexts use the same file.
  - Keep all other paths relative (../data, ../logs, etc.).

2) Backend: add Redis/GraphRAG/STT/TTS health checks and aggregate readiness
- File: backend/Controllers/HealthCheckController.cs (extend)
  - Add endpoints:
    - GET /api/healthcheck/redis (TCP to host:6379)
    - GET /api/healthcheck/graphrag (HTTP to configured BaseUrl/health, fallback to root)
    - GET /api/healthcheck/stt (HTTP to configured SttUrl/health, fallback to root)
    - GET /api/healthcheck/tts (HTTP to FishSpeech.BaseUrl/health, fallback to root)
  - Add GET /api/health that aggregates sqlite, weaviate, neo4j, redis, graphrag, stt, tts, and LLM endpoints.
- File: backend/appsettings.json (add keys; all portable)
  - AppSettings.Redis.ConnectionString: "localhost:6379"
  - AppSettings.GraphRag.BaseUrl: "http://localhost:8001"
  - Leave AppSettings.SttUrl as-is (do not change Whisper port)

3) Backend: proxy routes so frontend uses only /api
- New file: backend/Controllers/SearchProxyController.cs
  - POST /api/search → forwards JSON body to GraphRAG BaseUrl (/search), returns response
- (Optional, if needed) New file: backend/Controllers/SttProxyController.cs
  - Proxy STT calls to Whisper based on AppSettings.SttUrl
- No frontend calls to external ports; all via backend.

4) Backend: DI tidy-up for vector health
- File: backend/Controllers/HealthCheckController.cs
  - Avoid injecting a generic IVectorStore if it isn’t registered; use specific stores already in DI (WeaviateVectorStore) to prevent 500s.

5) Search service (GraphRAG) configuration and runner
- File: search/search.py
  - Read settings from environment variables with safe defaults:
    - SEARCH_SERVICE_PORT=8001
    - SQL_DB_PATH: repo-root/data/swai-vyn.db
    - WEAVIATE_URL=http://localhost:8080
    - NEO4J_URI=bolt://localhost:7687; NEO4J_USER/PASSWORD via env
    - LLM_API_URL default http://localhost:11434/v1/completions
  - Keep existing /health and /search endpoints.
- New file: scripts/run-search-dev.cmd
  - Create/use venv under search/
  - pip install required dependencies
  - Run uvicorn with configured port

6) Windows service installer alignment
- File: scripts/install-service.ps1
  - Point to the net8/dist published binary instead of the old net7 path
  - Pass content root/working directory so relative paths resolve under the service
  - Keep port via appsettings or args; do not bake absolutes

7) Documentation and port registry
- This SwaiSetup.md becomes the living document of the plan, ports, changes, and status.
- If desired, add a PORTS.md later; for now, this document is the source of truth.

### Non-goals (for clarity)
- No change to Whisper STT ports (container stays at 8000)
- No change to Docker orchestration in this pass
- No change to frontend code (since all requests go via backend)

### Open questions
- Whisper reachability from backend: backend runs on the host and will call AppSettings.SttUrl. Since the container exposes 8000 internally and no host mapping is shown, backend may not reach it via localhost unless there is a published port or shared network. We will not change ports; please confirm how you want the backend to target Whisper (e.g., publish 8000→8000 on host, or run backend within Docker network).
- GraphRAG service port: 8001 is proposed (backend-only). Confirm if 8001 is acceptable.

### Execution plan (phased)
- Phase 0 (this step): Create this plan document and wait for confirmation.
- Phase 1 (low risk fixes):
  - Update backend/appsettings.json DB path to ../data/swai-vyn.db
  - Implement DI-safe health endpoints and aggregated /api/health
  - Build and validate via curl and logs; no new ports introduced
- Phase 2 (backend-only integrations):
  - Add SearchProxyController for /api/search → GraphRAG BaseUrl
  - Parameterize search/search.py via env and add run-search-dev.cmd
  - Validate end-to-end (frontend → backend → GraphRAG) and health
- Phase 3 (service installer and polish):
  - Update scripts/install-service.ps1 to use dist (net8) and set content root
  - Document any operational notes here

### Risks and mitigations
- Whisper container not reachable from host backend if no host port published: health returns offline but non-fatal; we will not alter ports. Mitigation: document and await your direction.
- Neo4j offline: backend already tolerates; health will report offline, non-fatal.
- Env parity: ensure defaults align with Docker services (Weaviate 8080, TTS 5002) and Redis 6379.

### Validation plan
- curl checks for:
  - /api/health (aggregated) and individual /api/healthcheck/* endpoints
  - /api/agents (should return 200 after DB path fix)
  - /api/search (proxy) once GraphRAG is running
- Vite proxy continues to route /api to backend 5000; no frontend direct calls.

### Update log (to be appended as work proceeds)
- [x] 2025-08-18 08:05: Created this plan document and got approval to begin. Ports confirmed; all requests originate from backend. Redis 6379 OK. Do not change Whisper port.
- [x] 2025-08-18 08:10: Backend appsettings.json updated:
  - ConnectionStrings.DefaultConnection → ../data/swai-vyn.db (relative)
  - AppSettings.SttUrl → http://localhost:8002 (per your Whisper cheat sheet)
  - AppSettings.Redis.ConnectionString → localhost:6379
  - AppSettings.GraphRag.BaseUrl → http://localhost:8001
- [x] 2025-08-18 08:12: DI update to register IVectorStore as WeaviateVectorStore to stabilize healthcheck injection.
- [x] 2025-08-18 08:15: Implemented new backend health endpoints: /api/healthcheck/redis, /api/healthcheck/graphrag, /api/healthcheck/stt; extended existing endpoints.
- [x] 2025-08-18 08:20: Added aggregated /api/health and created backend /api/search controller (proxy via IHybridSearchService).
- [ ] 2025-08-18 08:25: Make search/search.py env-driven and add run-search-dev.cmd.
- [ ] 2025-08-18 08:35: Update Windows service installer.
- [ ] 2025-08-18 08:45: Attempted to start Neo4j via docker run (Option A). Image pull from Docker Hub failed with EOF; likely transient network/registry issue. Will retry on confirmation or switch to a pinned tag.

Issues encountered so far:
- Docker Hub pull failed for neo4j:5 with EOF (network/registry transient). Suggested actions: retry, pin to a specific 5.x tag, or login to Docker Hub to avoid rate limits.
- On first run, default seed hit NOT NULL constraint for ChatIndices.ContentType; non-fatal to this task but noted for follow-up.

