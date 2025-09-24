# SwAIvyn

<div align="center">
  
![SwAIvyn Logo](https://via.placeholder.com/200x200.png?text=SwAIvyn)

**A federated AI assistant with dual interfaces and Tamagotchi-like features**

[![MIT License](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Python](https://img.shields.io/badge/Python-3.11+-3776AB)](https://www.python.org/)
[![FastAPI](https://img.shields.io/badge/FastAPI-0.104+-009688)](https://fastapi.tiangolo.com/)
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
  - 🔗 **External Agent Integration** - Connect specialized AI workers on separate servers
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

- **Python 3.11+** with pip
- **Node.js** (v18+) and npm
- **PostgreSQL** database (local or hosted)
- **Docker** (recommended for full stack, optional for simplified deployment)

### Installation

#### Option 1: Bare Metal Installation (Windows) 🆕

For the complete SwAIvyn experience without Docker complexity:

**Complete Setup (One Command)**
```powershell
# Install all dependencies and start entire stack
.\scripts\setup-bare-metal.ps1 -InstallDependencies

# Start all services after setup
.\start-bare-metal.ps1
```

**What You Get:**
- ✅ **All Services Running Natively**: PostgreSQL, Neo4j, Qdrant, Temporal, TTS, Frontend, Backend
- ✅ **No Docker Overhead**: Direct Windows performance
- ✅ **Simple Management**: Easy start/stop scripts
- ✅ **Process Tracking**: Clean service management and monitoring
- ✅ **Custom Configuration**: All services optimized for local deployment

**Access Your Application:**
- **Frontend**: http://localhost:5000
- **Backend API**: http://localhost:8000
- **Neo4j Browser**: http://localhost:7474
- **Vector Database**: http://localhost:6333

**Service Management:**
```powershell
# Setup creates service management scripts
.\scripts\setup-bare-metal.ps1 -InstallDependencies

# Generated scripts will be available after setup:
# - .\start-bare-metal.ps1 (created by setup)
# - .\stop-bare-metal.ps1 (created by setup)
```

#### Option 2: Hybrid Development (Recommended for Local Development)

SwAIvyn uses a hybrid architecture for optimal development experience:

**🎯 Hybrid Architecture Benefits:**
- **Hot Reload**: Frontend, Backend, and Orchestrator run on host for instant code changes
- **Infrastructure Stability**: Databases and services run in Docker containers
- **Best of Both**: Development speed + production-like infrastructure

**Quick Start:**
```powershell
# Start hybrid development environment
.\dev-run.ps1

# Stop everything cleanly  
.\dev-shutdown.ps1
```

**Development Options:**
```powershell
# Frontend only (React/Vite hot reload)
.\dev-run.ps1 -FrontendOnly

# Backend only (FastAPI + Docker infrastructure)
.\dev-run.ps1 -BackendOnly

# Disable Traefik routing (use direct ports)
.\dev-run.ps1 -DisableTraefik
```

**🏠 Host Services (Hot Reload):**
- **Frontend** (React/Vite) - http://localhost:5173 (local) / :5000 (Replit)
- **Backend/BFF** (FastAPI) - http://localhost:5000 (local) / :8000 (Replit)
- **Orchestrator** (Temporal worker) - runs in background

**🐳 Docker Infrastructure Services:**
- **PostgreSQL** database - localhost:5432
- **Temporal** workflow service - localhost:7233  
- **Qdrant** vector database - localhost:6333
- **Neo4j** graph database - localhost:7474/7687
- **Fish Speech TTS** - localhost:8081
- **Whisper STT** - localhost:9000
- **ElevenLabs adapter** - localhost:8082
- **Traefik** reverse proxy - localhost:80

**Key Features:**
✅ **One-Command Setup** - Start everything with `.\dev-run.ps1`
✅ **Clean Shutdown** - Stop everything with `.\dev-shutdown.ps1`
✅ **Health Checks** - Waits for services to be ready before proceeding
✅ **Service Coordination** - Ensures dependencies are ready before starting dependent services
✅ **Remote Access Support** - Services accessible from other devices on your network
✅ **Traefik Integration** - Full routing with *.localhost domains
✅ **Process Management** - Tracks all running services for easy cleanup

**Available URLs:**
- **Frontend**: http://localhost:5173 (local) / http://localhost:5000 (Replit) or http://app.localhost:80
- **Backend API**: http://localhost:5000 (local) / http://localhost:8000 (Replit) or http://bff.localhost:80
- **Traefik Dashboard**: http://traefik.localhost:80 (local development only)
- **Infrastructure Services**:
  - Qdrant Vector DB: http://qdrant.localhost:80
  - Neo4j Graph DB: http://graph.localhost:80

**Remote Access:**
Both scripts configure services to be accessible from other devices on your network via your machine's IP address.

#### Cloud Development (Replit)

SwAIvyn runs seamlessly in cloud development environments like Replit:

**✅ Replit Environment Features:**
- **Simplified Architecture**: FastAPI backend + React frontend + PostgreSQL
- **Automatic Configuration**: Environment detection and CORS setup
- **Authentication Consistency**: Robust multi-user authentication across all pages
- **Port Management**: Automatic port binding (frontend:5000, backend:8000)
- **Dashboard Adaptation**: Environment-specific dashboard showing current stack status

**Environment Detection:**
The application automatically detects your environment and adapts:
- **Replit**: Shows environment info instead of Traefik dashboard
- **Local Development**: Shows full Traefik routing dashboard
- **Production**: Optimized for deployment scenarios

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

# Install backend dependencies
cd Services/bff
pip install -r requirements.txt

# Install frontend dependencies
cd ../../frontend
npm install

# Build frontend for production
npm run build
```

### First-Time Setup

1. When first launched, SwAIvyn will create a default admin account
2. You'll need to set a password and generate recovery phrases
3. Configure your AI's personality and avatar
4. Connect with other SwAIvyn instances on your network (optional)

### Access from Other Computers (LAN)

During development, both the frontend (Vite) and backend (FastAPI) can be reached from other devices on your LAN.

- Frontend: `http://<your-pc-ip>:5173` (local dev) / `http://<your-pc-ip>:5000` (Replit)
- Backend: `http://<your-pc-ip>:5000` (local dev) / `http://<your-pc-ip>:8000` (Replit)

Already configured in this repo:
- Vite binds to `0.0.0.0` so LAN clients can connect (see `frontend/vite.config.ts`).
- Backend runs on `0.0.0.0` for network accessibility.

Windows Firewall (PowerShell as Administrator):

```
# For local development
netsh advfirewall firewall add rule name="SwAIvyn Frontend" dir=in action=allow protocol=TCP localport=5173
netsh advfirewall firewall add rule name="SwAIvyn Backend" dir=in action=allow protocol=TCP localport=5000

# For Replit-style deployment  
netsh advfirewall firewall add rule name="SwAIvyn Frontend Replit" dir=in action=allow protocol=TCP localport=5000
netsh advfirewall firewall add rule name="SwAIvyn Backend Replit" dir=in action=allow protocol=TCP localport=8000
```

## 🔧 Recent Improvements (September 2025)

### 🖥️ Bare Metal Deployment Support 🆕
- **Complete Windows setup script** for Docker-free deployment with automatic dependency installation
- **Native service orchestration** managing 15+ services without containers
- **Optimized performance** with direct Windows process execution
- **Simplified deployment** with one-command setup and easy service management
- **Production-ready configuration** for PostgreSQL, Neo4j, Qdrant, Temporal, and TTS services

### 🛡️ Authentication Consistency
- **Fixed authentication bugs** where characters/agents showed on dashboard but not in settings/chat pages
- **Unified authentication pattern** across all frontend pages using `useEffectiveUser()` hook
- **Comprehensive auth audit** ensuring all API calls include proper JWT headers
- **Multi-user support verified** for all users (admin, mari, djay)

### 🔀 Hybrid Development Architecture
- **Optimized development workflow** with host services (Frontend, Backend, Orchestrator) for hot reload
- **Docker infrastructure services** for databases and AI services providing production-like environment
- **Fixed Temporal configuration** removing problematic BIND_ON_IP environment overrides that caused hangs
- **Improved service startup reliability** with proper health checks and dependency ordering

### 🖥️ Environment Detection & Dashboard
- **Smart environment detection** (Replit vs localhost vs production)
- **Adaptive dashboard** showing environment-appropriate information
- **Traefik integration** for local development, environment info for cloud deployment
- **TypeScript cleanup** with zero LSP diagnostics across the codebase

### 🌐 Cloud Development Support
- **Replit optimization** with automatic environment configuration
- **Port binding** properly configured for cloud development constraints
- **CORS enhancement** supporting both `*.repl.co` and localhost origins
- **Development workflow** streamlined for both local and cloud environments

### 📚 Documentation & Organization
- **Agent Stack Integration Guide** (800+ lines) with complete technical specifications
- **Consolidated documentation** with consistent kebab-case naming conventions  
- **Scripts folder cleanup** with redundant files moved to old-scripts archive
- **Comprehensive setup guides** for bare metal, Docker, and cloud deployments

## 🚧 Known Architecture Notes

- **SignalR Frontend Code Removed**: Earlier SignalR client remnants have been deleted, and the frontend now exclusively uses REST APIs for chat updates.
- **Current Communication**: All real-time updates rely on REST API polling rather than WebSocket/SignalR connections.
- **Development Ports**: Local development uses Frontend :5173 + Backend :5000; Replit/cloud environments use Frontend :5000 + Backend :8000.

## 🧾 Required Environment Variables

### FastAPI BFF

- `DATABASE_URL` – PostgreSQL connection string used for all persistence.
- `JWT_SECRET` – Secret key for signing and verifying access tokens.
- `ALLOWED_ORIGINS` *(optional)* – Comma-delimited list of origins allowed via CORS. Defaults to common localhost ports for development.

### Temporal Orchestrator Worker

- `TEMPORAL_HOST` – Temporal frontend host:port that the worker connects to.
- `ACTIVITY_THREADS` *(optional)* – Number of worker threads for activities (must be a positive integer, defaults to 8).
- `ORCHESTRATOR_HEALTH_PORT` *(optional)* – Port for the worker health endpoint (defaults to 8088).

### Frontend (Vite)

- `VITE_API_BASE_URL` – Base URL for the BFF API. Required in production builds; optional during development where Vite proxying is used.
- `VITE_STAGEWISE_ENABLED` *(optional)* – Enable the Stagewise toolbar integration when set to `true`.

## 🧩 Features in Detail

### Text Chat Interface

Traditional chat interface for keyboard-based interaction:
- Full conversation history with persistent storage
- File uploads and webcam integration
- Rich markdown and code support
- TTS playback of AI responses
- **Multi-user authentication** with proper data isolation

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
- **User-scoped access** ensuring privacy and data isolation

### Character System

- Import JSON character cards to set personality
- Upload custom avatars for visual representation
- Configure voice and speech patterns
- **Admin-controlled character creation** with user assignment
- Future: 3D avatar support and animations

### Module System

- Add plugins to extend functionality
- Configure TTS/STT engines (ElevenLabs, Fish Speech)
- Select LLM backends (Ollama, LM Studio, OpenAI, Claude)
- Install custom agents and workflows
- **Environment-aware configuration** for different deployment scenarios

### Default Chat Workflow (New)

Chat execution is driven by a versioned "Default Chat" workflow stored in the DB. This centralizes LLM selection and connection wiring so future enhancements (e.g., search, moderation) are just workflow edits.

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

- Account management with role-based access control
- Network settings and federation
- Character personality and appearance
- Voice and speech preferences
- LLM engine selection and configuration
- Backup locations and schedule

## 🛣️ Roadmap

- [x] Core chat functionality
- [x] Multi-user authentication system
- [x] External agent integration framework
- [x] Basic AI personality system
- [x] Environment-aware deployment
- [ ] Voice-first room interface
- [ ] Federation between instances
- [ ] Memory management UI enhancement
- [ ] Plugin system expansion
- [ ] 3D avatar support
- [ ] Mobile companion app

See the [project board](https://github.com/SweetingTech/SwAIvyn/projects) for detailed progress.

## 💻 Technical Architecture

SwAIvyn features a flexible architecture supporting multiple deployment scenarios:

- **Backend**: FastAPI (Python) with async PostgreSQL database and robust authentication
- **Frontend**: React 18 with TypeScript, Vite, and consistent authentication patterns
- **Authentication**: JWT tokens with bcrypt password hashing and proper user data isolation
- **Status Polling**: Efficient polling-based status updates with environment detection
- **External Agents**: Multi-tenant agent registry with user isolation and secure API keys
- **Infrastructure**: Multi-deployment support (Docker Swarm, bare metal Windows, cloud environments)

### Deployment Architectures

**🖥️ Bare Metal (Windows)**
- Native Windows services with Chocolatey dependency management
- Direct process execution for optimal performance
- Simplified networking without containerization overhead
- Ports: Frontend (5000), Backend (8000), PostgreSQL (5432), Neo4j (7474), etc.

**🐳 Hybrid Development (Cross-platform)**
- Host services (Frontend :5173, Backend :5000, Orchestrator) for hot reload
- Docker infrastructure (databases, TTS, Traefik) for stability
- Best development experience with production-like infrastructure
- Automatic service orchestration and health monitoring

**☁️ Cloud Development (Replit/etc)**
- Simplified 2-service architecture (Frontend + Backend + Managed DB)
- Automatic environment detection and configuration
- Optimized for development and prototyping

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
- **Authentication consistency** ensures all API calls include proper JWT headers

### Agents Integration (Workers)

- Workers orchestrator (Docker) serves the agent catalog: `GET http://localhost:8000/api/agents`.
- BFF proxies this for the UI at: `GET /api/agents/catalog`.
- Runtime agent activity (running/completed) remains under BFF `/api/agents` endpoints.

See `docs/EXTERNAL_AGENT_GUIDE.md` for basic integration and `docs/AGENT_STACK_INTEGRATION.md` for comprehensive technical specifications.

### PostgreSQL Data Layer

The FastAPI backend stores authentication, character, and conversation data in PostgreSQL via SQLAlchemy:

- `DATABASE_URL` controls the connection string (see environment variable section above).
- Development scripts spin up PostgreSQL 16 containers alongside Temporal and other infra services.
- Alembic/seed utilities initialize baseline users and settings during application startup.
- Vector and memory services integrate through dedicated workers; no SQLite components remain in the active stack.

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

## 🤖 External Agent Integration

SwAIvyn features a powerful external agent system that allows you to connect specialized AI workers and services running on separate servers. This enables distributed task processing while maintaining secure user data isolation.

### 🔑 Key Features

- **Multi-tenant Architecture**: Each user's agents and tasks are completely isolated
- **Secure Authentication**: JWT-based authentication for all agent operations
- **Task Management**: Full lifecycle management of agent tasks with status tracking
- **Status Monitoring**: Efficient polling-based status tracking
- **Flexible Agent Types**: Support for any type of external AI service or worker
- **Data Format Support**: Comprehensive support for text, images, vectors, and structured data

### 🚀 Quick Start

1. **Register Your Agent Service**:
   ```bash
   POST /api/agents/register
   Authorization: Bearer <your-jwt-token>
   
   {
     "name": "My AI Agent",
     "description": "Specialized task processor",
     "endpoint_url": "https://my-agent.example.com",
     "agent_type": "task_processor",
     "capabilities": ["text_processing", "data_analysis"]
   }
   ```

2. **Create Agent Tasks**:
   ```bash
   POST /api/agents/tasks
   Authorization: Bearer <your-jwt-token>
   
   {
     "registry_id": "agent-registry-id",
     "task_type": "process_document",
     "input_data": {"document": "content to process"},
     "priority": "normal"
   }
   ```

3. **Monitor Results**:
   ```bash
   GET /api/agents/tasks/{task_id}/results
   ```

### 📚 Complete Documentation

See the **[External Agent Connection Guide](docs/EXTERNAL_AGENT_GUIDE.md)** for basic integration and the **[Agent Stack Integration Guide](docs/AGENT_STACK_INTEGRATION.md)** for comprehensive technical specifications including:

- **Port Configuration**: Required ports and network setup
- **API Specifications**: Complete endpoint documentation with authentication
- **Data Format Standards**: File types, encoding, and structure requirements
- **Agent Registration**: Step-by-step service registration process
- **Data Ingestion**: How SwAIvyn receives and processes agent data
- **Vector Data Handling**: Embeddings, similarity search, and storage
- **Image Processing**: Format support, metadata, and storage patterns
- **Service Discovery**: Automatic agent detection and capability reporting

### 🎯 Core API Endpoints

- **Agent Registry**: `/api/agents/register` - Register and manage external agent services
- **Task Management**: `/api/agents/tasks` - Create and monitor agent tasks
- **Results**: `/api/agents/tasks/{task_id}/results` - Access completed task results
- **Data Ingestion**: `/api/agents/ingest` - Receive data from external agents
- **Status Polling**: `/api/agents/status` - Monitor agent health and availability
- **User Isolation**: All endpoints enforce user-scoped data access

### 🖥️ Management Interface

The SwAIvyn frontend provides comprehensive agent management:

- **Agent Registry**: View and manage all registered external agents
- **Task Dashboard**: Monitor active tasks and their progress  
- **Results Viewer**: Access completed task results with filtering and search
- **Status Monitoring**: Real-time updates on agent availability and performance
- **Data Visualization**: View ingested data with proper formatting and metadata
- **User Isolation**: Each user sees only their own agents and tasks

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
- **UI**: http://localhost:5173 (local) / http://localhost:5000 (Replit)
- **BFF API**: http://localhost:8000 (health: `/healthz`, `/api/readyz`)
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

See `docs/hybrid-development.md` for details, `docs/bare-metal-deployment.md` for a container‑free deployment guide, and `docs/architecture-and-dataflow.md` for architecture diagrams.