# Overview

SwAIvyn is a privacy-focused, federated AI assistant with dual interfaces and Tamagotchi-like features. It runs entirely on your local network with no external data transmission. The application features both traditional text-based chat and an immersive voice-first interface, supporting AI-to-AI communication across instances and customizable personalities through character cards.

# User Preferences

Preferred communication style: Simple, everyday language.

# System Architecture

## Frontend Architecture
- **Framework**: React 18 with TypeScript
- **Build Tool**: Vite with hot reload support
- **Styling**: TailwindCSS with custom components
- **State Management**: Zustand for global state
- **Real-time Communication**: SignalR for WebSocket connections
- **Routing**: React Router DOM for navigation
- **HTTP Client**: Axios for API communication

## Backend Architecture
- **Primary Backend**: FastAPI (Python) serving as Backend-for-Frontend (BFF)
- **Orchestration**: Temporal workflows for AI processing pipelines
- **Authentication**: JWT tokens with bcrypt password hashing
- **API Design**: RESTful endpoints with WebSocket support for real-time features

## Data Storage Solutions
- **Primary Database**: SQLite with Entity Framework Core (.NET) or SQLAlchemy (Python)
- **Vector Search**: SQLite-VSS extension for semantic similarity search
- **Graph Database**: Neo4j for relationship mapping and memory connections
- **Session Storage**: JSON files on filesystem for chat messages
- **File Storage**: Local filesystem for avatars, uploads, and assets

## Authentication and Authorization
- **Single-User Model**: Application designed for one primary user with default user ID
- **Authentication Methods**: Username/password, PIN code, and recovery phrases
- **Session Management**: JWT tokens for API access
- **Security**: Local-only operation with no external authentication services

## AI Integration
- **LLM Engines**: Support for Ollama, LM Studio, OpenAI, Claude, and vLLM
- **Model Selection**: User-configurable per conversation
- **Context Management**: Automatic context window handling
- **Character System**: Importable character cards with custom personalities

## Voice and Audio
- **Text-to-Speech**: Fish Speech integration with voice cloning capabilities
- **Speech-to-Text**: Whisper service for voice input
- **Audio Processing**: Real-time voice streaming and playback
- **Voice Management**: Custom voice upload and management system

# External Dependencies

## Core Infrastructure
- **Docker**: Container orchestration for development and deployment
- **Docker Swarm**: Service orchestration with Traefik routing
- **Postgres**: Optional database for production deployments
- **Neo4j**: Graph database for memory relationships

## AI Services
- **Ollama**: Local LLM hosting (http://localhost:11434)
- **LM Studio**: Alternative local LLM hosting (http://localhost:1234)
- **OpenAI API**: External GPT models integration
- **Anthropic Claude**: External Claude models integration

## Vector and Search
- **Qdrant**: Vector database for semantic search (http://localhost:6333)
- **Weaviate**: Alternative vector database option (http://stabled:8080)
- **SQLite-VSS**: Local vector search extension

## Audio Services
- **Fish Speech TTS**: Local text-to-speech service (http://localhost:8081)
- **ElevenLabs**: External TTS service with API integration
- **Whisper**: Speech-to-text service (http://localhost:9000)

## Development Tools
- **Temporal**: Workflow orchestration (http://localhost:7233)
- **Traefik**: Reverse proxy and load balancer
- **Node.js**: Frontend build tooling
- **Python**: Backend services and AI processing

## Optional Integrations
- **Google Workspace**: Gmail, Calendar, and Drive API integration
- **MCP Servers**: Modular plugin architecture support
- **Browsh**: Text-based web browsing capabilities