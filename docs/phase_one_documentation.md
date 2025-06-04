# Phase One Documentation (Updated)

This document provides detailed documentation of the work completed in Phase One of the SwAIvyn project, including setup instructions, API endpoints, configuration, usage notes, and updated information after comprehensive audit.

## Backend Setup

### Project Structure
- ASP.NET Core (.NET 8) project with controllers, SignalR hubs, and EF Core integration.
- Program.cs configures services and middleware including CORS, Swagger, SignalR hubs, and EF Core with SQLite.

### Database
- SQLite database configured via appsettings.json.
- Entities managed by ApplicationDbContext:
  - AppUser: User profiles with authentication data (username, password hash, PIN, recovery phrase).
  - AvatarInfo: AI character profiles linked to users.
  - MemoryItem: User memory items.
  - ChatHistory: Stores chat messages per user and conversation.
  - Settings: User and system settings.

### Authentication API
- AuthController exposes:
  - POST /api/auth/register: Register new user with username, password, PIN, recovery phrase.
  - POST /api/auth/login: Login with username and password.
  - POST /api/auth/pin-login: Login with username and PIN code.
- Passwords hashed with SHA256.
- Recovery phrases generated as GUID strings.

### LLM Connector Service
- LlmConnectorService connects to local LLM engines Ollama and LM Studio.
- Ollama API base URL: http://localhost:11434
- LM Studio API base URL: http://localhost:1234 (updated from 5000 to avoid port conflicts)
- Methods:
  - GetOllamaModelsAsync(): Lists available Ollama models dynamically.
  - GetLmStudioModelAsync(): Gets current LM Studio model.
  - GenerateResponseAsync(prompt, engine, model): Sends prompt to selected engine/model.

### Core Services
- ChatService provides chat history and message sending placeholders.
- LlmConnectorService fully implemented with live connections to Ollama and LM Studio.

## Frontend Setup

### React Project
- Built with React 18 and Vite.
- TailwindCSS for styling.
- React Router for routing with multiple pages.

### Components
- Layout.tsx and Navigation.tsx for basic layout.
- LoginPage.tsx for authentication UI supporting username/password and PIN login.
- Frontend communicates with backend APIs and SignalR hubs.

## Service Architecture

SwAIvyn now uses a DNS-like naming system for service discovery:

| Service           | Logical Name                 | Default URL                   | User Configurable |
|-------------------|------------------------------|-------------------------------|-------------------|
| Backend API       | api                          | http://localhost:5000         | No                |
| Ollama API        | ollamaApi                    | http://localhost:11434        | **Yes**           |
| LM Studio API     | lmStudioApi                  | http://localhost:1234         | **Yes**           |
| SignalR Chat Hub  | chatHub                      | /hubs/chat                    | No                |
| SignalR Voice Hub | voiceHub                     | /hubs/voice                   | No                |
| SignalR Notify Hub| notificationHub              | /hubs/notification            | No                |

The Configuration Service provides dynamic service discovery, eliminating the need for hardcoded ports. Users can configure the URLs for LLM services (Ollama, LM Studio) through the Settings page under the "Connections" tab.

## How to Run

### Quick Start (Recommended)

1. Double-click `SwAIvyn.cmd` in the project root
2. The application will build (first time only) and launch automatically
3. Interact with the assistant through your web browser

### Manual Development Setup

1. Start Ollama and LM Studio local servers
2. Run backend ASP.NET Core server with `dotnet run`
3. Run frontend React app with `npm run dev`
4. Use LoginPage to authenticate
5. Use LlmConnectorService to interact with local LLMs

## Notes

- Passwords are hashed with SHA256.
- Recovery phrases are generated as GUID strings.
- LlmConnectorService supports dynamic Ollama model selection.
- Frontend and backend communicate via REST APIs and SignalR hubs.
- Core services placeholders have been replaced with live implementations where applicable.

## Self-Contained Application

SwAIvyn is now packaged as a self-contained application:

- Single executable (.exe) with no external dependencies
- Frontend embedded in the backend application
- Automatic browser launch on startup
- DNS-like naming system for service discovery
- No need to run npm or dotnet commands separately

See [Self-Contained Application Guide](self_contained_application.md) and [Build and Deployment Guide](build_and_deployment.md) for more details.

---

This updated documentation reflects the current state of the project after implementing the self-contained application architecture and DNS-like naming system.
