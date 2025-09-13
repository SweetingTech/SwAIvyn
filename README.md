# SwAIvyn

<div align="center">
  
![SwAIvyn Logo](https://via.placeholder.com/200x200.png?text=SwAIvyn)

**A federated AI assistant with dual interfaces and Tamagotchi-like features**

[![MIT License](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![React](https://img.shields.io/badge/React-18-61DAFB)](https://reactjs.org/)

</div>

## 📋 Overview

SwAIvyn is a privacy-focused, self-contained AI assistant that runs entirely on your local network. It features both traditional text-based chat and an immersive voice-first interface where your AI lives in a customizable virtual space.

### Key Features

- **Dual Interfaces**
  - 💬 **Text Chat**: Traditional keyboard-first interface
  - 🎤 **AI Room**: Voice-first Tamagotchi-style interface with virtual living space
  
- **Complete Privacy**
  - 🔒 Runs entirely on your closed network
  - 🔐 No data sent to external servers
  - 📂 Local storage of all conversations and memories
  
- **Federated Experience**
  - 🔄 AI-to-AI communication across instances
  - 💌 Message passing between users
  - 🧠 Selective memory sharing between AIs
  
- **Personalization**
  - 🎭 Import character cards or create custom personalities
  - 🖼️ Customizable avatars (2D now, 3D planned)
  - 🏠 Decorable virtual living space

- **Advanced Features**
  - 📧 Email and calendar integration
  - 🔍 Web browsing capabilities via Browsh
  - 💾 Automated backup to NAS or cloud
  - 🧩 Modular plugin architecture
  - 🎙️ Fish Speech TTS with API token authentication

## 🖥️ Screenshots

<div align="center">
  <img src="https://via.placeholder.com/400x250.png?text=Text+Chat+UI" alt="Text Chat UI" width="45%">
  <img src="https://via.placeholder.com/400x250.png?text=AI+Room+UI" alt="AI Room UI" width="45%">
</div>

## 🚀 Getting Started

### Prerequisites

- **.NET 8 SDK** or later
- **Node.js** (v16+) and npm
- **Windows 10/11** or **Linux** with systemd support

### Installation

#### Development (Recommended)

SwAIvyn includes comprehensive development scripts that handle both Docker containers and non-Docker services:

**1. Quick Start - Full Development Environment**

Start everything with a single command:

```powershell
# Start all Docker containers + non-Docker services
.\dev-run.ps1

# Stop everything cleanly
.\dev-shutdown.ps1
```

**2. Development Options**

The dev-run script supports various configurations:

```powershell
# Frontend only (React/Vite)
.\dev-run.ps1 -FrontendOnly

# Backend only (FastAPI + Docker infrastructure)
.\dev-run.ps1 -BackendOnly

# Disable Traefik routing (use direct ports)
.\dev-run.ps1 -DisableTraefik

# Complete shutdown with container removal
.\dev-shutdown.ps1 -DownCompose

# Aggressive cleanup (prune networks and system)
.\dev-shutdown.ps1 -Aggressive
```

**What the Development Scripts Handle:**

**Docker Containers Started:**
- PostgreSQL database (swai-db) - localhost:5432
- Temporal workflow service - localhost:7233  
- Qdrant vector database - localhost:6333
- Neo4j graph database - localhost:7474/7687
- Fish Speech TTS service - localhost:8081
- Speech-to-Text (Whisper) - localhost:9000
- 11Labs TTS adapter - localhost:8082
- Traefik reverse proxy - localhost:80

**Non-Docker Services Started:**
- Frontend (React/Vite) - localhost:5173
- Backend BFF (FastAPI) - localhost:5000
- Orchestrator (Temporal worker)

**Key Features:**
✅ **One-Command Setup** - Start everything with `.\dev-run.ps1`
✅ **Clean Shutdown** - Stop everything with `.\dev-shutdown.ps1`
✅ **Health Checks** - Waits for services to be ready before proceeding
✅ **Service Coordination** - Ensures dependencies are ready before starting dependent services
✅ **Remote Access Support** - Services accessible from other devices on your network
✅ **Traefik Integration** - Full routing with *.localhost domains
✅ **Process Management** - Tracks all running services for easy cleanup

**Available URLs:**
- **Frontend**: http://localhost:5173 or http://app.localhost:80
- **Backend API**: http://localhost:5000 or http://bff.localhost:80
- **Traefik Dashboard**: http://traefik.localhost:80
- **Infrastructure Services**:
  - Qdrant Vector DB: http://qdrant.localhost:80
  - Neo4j Graph DB: http://graph.localhost:80
  - Weaviate Vector DB: http://weaviate.localhost:80

**Remote Access:**
Both scripts configure services to be accessible from other devices on your network via your machine's IP address.

### Dev Seed Helpers (Users, Characters, Workflow)

For a quick, consistent dev setup:

```
# Accounts: admin (admin1234), mari, djay
SwAIvyn/scripts/dev-seed-accounts.ps1 -Yes

# Characters: import Sam & Sherlock from frontend/AI into DB (global)
SwAIvyn/scripts/dev-seed-characters.ps1 -Yes

# Default Chat Workflow: upsert canonical workflow definition
SwAIvyn/scripts/dev-seed-workflows.ps1 -Yes
```

All scripts read `.env` and, if needed, build `DATABASE_URL` from `POSTGRES_PASSWORD`.

#### Single-Executable Release

1. Download the latest release from the [Releases page](https://github.com/SweetingTech/SwAIvyn/releases)
2. Run the executable
3. Follow the on-screen setup instructions

#### Manual Build

```bash
# Clone the repository
git clone https://github.com/SweetingTech/SwAIvyn.git
cd SwAIvyn

# Build the backend
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true

# The executable will be in bin/Release/net8.0/win-x64/publish/
```

### First-Time Setup

1. When first launched, SwAIvyn will create a default admin account
2. You'll need to set a password and generate recovery phrases
3. Configure your AI's personality and avatar
4. Connect with other SwAIvyn instances on your network (optional)

### Access from Other Computers (LAN)

During development, both the frontend (Vite) and backend (FastAPI) can be reached from other devices on your LAN.

- Frontend: `http://<your-pc-ip>:5173` (proxies `/api` and `/uploads` to the backend)
- Backend (optional direct access): `http://<your-pc-ip>:5000`

Already configured in this repo:
- Vite binds to `0.0.0.0` so LAN clients can connect (see `frontend/vite.config.ts`).
- Backend runs on `0.0.0.0:5000` (see `scripts/dev-bff.ps1`).

Windows Firewall (PowerShell as Administrator):

```
netsh advfirewall firewall add rule name="SwAIvyn Vite 5173" dir=in action=allow protocol=TCP localport=5173
netsh advfirewall firewall add rule name="SwAIvyn BFF 5000" dir=in action=allow protocol=TCP localport=5000
```

Optional CORS: If you skip the Vite proxy and call the backend directly from a different origin, add your LAN origin (e.g., `http://<your-pc-ip>:5173`) to `allow_origins` in `Services/bff/app/main.py`.

## 🧩 Features in Detail

### Text Chat Interface

Traditional chat interface for keyboard-based interaction:
- Full conversation history
- File uploads and webcam integration
- Rich markdown and code support
- TTS playback of AI responses

### AI Room Interface

Immersive, voice-focused interface:
- Visual representation of your AI in its living space
- Voice as primary interaction method
- Minimized text input for when needed
- Future: 3D environment with customizable items

### Memory Management

- Browse, search, and edit your AI's memories
- Toggle sharing of specific memories with other instances
- Categorize and organize important information
- Review what your AI has learned about you

### Character System

- Import JSON character cards to set personality
- Upload custom avatars for visual representation
- Configure voice and speech patterns
- Future: 3D avatar support and animations

### Module System

- Add plugins to extend functionality
- Configure TTS/STT engines (ElevenLabs, Fish Speech)
- Select LLM backends
- Install custom agents and workflows

### Default Chat Workflow (New)

Chat execution is driven by a versioned “Default Chat” workflow stored in the DB. This centralizes LLM selection and connection wiring so future enhancements (e.g., search, moderation) are just workflow edits.

- List workflows: `GET /api/workflows`
- Get default workflow: `GET /api/workflows/default`
- Get by id: `GET /api/workflows/{id}`

Seed the default workflow with: `SwAIvyn/scripts/dev-seed-workflows.ps1 -Yes`.

### TTS/Voice Configuration

SwAIvyn defaults to Fish Speech for TTS (local, privacy‑friendly). A minimal proxy container is deployed behind Traefik and serves `/health`, `/voices`, `/tts`, and `/tts/clone`:

- Start via dev script: `SwAIvyn\scripts\run_dev.ps1`
- Build images: `SwAIvyn\scripts\build-stack.ps1 -Target tts -Pull`
- Voices directory (bind‑mounted): `SwAIvyn/speech/TTS/openaudio-s1-mini/voices`
  - Supported layouts:
    - `voices.json` (either `["name"]` or `{ "voices": [{"name":"jazzy"}, ...] }`)
    - `*.wav` directly under `voices/`
    - One‑level subfolders containing `*.wav` (folder name used as voice id)
- Upstream pass‑through: set `UPSTREAM_TTS` (defaults to `http://host.docker.internal:8080`) to forward synth to a full Fish Speech server if available.

### Build Targets (Swarm)

Use `SwAIvyn\scripts\build-stack.ps1` to rebuild images for the Swarm stack.

- List targets: `-List`
- Build all: `-All -Pull`
- Build groups: `-Target tts,infra,kanban,app`
- Build individual: `-Target wekan`, `-Target postgres`, `-Target tts-proxy`

Groups:
- `tts`: tts-proxy, tts-11labs-adapter, stt (pulls remote)
- `infra`: postgres, qdrant, neo4j, temporal (pulls remote)
- `kanban`: wekan, mongo, postgres:15 (pulls remote)
- `app`: bff, frontend, orchestrator, workers (local Dockerfiles)
- The Settings → Voice tab lists voices from `/voices`, lets you test, and saves per‑user voice.

### Federation

- AI-to-AI communication across instances
- Coordinate calendars, share information
- Pass messages between users
- Maintain appropriate privacy boundaries

## 🔧 Configuration

All settings can be configured through the Settings UI, including:

- Account management
- Network settings and federation
- Character personality and appearance
- Voice and speech preferences
- Backup locations and schedule

## 🛣️ Roadmap

- [x] Core chat functionality
- [x] Basic AI personality system
- [ ] Voice-first room interface
- [ ] Federation between instances
- [ ] Memory management UI
- [ ] Plugin system
- [ ] 3D avatar support
- [ ] Mobile companion app

See the [project board](https://github.com/SweetingTech/SwAIvyn/projects) for detailed progress.

## 💻 Technical Architecture

SwAIvyn hybrid dev stack:

- BFF (FastAPI, Python) + Orchestrator worker (Temporal) on host
- React (Vite) on host
- Infra in Docker: Temporal, Postgres, Qdrant, Neo4j, STT, optional 11Labs adapter/TTS
- Fish Speech TTS on host (default in dev) or in Docker (profile `tts`)

### Per‑User LLM Dataflow

- Settings (per user)
  - Save Engine/Model: `PUT /api/chat/settings/{userId}`
  - Save Connections: `PUT /api/settings/connections`
- Chat → Send
  - Calls `POST /api/conversation/chat` with `engine` + `model`
  - BFF launches the engine‑specific workflow
  - Worker calls only that engine/model; no fallback
  - TTS synthesizes via host TTS or adapter URLs
  - Conversations
    - Create: `POST /api/conversation` with `title` and `userId`
    - Delete: `DELETE /api/conversation/{id}` (idempotent: 204 when already deleted)
    - Ownership enforced; admin can manage any conversation
  - Dashboard
    - Admin-only view that surfaces the active LLM engine and model from provider status checks

Authorization: users can modify/read only their settings/conversations; admin can see all.

### Effective User Resolution (Frontend)

- Pages use a shared hook `useEffectiveUser` that resolves the active user id in a robust way:
  1) `useAuth().user?.id`
  2) `useInitialization().user?.id`
  3) Fallback to `/api/auth/me` when a token is present
- The hook also exposes `headers` with `Authorization` automatically, used in fetch()/axios calls throughout the app.

### Agents Integration (Workers)

- Workers orchestrator (Docker) serves the agent catalog: `GET http://localhost:8000/api/agents`.
- BFF proxies this for the UI at: `GET /api/agents/catalog`.
- Runtime agent activity (running/completed) remains under BFF `/api/agents` endpoints.

See `docs/AGENTS_AND_WORKFLOWS.md` for how the UI will expose catalog, per‑user enable/disable toggles, and user‑owned YAML uploads.

### SQLite VSS Integration

The application uses SQLite VSS for efficient vector similarity search:

- Pre-built DLL file located in the `assets` directory
- Test project in `TestSqliteVssProject` for verifying VSS functionality
- PowerShell build scripts for customized builds
- Ensure `VectorServerAvailable` is set to `true` in `appsettings.json`

When deploying to a new system, copy the `sqlite-vss.dll` file to `assets` directory before building.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- All the open-source AI and NLP communities
- Character card creators and standards
- Tamagotchi for the inspiration

---

<div align="center">
  <i>SwAIvyn: Your AI companion that lives in your home network</i>
</div>

## Agents Feature

The Agents feature allows SwAIvyn to delegate tasks to external workers, typically implemented as Python FastAPI services. This enables SwAIvyn to offload specialized or long-running jobs and monitor their progress.

### Backend Configuration

To use the Agents feature, you need to configure the base URL of your external worker API in the `appsettings.json` file of the SwAIvyn backend project. Add the following key:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=SwAIvyn.db"
  },
  "WorkerApiBaseUrl": "http://YOUR_WORKER_IP_OR_HOSTNAME:8000", // Replace with your worker's actual address
  "Logging": {
    // ... existing logging settings
  }
}
```
Ensure `WorkerApiBaseUrl` points to the correct address and port of your FastAPI worker, without a trailing slash.

### External FastAPI Worker Setup

The Agents feature is designed to communicate with an external worker application built with FastAPI.

1.  **Environment**: Set up a Python environment with `fastapi` and `uvicorn`.
2.  **Implementation**: The worker should expose at least the following endpoints:
    *   `POST /tasks`: To receive a new task from SwAIvyn. Expects a payload like `{"agent_id": "...", "payload": {"goal": "...", "agentName": "..."}}`. Should return a `{"task_id": "..."}`.
    *   `GET /tasks/{task_id}`: To poll for the status of a task. Should return `{"status": "queued|in-progress|completed", "result": "..."}`.
    *   `GET /health` (optional but good practice): To check if the worker is alive.
3.  **Running the Worker**:
    ```bash
    # Example: Navigate to your worker's directory
    # uvicorn main:app --host 0.0.0.0 --port 8000
    # Replace 'main:app' with your actual FastAPI application instance.
    ```

### SwAIvyn API Endpoints for Agents

The SwAIvyn backend now exposes the following API endpoints to manage agents:

*   **`GET /api/agents`**: Retrieves a list of all configured agents.
*   **`POST /api/agents/{id}/start`**: Starts the agent with the specified `id`. SwAIvyn will then dispatch a task to the configured worker.
*   **`POST /api/agents/{id}/stop`**: Marks the agent with the specified `id` as "stopped" in SwAIvyn. (Note: This primarily updates SwAIvyn's internal state; a full cancel on the worker side would require additional implementation on the worker and in SwAIvyn's `AgentService`).

### Frontend UI

A new **Agents** tab is available in the SwAIvyn frontend. This tab allows you to:
*   View all configured agents and their current status.
*   See when an agent was last run and how many tasks it has completed.
*   Start and Stop agents.
*   The status of agents is polled periodically to reflect updates from the worker.

---

## Developer Quickstart

SwAIvyn uses a hybrid development approach: application services (BFF, Orchestrator, Frontend) run on the host with hot reload, while infrastructure services run in Docker containers.

### Prerequisites:
- **Docker Desktop** (required for infrastructure)
- **Python 3.11+** (`python --version`)
- **Node 18+** (`node --version`)

### Quick Start:
```powershell
# Start everything (Docker + non-Docker services)
.\dev-run.ps1

# Stop everything cleanly
.\dev-shutdown.ps1
```

### Development Endpoints:
- **UI**: http://localhost:5173
- **BFF API**: http://localhost:5000 (health: `/healthz`, `/api/readyz`)
- **Temporal**: localhost:7233
- **Qdrant**: http://localhost:6333
- **Neo4j**: http://localhost:7474 (bolt: `localhost:7687`)

### Default Test Users:
- **admin** / admin1234
- **Mari** / mari1234  
- **DJay** / djay1234

### Advanced Options:
```powershell
# Frontend only development
.\dev-run.ps1 -FrontendOnly

# Backend only development  
.\dev-run.ps1 -BackendOnly

# Complete removal (containers + networks)
.\dev-shutdown.ps1 -DownCompose -Prune
```

See `docs/HYBRID_DEV.md` for details, `docs/BARE_METAL.md` for a container‑free deployment guide, and `docs/DATAFLOW.md` for architecture diagrams.
