# SwAIvyn Project Structure

> **Note:** This document outlines the legacy .NET-oriented layout. The current production stack centers on the FastAPI BFF and React frontend described in the README.

This document provides a comprehensive overview of the SwAIvyn project folder and file layout, including the Fish Speech TTS integration.

## 📁 Root Directory Structure

```
SwAIvyn/
├── 📁 backend/                    # .NET Core backend application
├── 📁 frontend/                   # React frontend application
├── 📁 speech/                     # Speech processing (TTS/STT)
├── 📁 search/                     # Python search service
├── 📁 scripts/                    # Build and utility scripts
├── 📁 docs/                       # Documentation
├── 📁 data/                       # Application data (created at runtime)
├── 📁 logs/                       # Application logs (created at runtime)
├── 📁 dll/                        # Required DLL files
├── 📁 dnd_dll/                    # Source DLL files for build
├── 📁 Sqldatabase/                # SQLite database files
├── 📄 .gitignore                  # Git ignore rules
├── 📄 SwAIvyn.exe                 # Main executable (generated)
└── 📄 README.md                   # Project overview
```

## 🔧 Backend Structure (`/backend/`)

```
backend/
├── 📁 Configuration/              # Configuration classes
│   ├── 📄 FishSpeechOptions.cs   # Fish Speech TTS configuration
│   └── 📄 ...                    # Other config classes
├── 📁 Controllers/                # API controllers
│   ├── 📄 TtsController.cs       # TTS API endpoints
│   ├── 📄 CharacterController.cs # Character management
│   ├── 📄 ConversationController.cs
│   ├── 📄 LlmController.cs       # LLM integration
│   └── 📄 ...                    # Other controllers
├── 📁 Data/                       # Database context and entities
│   ├── 📁 Entities/               # Database entities
│   └── 📄 ApplicationDbContext.cs
├── 📁 HostedServices/             # Background services
│   ├── 📄 FishSpeechHostedService.cs  # Fish Speech TTS service
│   ├── 📄 BackupService.cs       # Database backup
│   └── 📄 ...                    # Other hosted services
├── 📁 Hubs/                       # SignalR hubs
│   ├── 📄 ChatHub.cs             # Real-time chat
│   ├── 📄 VoiceHub.cs            # Voice communication
│   └── 📄 NotificationHub.cs     # Notifications
├── 📁 Middleware/                 # Custom middleware
├── 📁 Services/                   # Business logic services
│   ├── 📄 FishSpeechTtsService.cs # Fish Speech TTS integration
│   ├── 📄 ElevenLabsTtsService.cs # ElevenLabs TTS integration
│   ├── 📄 LlmConnectorService.cs # LLM API connections
│   ├── 📁 Graph/                 # Neo4j graph services
│   ├── 📁 VectorStore/           # Vector database services
│   └── 📄 ...                    # Other services
├── 📄 Program.cs                  # Application entry point
├── 📄 SwAIvyn.csproj             # Project file
├── 📄 appsettings.json           # Configuration settings
└── 📁 wwwroot/                   # Static web files (generated)
```

## 🎨 Frontend Structure (`/frontend/`)

```
frontend/
├── 📁 public/                     # Public assets
├── 📁 src/                        # Source code
│   ├── 📁 components/             # React components
│   ├── 📁 pages/                  # Page components
│   ├── 📁 services/               # API services
│   │   ├── 📄 ttsService.ts      # TTS API integration
│   │   └── 📄 ...                # Other API services
│   ├── 📁 hooks/                  # Custom React hooks
│   ├── 📁 utils/                  # Utility functions
│   └── 📄 App.tsx                # Main app component
├── 📁 dist/                       # Built frontend (generated)
├── 📄 package.json               # Node.js dependencies
├── 📄 vite.config.ts             # Vite configuration
└── 📄 tsconfig.json              # TypeScript configuration
```

## 🎤 Speech Processing Structure (`/speech/`)

