# Phase One Documentation (Updated)

This document provides detailed documentation of the work completed in Phase One of the SwAIvyn project, including setup instructions, API endpoints, configuration, usage notes, and updated information after comprehensive audit.

## Backend Setup

### Project Structure
- ASP.NET Core (.NET 7) project with controllers, SignalR hubs, and EF Core integration.
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
- LM Studio API base URL: http://localhost:5000
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

## Ports and URLs

| Service           | URL                          |
|-------------------|------------------------------|
| Backend API       | http://localhost:5000 (default) |
| Frontend          | http://localhost:3000         |
| Ollama API        | http://localhost:11434        |
| LM Studio API     | http://localhost:5000         |
| SignalR Chat Hub  | /hubs/chat                   |
| SignalR Voice Hub | /hubs/voice                  |
| SignalR Notify Hub| /hubs/notification           |

## How to Run

1. Start Ollama and LM Studio local servers.
2. Run backend ASP.NET Core server.
3. Run frontend React app with `npm run dev`.
4. Use LoginPage to authenticate.
5. Use LlmConnectorService to interact with local LLMs.

## Notes

- Passwords are hashed with SHA256.
- Recovery phrases are generated as GUID strings.
- LlmConnectorService supports dynamic Ollama model selection.
- Frontend and backend communicate via REST APIs and SignalR hubs.
- Core services placeholders have been replaced with live implementations where applicable.

---

This updated documentation reflects the current state of the project after a comprehensive audit and serves as a reliable reference for developers and users.
