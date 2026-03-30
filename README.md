# SwAIvyn

<div align="center">

**Every self-hosted AI assistant on GitHub is built for one person. SwAIvyn is built for many.**

[![MIT License](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Python](https://img.shields.io/badge/Python-3.11+-3776AB)](https://www.python.org/)
[![FastAPI](https://img.shields.io/badge/FastAPI-0.104+-009688)](https://fastapi.tiangolo.com/)
[![React](https://img.shields.io/badge/React-18-61DAFB)](https://reactjs.org/)

</div>

SwAIvyn is a self-hosted AI platform designed for multi-user teams. It runs entirely on your infrastructure — bare metal, Docker Swarm, or cloud — and gives every user a fully isolated AI workspace while giving administrators visibility and control across the entire system. The orchestration layer is built on Temporal, the agent registry is multi-tenant from the ground up, and every workflow is versioned and auditable.

---

## Why SwAIvyn

|                        | Single-user AI tools       | SwAIvyn                                             |
|------------------------|----------------------------|-----------------------------------------------------|
| **Users**              | 1                          | Unlimited, fully isolated                           |
| **Agent integration**  | Manual / none              | Multi-tenant registry with full task lifecycle      |
| **Workflow execution** | Direct API calls           | Temporal-orchestrated, versioned workflows          |
| **Memory**             | Shared or none             | Per-user vector + graph memory with admin controls  |
| **LLM routing**        | Global config              | Per-user engine selection with admin-level routing  |
| **Deployment**         | Local only                 | Bare metal, Docker Swarm, cloud                     |

---

## Architecture

```
[User A] ─┐
[User B] ─┤─── FastAPI BFF ──── Temporal Orchestrator ──── [LLM Engine]
[User C] ─┘         │
                     ├── PostgreSQL  (auth / conversations / characters)
                     ├── Qdrant      (per-user vector memory)
                     ├── Neo4j       (per-user memory graph)
                     └── External Agent Registry (multi-tenant, task lifecycle)
```

**Key layers:**

- **FastAPI BFF** — Authentication, per-user routing, and API boundary. All requests are scoped to the authenticated user; admin tokens unlock cross-user visibility.
- **Temporal Orchestrator** — Durable, versioned workflow execution. Chat, agent dispatch, and background tasks all run as Temporal workflows, giving you replay, retry, and auditability out of the box.
- **Storage tier** — Three purpose-built stores: PostgreSQL for relational data, Qdrant for vector similarity, Neo4j for associative memory graphs. Each store enforces user-scoped access.
- **External Agent Registry** — A structured registry for specialized AI workers running on separate servers. Every agent registration, task submission, and result is scoped to the owning user.
- **Voice / TTS / STT** — Fish Speech (local, privacy-preserving) proxied behind Traefik, with an ElevenLabs adapter for cloud voice. Per-user voice configuration persisted in settings.

---

## Developer Quickstart

**Prerequisites:** Docker Desktop, Python 3.11+, Node 18+

```powershell
# Start the full stack (infrastructure in Docker, app services with hot reload)
.\scripts\dev-run.ps1

# Stop everything cleanly
.\scripts\dev-shutdown.ps1
```

**Seed default users, characters, and the chat workflow:**
```powershell
.\scripts\dev-seed-accounts.ps1 -Yes    # admin / mari / djay
.\scripts\dev-seed-characters.ps1 -Yes  # Sam & Sherlock (global characters)
.\scripts\dev-seed-workflows.ps1 -Yes   # Default Chat workflow
```

**Default test users:**

| Username | Password  | Role  |
|----------|-----------|-------|
| admin    | admin1234 | Admin |
| mari     | mari1234  | User  |
| djay     | djay1234  | User  |

**Development endpoints:**

| Service         | URL                                        |
|-----------------|--------------------------------------------|
| Frontend (UI)   | http://localhost:5173                      |
| BFF API         | http://localhost:8000 (`/healthz`, `/api/readyz`) |
| Temporal        | localhost:7233                             |
| Qdrant          | http://localhost:6333                      |
| Neo4j           | http://localhost:7474 (bolt: 7687)         |

**Advanced options:**
```powershell
.\scripts\dev-run.ps1 -FrontendOnly          # React/Vite only
.\scripts\dev-run.ps1 -BackendOnly           # FastAPI + Docker infra
.\scripts\dev-run.ps1 -DisableTraefik        # Direct ports, no reverse proxy
.\scripts\dev-shutdown.ps1 -DownCompose -Prune  # Full cleanup
```

---

## Deployment

### Hybrid Development (Recommended)

Application services (BFF, Orchestrator, Frontend) run on the host for hot reload. Infrastructure (databases, TTS, Traefik) runs in Docker containers.

```powershell
.\scripts\dev-run.ps1
```

Host services: Frontend `:5173`, BFF `:8000`, Temporal worker (background)
Docker infrastructure: PostgreSQL `:5432`, Temporal `:7233`, Qdrant `:6333`, Neo4j `:7474/7687`, Fish Speech TTS `:8081`, Whisper STT `:9000`, ElevenLabs adapter `:8082`, Traefik `:80`

### Bare Metal (Windows, No Docker)

Installs and runs all 15+ services natively via Chocolatey. Suitable for production deployments without containerization overhead.

```powershell
# Install all dependencies and start the full stack
.\scripts\setup-bare-metal.ps1 -InstallDependencies

# Start / stop after setup
.\start-bare-metal.ps1
.\stop-bare-metal.ps1
```

Service endpoints: Frontend `:5000`, BFF `:8000`, PostgreSQL `:5432`, Neo4j `:7474`, Qdrant `:6333`

### Cloud / Replit

SwAIvyn detects Replit automatically and reconfigures: Frontend binds `:5000`, BFF binds `:8000`, CORS expands to `*.repl.co`. No manual configuration needed.

### Docker Swarm

Use `.\scripts\build-stack.ps1` to build images and deploy to a Swarm cluster.

```powershell
.\scripts\build-stack.ps1 -All -Pull           # Rebuild everything
.\scripts\build-stack.ps1 -Target tts,app      # Rebuild specific groups
.\scripts\build-stack.ps1 -List                # List available targets
```

Build groups: `tts` (tts-proxy, 11labs-adapter, stt), `infra` (postgres, qdrant, neo4j, temporal), `app` (bff, frontend, orchestrator, workers)

### Single-Executable Release

Download the latest release from the [Releases page](https://github.com/SweetingTech/SwAIvyn/releases), run the executable, and follow the on-screen setup.

### Manual Build

```bash
git clone https://github.com/SweetingTech/SwAIvyn.git
cd SwAIvyn

cd Services/bff && pip install -r requirements.txt
cd ../../frontend && npm install && npm run build
```

### LAN Access

Vite and FastAPI both bind to `0.0.0.0` by default. To allow other devices on your network:

```powershell
# Local development
netsh advfirewall firewall add rule name="SwAIvyn Frontend" dir=in action=allow protocol=TCP localport=5173
netsh advfirewall firewall add rule name="SwAIvyn Backend" dir=in action=allow protocol=TCP localport=8000
```

---

## Platform Capabilities

### Per-user isolated memory with admin visibility controls

Each user has a dedicated memory space across three stores: Qdrant (vector similarity), Neo4j (associative memory graph), and PostgreSQL (structured conversation history). Users can browse, search, edit, and selectively share memories. Admins can view and manage memory across all users without cross-contaminating user data.

### Per-user LLM engine selection with admin-level routing

Every user independently selects their LLM backend (Ollama, LM Studio, OpenAI, Claude) and model via the Settings UI. The BFF routes each chat request to the correct engine for that user — no shared global config, no fallback to another user's engine. Admins can see the active engine per user from the dashboard.

**Per-user LLM dataflow:**
- Save engine/model: `PUT /api/chat/settings/{userId}`
- Save connections: `PUT /api/settings/connections`
- Chat sends `engine` + `model` per request → BFF launches engine-specific Temporal workflow → worker calls only that engine/model

### Cross-instance AI federation with privacy boundary enforcement

SwAIvyn instances on the same network can communicate AI-to-AI. This enables cross-instance calendar coordination, selective memory sharing, and user-to-user message passing — all with explicit privacy boundary controls so users choose what leaves their instance.

### Multi-tenant external agent registry with full task lifecycle management

Register specialized AI workers running on any server. Every agent registration, task, and result is scoped to the owning user. Workers never see another user's tasks, data, or registry entries.

**Register an agent:**
```bash
POST /api/agents/register
Authorization: Bearer <jwt>

{
  "name": "Document Processor",
  "endpoint_url": "https://my-agent.example.com",
  "agent_type": "task_processor",
  "capabilities": ["text_processing", "data_analysis"]
}
```

**Submit a task:**
```bash
POST /api/agents/tasks
Authorization: Bearer <jwt>

{
  "registry_id": "<agent-id>",
  "task_type": "process_document",
  "input_data": {"document": "content"},
  "priority": "normal"
}
```

**Retrieve results:** `GET /api/agents/tasks/{task_id}/results`

Full API reference: [`docs/EXTERNAL_AGENT_GUIDE.md`](docs/EXTERNAL_AGENT_GUIDE.md) and [`docs/AGENT_STACK_INTEGRATION.md`](docs/AGENT_STACK_INTEGRATION.md)

### Temporal-orchestrated versioned chat workflows

Chat execution is driven by a versioned "Default Chat" workflow stored in the database. Every chat request becomes a Temporal workflow execution — durable, retryable, and auditable. Future enhancements (search augmentation, moderation, tool use) are workflow edits, not code changes.

- List workflows: `GET /api/workflows`
- Get default: `GET /api/workflows/default`
- Get by id: `GET /api/workflows/{id}`

### Dual interfaces: text chat and voice-first room

- **Text Chat** — Full conversation history, file uploads, webcam, rich markdown and code rendering, TTS playback. All data is user-scoped.
- **AI Room** — Voice-first interface with visual representation of the AI in a virtual space. Voice is the primary input method; text input is available as a secondary mode.

### Admin dashboard

Admin-only view surfacing active LLM engine and model per user, agent registry status, and cross-user conversation management. Standard users see only their own data.

---

## Environment Variables

### FastAPI BFF

| Variable           | Required | Description |
|--------------------|----------|-------------|
| `DATABASE_URL`     | Yes      | PostgreSQL connection string |
| `JWT_SECRET`       | Yes      | Secret key for signing access tokens |
| `ALLOWED_ORIGINS`  | No       | Comma-delimited CORS origins (defaults to localhost dev ports) |

### Temporal Orchestrator Worker

| Variable                   | Required | Description |
|----------------------------|----------|-------------|
| `TEMPORAL_HOST`            | Yes      | Temporal frontend `host:port` |
| `ACTIVITY_THREADS`         | No       | Worker thread count for activities (default: 8) |
| `ORCHESTRATOR_HEALTH_PORT` | No       | Health endpoint port (default: 8088) |

### Frontend (Vite)

| Variable                 | Required | Description |
|--------------------------|----------|-------------|
| `VITE_API_BASE_URL`      | Production | BFF API base URL (optional in dev; Vite proxy handles it) |
| `VITE_STAGEWISE_ENABLED` | No       | Set to `true` to enable Stagewise toolbar |

All scripts read `.env` and construct `DATABASE_URL` from `POSTGRES_PASSWORD` if not explicitly set.

---

## Architecture Notes

- **Communication**: Frontend uses REST API polling for real-time updates. SignalR/WebSocket client code has been removed; all live updates go through polling.
- **Authentication**: JWT tokens with bcrypt hashing. The `useEffectiveUser` hook resolves the active user ID in priority order: `useAuth().user?.id` → `useInitialization().user?.id` → `/api/auth/me` fallback. All API calls include `Authorization` headers automatically.
- **Ownership enforcement**: Users can read/modify only their own settings and conversations. Admins can manage any user's data.
- **Workers agent catalog**: `GET http://localhost:8000/api/agents` (workers orchestrator) — proxied by BFF at `GET /api/agents/catalog`.
- **Development ports**: Local dev uses Frontend `:5173` + BFF `:8000`. Replit uses Frontend `:5000` + BFF `:8000`.

---

## Roadmap

- [x] Multi-user authentication with role-based access control
- [x] Per-user LLM engine and model selection
- [x] External agent registry with multi-tenant task lifecycle
- [x] Temporal-orchestrated chat workflow
- [x] Bare metal, Docker Swarm, and cloud deployment
- [x] Environment-aware configuration and dashboard
- [ ] Voice-first AI Room interface
- [ ] Cross-instance federation
- [ ] Memory management UI enhancements
- [ ] Plugin system expansion
- [ ] 3D avatar support
- [ ] Mobile companion app

See the [project board](https://github.com/SweetingTech/SwAIvyn/projects) for detailed progress.

---

## Personalization Layer

Each user can configure a persistent AI persona through the character system:

- **Character cards** — Import JSON character cards or create custom personalities via the admin panel. Admin controls character creation; users are assigned characters.
- **Avatars** — Upload custom 2D avatars (3D support planned). Voice and speech pattern configuration per character.
- **AI Room** — An optional voice-first interface that gives the AI a visual presence in a virtual living space. Decorable environment with planned 3D item support.
- **Voice** — Fish Speech TTS (default, local, private) or ElevenLabs adapter. Per-user voice selection persisted in settings. Voice directory layout: `voices.json`, `*.wav` files, or one-level subfolders under `speech/TTS/openaudio-s1-mini/voices/`.

The personalization layer runs on top of the platform — characters and avatars are cosmetic configuration, not architectural components.

---

## License

MIT License — see [LICENSE](LICENSE) for details.

---

## Further Reading

- [`docs/hybrid-development.md`](docs/hybrid-development.md) — Hybrid dev environment details
- [`docs/bare-metal-deployment.md`](docs/bare-metal-deployment.md) — Container-free deployment guide
- [`docs/architecture-and-dataflow.md`](docs/architecture-and-dataflow.md) — Architecture diagrams and dataflow
- [`docs/EXTERNAL_AGENT_GUIDE.md`](docs/EXTERNAL_AGENT_GUIDE.md) — External agent integration basics
- [`docs/AGENT_STACK_INTEGRATION.md`](docs/AGENT_STACK_INTEGRATION.md) — Comprehensive agent technical spec (800+ lines)