```
speech/
├── 📄 DOWNLOAD_INSTRUCTIONS.txt   # Setup instructions
├── 📁 STT/                        # Speech-to-Text (future)
└── 📁 TTS/                        # Text-to-Speech
    ├── 📁 chatterbox/             # Alternative TTS engine
    └── 📁 openaudio-s1-mini/      # Fish Speech TTS
        ├── 📁 fish-speech/        # Fish Speech repository
        │   ├── 📁 fish_speech/    # Core Fish Speech library
        │   ├── 📁 tools/          # Training and utility tools
        │   ├── 📁 docs/           # Fish Speech documentation
        │   ├── 📄 pyproject.toml  # Python project config
        │   └── 📄 ...             # Other Fish Speech files
        ├── 📁 voices/             # Voice samples and embeddings
        │   ├── 📁 glados/         # GLaDOS voice samples
        │   │   ├── 📄 glados.wav  # Audio sample
        │   │   ├── 📄 glados.txt  # Text transcript
        │   │   └── 📄 glados.pt   # Voice embedding (generated)
        │   ├── 📁 jazzy/          # Jazzy voice samples
        │   └── 📁 scarlet/        # Scarlet voice samples
        ├── 📄 fish_speech_api.py  # Custom API wrapper
        ├── 📄 model.pth           # Pre-trained model weights
        ├── 📄 codec.pth           # Audio codec weights
        ├── 📄 config.json         # Model configuration
        ├── 📄 tokenizer.tiktoken  # Text tokenizer
        ├── 📄 special_tokens.json # Special tokens config
        ├── 📄 start-fish-speech.cmd   # Windows startup script
        ├── 📄 start-fish-speech.ps1   # PowerShell startup script
        ├── 📄 start.sh            # Linux startup script
        ├── 📄 Dockerfile          # Docker container config
        ├── 📄 docker-compose.yml  # Docker Compose config
        └── 📄 README.md           # Fish Speech setup guide
```

## 🔍 Search Service Structure (`/search/`)

```
search/
├── 📄 search.py                   # FastAPI search service
├── 📄 requirements.txt            # Python dependencies
└── 📄 ...                        # Other search-related files
```

## 🛠️ Scripts Structure (`/scripts/`)

```
scripts/
├── 📄 new.ps1                     # Main build script
├── 📄 build-app.ps1              # Application build script
├── 📄 run.cmd                     # Quick run script
└── 📄 ...                        # Other utility scripts
```

## 📚 Documentation Structure (`/docs/`)

```
docs/
├── 📄 README.md                   # Main documentation
├── 📄 SwAIvyn_Project_Structure.md # This file
├── 📄 fish_speech_integration.md  # Fish Speech TTS guide
├── 📄 voice_management_implementation.md # Voice management
├── 📄 SwAIvyn_DataFlow.md         # System data flow
├── 📄 SwAIvyn_System_Documentation.md # System overview
├── 📄 Neo4j_Configuration_Guide.md # Neo4j setup
├── 📄 build_and_deployment.md     # Build instructions
├── 📄 Port_Usage.md               # Network ports used
└── 📄 ...                        # Other documentation files
```

## 💾 Runtime Data Structure (`/data/`)

```
data/                              # Created automatically at runtime
├── 📁 avatars/                    # Character avatar images
├── 📁 uploads/                    # User uploaded files
├── 📁 backups/                    # Database backups
├── 📁 modules/                    # Application modules
├── 📁 sessions/                   # User session data
└── 📁 characters/                 # Character card files
```

## 📊 Database Structure (`/Sqldatabase/`)

```
Sqldatabase/
├── 📄 swai-vyn.db                # Main SQLite database
├── 📄 swai-vyn.db-shm            # Shared memory file (WAL mode)
└── 📄 swai-vyn.db-wal            # Write-ahead log file (WAL mode)
```

## 📝 Logs Structure (`/logs/`)

