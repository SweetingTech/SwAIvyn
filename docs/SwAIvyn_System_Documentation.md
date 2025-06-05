# SwAIvyn System Documentation

## System Overview

SwAIvyn is a desktop application that provides an AI assistant interface with chat capabilities, memory management, and voice interaction. The application is built using a .NET/C# backend with a React frontend, packaged as a self-contained executable.

## Architecture

The application follows a client-server architecture:
- **Backend**: .NET 8.0 API with SignalR for real-time communication
- **Frontend**: React with TypeScript, using Vite as the build tool
- **Database**:
  - SQLite (WAL mode) for relational data
  - SQLite-VSS for vector embeddings
  - Neo4j for graph relationships
  - File system for chat message storage
- **Packaging**: Self-contained executable (.exe) with embedded frontend

## Core Components

### Backend Services

| Service | Purpose | Key Functions |
|---------|---------|--------------|
| `SimpleLoggerService` | Handles application logging | `LogInfo()`, `LogWarning()`, `LogError()`, `LogCritical()` |
| `ApplicationMonitorService` | Monitors application health | `ExecuteAsync()`, `LogApplicationStatus()` |
| `ConfigurationService` | Manages application settings | `GetSetting()`, `SetSetting()`, `GetAllSettings()` |
| `SettingsService` | Manages user settings | `GetSettingAsync()`, `SetSettingAsync()`, `GetSettingsAsync()` |
| `AuthService` | Handles user authentication | `Login()`, `Register()`, `VerifyPin()`, `GenerateToken()` |
| `FolderService` | Manages folder structure | `CreateFolderAsync()`, `GetFoldersAsync()`, `UpdateFolderParentAsync()` |
| `ConversationService` | Manages conversations | `CreateConversationAsync()`, `AppendMessageAsync()`, `GetLastOpenConversationAsync()` |
| `AiChatService` | Manages AI chat responses | `GenerateAndStoreResponseAsync()`, `GetCurrentLlmSettingsAsync()`, `SetDefaultLlmSettingsAsync()` |
| `LlmConnectorService` | Connects to language models | `GenerateResponseAsync()`, `GetOllamaModelsAsync()`, `GetLmStudioModelAsync()` |
| `MemoryService` | Manages user memories | `AddMemory()`, `GetMemories()`, `SearchMemories()` |
| `BrainService` | Manages vector search | `AddMemoryAsync()`, `SearchAsync()`, `DeleteMemoryAsync()` |
| `Neo4jService` | Manages graph database | `CreateNodeAsync()`, `CreateRelationshipAsync()`, `ExecuteQueryAsync()` |
| `BrainGraphService` | Combines vector and graph | `AddMemoryAsync()`, `SearchAsync()`, `GetGraphVisualizationAsync()` |
| `VoiceService` | Handles voice processing | `SpeechToText()`, `TextToSpeech()` |

### Frontend Components

| Component | Purpose | Key Functions |
|-----------|---------|--------------|
| `ChatSidebar` | Chat session management | Display, create, rename, delete folders and chat sessions |
| `FolderTree` | Folder structure UI | Display, create, organize folders |
| `ConversationList` | Conversation listing | Display, filter, select conversations |
| `ChatPage` | Main chat page | Manage chat state, handle session creation and switching |
| `ChatInterface` | Main chat UI | Message display, input handling |
| `MemoryManager` | Memory management UI | Add, view, search memories |
| `BrainExplorer` | Brain visualization | Display graph relationships |
| `SearchInterface` | Search functionality | Search conversations and memories |
| `SettingsPanel` | Application settings | Configure app behavior, LLM settings |
| `VoiceControls` | Voice interaction UI | Start/stop voice input, play TTS |
| `AuthScreens` | Login/registration UI | User authentication flows |

### Data Flow

1. **User Authentication**:
   - User credentials → `AuthController` → `AuthService` → Database
   - Response: JWT token → Frontend → Local storage

2. **Folder Management**:
   - Folder creation → `FolderController` → `FolderService` → Database
   - Folder retrieval: Database → `FolderService` → `FolderController` → Frontend

3. **Conversation Management**:
   - New conversation → `ConversationController` → `ConversationService` → Database + File system
   - Conversation retrieval: Database → `ConversationService` → `ConversationController` → Frontend
   - Folder organization: `FolderController` → `FolderService` → Database
   - Automatic session creation: First message → UUID assignment → Database
   - Session title generation: First message content → Truncated title
   - Session deletion: `ConversationController` → `ConversationService` → Cascade delete messages

4. **Chat Interaction**:
   - Empty session on startup: `ConversationService` → `GetLastOpenConversationAsync()` or new session
   - User message → `ConversationController` → `AiChatService` → `ConversationService` → File system + `ChatIndex`
   - LLM settings: `AiChatService` → `SettingsService` → User preferences (engine, model)
   - AI generation: `AiChatService` → `LlmConnectorService` → LLM API (Ollama/LM Studio)
   - Response storage: `AiChatService` → `ConversationService` → File system + `ChatIndex`
   - Session switching: `ChatSidebar` → `handleSelectConversation()` → Load messages

