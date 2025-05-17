# Phase One Dataflow Overview

This document describes the dataflow and architecture of the SwAIvyn project backend and frontend components implemented in Phase One.

## Backend

### 1. ASP.NET Core Hosting
- The backend is hosted using ASP.NET Core (.NET 7).
- Program.cs configures services including controllers, SignalR hubs, Swagger, CORS, and EF Core with SQLite.

### 2. Database Models and EF Core
- SQLite database configured with connection string in appsettings.json.
- ApplicationDbContext manages entities:
  - AppUser: User profile with authentication info (username, password hash, PIN, recovery phrase).
  - AvatarInfo: AI character profiles linked to users.
  - MemoryItem: User memory items.
  - ChatHistory: Stores chat messages per user and conversation.
  - Settings: User and system settings.

### 3. Authentication
- AuthController provides API endpoints for:
  - User registration (username, password, PIN, recovery phrase).
  - Username/password login.
  - PIN code login.
- Passwords hashed with SHA256.

### 4. LLM Connector Service
- LlmConnectorService connects to local LLM engines:
  - Ollama API at http://localhost:11434
    - Discovers available models via GET /v1/models.
    - Sends prompts to selected model via POST /v1/completions.
  - LM Studio API at http://localhost:5000
    - Gets current model via GET /model.
    - Sends prompts via POST /generate.
- Supports dynamic model selection for Ollama.

### 5. Core Services Placeholders
- ChatService provides placeholder methods for chat history and sending messages.

## Frontend

### 1. React Project Setup
- Frontend built with React 18 using Vite.
- TailwindCSS installed and configured for styling.
- React Router used for routing with multiple pages.

### 2. Layout Components
- Basic layout components include Layout.tsx and Navigation.tsx.

### 3. Authentication Screens
- LoginPage.tsx provides login UI supporting username/password and PIN login.
- Connects to backend AuthController APIs.

## Ports and Mappings

- Backend ASP.NET Core server runs on default port (e.g., 5000 or 5001 for HTTPS).
- CORS configured to allow frontend at http://localhost:3000.
- Ollama API expected at http://localhost:11434.
- LM Studio API expected at http://localhost:5000.
- SignalR hubs mapped at:
  - /hubs/chat
  - /hubs/voice
  - /hubs/notification

## Open Hooks and Extensibility

- LlmConnectorService interface allows adding more LLM engines.
- AuthController can be extended for additional authentication methods.
- Frontend routing and components designed for easy expansion.

---

This dataflow overview summarizes the architecture and key components implemented in Phase One.