```
logs/                              # Created automatically at runtime
├── 📄 SwAIvyn_YYYYMMDD_HHMMSS.log # Application logs
├── 📄 crash_YYYYMMDD_HHMMSS.txt  # Crash logs
└── 📄 ...                        # Historical log files
```

## 🔗 DLL Structure (`/dll/` and `/dnd_dll/`)

```
dll/                               # Runtime DLL location
├── 📄 sqlite-vss.dll            # SQLite vector search extension
├── 📄 faiss.dll                 # Facebook AI Similarity Search
└── 📄 libopenblas.dll           # Linear algebra library

dnd_dll/                          # Source DLL files (tracked in git)
├── 📄 sqlite-vss.dll            # Source: SQLite vector search
├── 📄 faiss.dll                 # Source: FAISS library
└── 📄 libopenblas.dll           # Source: OpenBLAS library
```

## 🎯 Key File Descriptions

### Backend Core Files

- **`Program.cs`**: Application entry point, dependency injection, middleware configuration
- **`appsettings.json`**: Configuration settings for all services including Fish Speech TTS
- **`SwAIvyn.csproj`**: .NET project file with dependencies and build settings

### Fish Speech TTS Integration

- **`FishSpeechTtsService.cs`**: Main service for Fish Speech TTS integration
- **`FishSpeechHostedService.cs`**: Background service for managing Fish Speech process
- **`FishSpeechOptions.cs`**: Configuration options for Fish Speech TTS
- **`TtsController.cs`**: API endpoints for TTS functionality
- **`fish_speech_api.py`**: Custom Python API wrapper for Fish Speech

### Voice Management

- **`voices/`**: Directory containing voice samples and embeddings
  - **`*.wav`**: Audio samples for voice cloning
  - **`*.txt`**: Text transcripts corresponding to audio samples
  - **`*.pt`**: Generated voice embeddings for fast inference

### Configuration Files

- **`config.json`**: Fish Speech model configuration
- **`tokenizer.tiktoken`**: Text tokenization model
- **`special_tokens.json`**: Special tokens for text processing
- **`model.pth`**: Pre-trained Fish Speech model weights
- **`codec.pth`**: Audio codec model weights

## 🚀 Startup Scripts

### Windows
- **`start-fish-speech.cmd`**: Windows batch script to start Fish Speech TTS
- **`start-fish-speech.ps1`**: PowerShell script with advanced options

### Linux/macOS
- **`start.sh`**: Shell script for Unix-based systems

### Docker
- **`Dockerfile`**: Container definition for Fish Speech TTS
- **`docker-compose.yml`**: Multi-container orchestration

## 🔧 Build and Development

### Build Scripts
- **`new.ps1`**: Main build script that builds backend, copies DLLs, and prepares frontend
- **`build-app.ps1`**: Focused application build script

### Development Workflow
1. **Frontend Development**: `npm run dev` in `/frontend/` (localhost:5173)
2. **Backend Development**: Run `SwAIvyn.exe` (localhost:5000)
3. **Fish Speech TTS**: Auto-started by backend (localhost:8081)

## 📡 Network Ports

- **5000**: Backend API server
- **5173**: Frontend development server (Vite)
- **7474**: Neo4j HTTP interface
- **7687**: Neo4j Bolt protocol
- **8080**: Weaviate vector database (optional)
- **8081**: Fish Speech TTS API
- **11434**: Ollama API (external)
- **1234**: LM Studio API (external)

## 🔄 Data Flow

```
Frontend (5173) ←→ Backend API (5000) ←→ SQLite Database
                           ↓
                    Neo4j Graph DB (7474/7687)
                           ↓
                    Fish Speech TTS (8081)
                           ↓
                    External LLMs (Ollama/LM Studio)
```

This structure provides a complete overview of the SwAIvyn project organization, with special emphasis on the Fish Speech TTS integration and voice management capabilities.