5. **Memory and Brain Operations**:
   - Memory creation → `BrainController` → `BrainService` → Vector store
   - Vector search: Query → `BrainController` → `BrainService` → Vector store → Results
   - Graph operations: `BrainGraphController` → `BrainGraphService` → `Neo4jService` → Graph database

6. **Voice Processing**:
   - Voice input → `VoiceController` → `VoiceService` → Text
   - Text response → `VoiceService` → Audio → Frontend → Playback

7. **Logging**:
   - Application events → `SimpleLoggerService` → Log files
   - Errors/crashes → Global exception handler → `SimpleLoggerService` → Crash logs

## Error Handling and Logging

### Logging System

The application uses a comprehensive logging system that captures:
- Regular application events (startup, shutdown)
- User interactions
- Errors and exceptions
- Application performance metrics

### Log Types

1. **Application Logs** (`SwAIvyn_[timestamp].log`):
   - INFO: Normal operations, startup, shutdown
   - WARNING: Potential issues that don't affect functionality
   - ERROR: Problems that affect specific features
   - CRITICAL: Severe issues that may crash the application

2. **Crash Logs** (`crash_[timestamp].txt`):
   - Created when unhandled exceptions occur
   - Contains detailed stack traces
   - Includes memory usage and process information

### Error Handling

1. **Global Exception Handler**:
   - Middleware that catches all unhandled exceptions
   - Logs detailed error information
   - Returns user-friendly error responses

2. **Application Monitor**:
   - Background service that tracks application health
   - Logs memory usage, CPU usage, and uptime
   - Helps identify performance issues

## Database Schema

### Tables

1. **AppUser**:
   - Id (PK)
   - Username
   - PasswordHash
   - PINCode
   - RecoveryPhrase
   - CreatedAt

2. **Folder**:
   - Id (PK)
   - UserId (FK)
   - Name
   - ParentId (FK, nullable)
   - CreatedUtc

3. **Conversation**:
   - Id (PK)
   - UserId (FK)
   - FolderId (FK, nullable)
   - Title
   - CreatedUtc
   - LastOpenUtc

4. **ChatHistory**:
   - Id (PK)
   - ConversationId (FK)
   - UserId (FK)
   - Message
   - Sender
   - Timestamp

5. **ChatIndex**:
   - Id (PK)
   - ConversationId (FK)
   - Role
   - FilePath
   - CreatedUtc

6. **MemoryItem**:
   - Id (PK)
   - UserId (FK)
   - Content
   - Category
   - CreatedAt
   - LastAccessed

7. **AvatarInfo**:
   - Id (PK)
   - UserId (FK)
   - Name
   - ImagePath
   - Personality
   - VoiceSettings

8. **Settings**:
   - Id (PK)
   - UserId (FK, nullable)
   - Key
   - Value
   - LastModified

   *Key settings include:*
   - OllamaApiUrl
   - LmStudioApiUrl
   - Neo4jUri
   - Neo4jBoltPort
   - Neo4jHttpPort
   - DefaultLlmEngine
   - DefaultLlmModel

### Vector and Graph Storage

1. **CoreVectors** (SQLite-VSS Virtual Table):
   - id (PK)
   - embedding (BLOB)
   - metadata (JSON)

2. **Neo4j Graph Database**:
   - Memory nodes
   - Relationship edges
   - Properties for semantic connections

## Troubleshooting

### Common Issues

1. **Application Crashes**:
   - Check crash logs in the `logs` directory
   - Look for specific exception types and stack traces
   - Verify database integrity

2. **Performance Issues**:
   - Review application logs for memory usage patterns
   - Check CPU utilization in the logs
   - Verify disk space for database and logs

3. **Authentication Problems**:
   - Verify user credentials in the database
   - Check for token expiration
   - Review auth service logs

4. **Neo4j Authentication Issues**:
   - Default Neo4j credentials are:
     - Username: `neo4j`
     - Password: `password`
   - These credentials are configured in `appsettings.json` under `AppSettings:Neo4jUser` and `AppSettings:Neo4jPassword`
   - The Neo4j auth file is created at `%AppData%\SwAIvyn\neo4j\conf\auth` during first startup

5. **SQLite-VSS Extension Issues**:
   - The SQLite-VSS extension is required for vector search functionality
   - The extension file (`sqlite-vss.dll`) should be in the application directory
   - If the extension fails to load, check that the file exists and is accessible
   - The application will try to load the extension from both the full path and the filename

6. **Neo4j Configuration Warnings**:
   - Neo4j 2025.04.0 uses different configuration settings than previous versions
   - The application now uses the newer `server.*` settings instead of the deprecated `dbms.*` settings
   - Strict validation is disabled to allow for a smoother transition

### Viewing Logs

Use the provided scripts to view logs:
- `show-logs.cmd`: Shows the most recent logs and any crash information
- PowerShell commands for detailed log analysis (provided in the script output)

## Development Guidelines

1. **Logging Best Practices**:
   - Use appropriate log levels (INFO, WARNING, ERROR, CRITICAL)
   - Include contextual information in log messages
   - Log the start and end of important operations

2. **Error Handling**:
   - Use try-catch blocks for operations that might fail
   - Log exceptions with detailed information
   - Provide user-friendly error messages

3. **Performance Considerations**:
   - Monitor memory usage for large operations
   - Use async/await for I/O-bound operations
   - Implement pagination for large data sets
