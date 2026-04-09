# SwAIvyn Project Roadmap

## Phase 1: Core Infrastructure (Completed)
- Backend setup with .NET 8, ASP.NET Core hosting, and SQLite database using EF Core.
- Basic user authentication implemented with username/password, PIN code, and recovery phrases.
- Database models created for user profiles, AI character profiles, memory items, chat history, and settings.
- Frontend foundations established with React 18, Vite, TailwindCSS, routing, and basic layout components.
- Authentication screens and login/setup flow implemented.
- Core services developed including live connections to Ollama and LM Studio LLMs, simple prompt manager, context window handling, file storage service, and settings service.
- Comprehensive documentation created and updated.

## Phase 2: Chat Interface & Basic Features (In Progress)
- [OK] Create chat UI components: message bubbles, input area, file upload zone, conversation history display.
- [OK] Implement WebSocket/SignalR for real-time chat.
- [OK] Add markdown rendering for messages.
- [OK] Create conversation session management: create, switch, and delete conversations.
- [OK] Implement user-configurable LLM settings with database storage.
- [OK] Create settings UI for configuring LLM connections (Ollama, LM Studio).
- [OK] Integrate chat functionality with user-selected LLM settings.
- [OK] Implement folder management for organizing conversations:
  - [OK] Create, rename, and delete folders
  - [OK] Hierarchical folder structure
  - [OK] Automatic deletion of contained conversations when folder is deleted
- [OK] Implement automatic chat session management:
  - [OK] Start with empty chat session
  - [OK] Assign UUID on first message
  - [OK] Auto-save sessions
  - [OK] Generate title from first message
  - [OK] Rename, edit, and delete sessions
- Design database schema for character profiles.
- Create character card import/export functionality.
- Build character editor UI.
- Implement 2D avatar management with upload, cropping, and selection interface.

## Phase 3: Voice Integration & Room Interface
- [OK] Integrate STT engine (Whisper) with audio capture, stream processing, and transcript generation.
- [OK] Implement TTS engine with voice selection, audio streaming, and playback controls.
- [ ] Create wake word detection service with background audio monitoring and event notification.
- [OK] Design 2D room environment with avatar display, room background, and interactive elements.
- [OK] Build voice-first interaction controls including microphone button and voice status indicators.
- [OK] Create minimizable text chat component with slide animations and auto-hide.
- [OK] Replace 2D orb avatar with a real-time 3D scene (Three.js / React Three Fiber) in the Voice Room.
- [OK] Add wake word detection service with background SpeechRecognition monitoring.
- [OK] Add 3D model (VRM / glTF) upload support in the Character Editor.

## Phase 4: Federation & Advanced Features
- [OK] Implement peer discovery on local network.
- [OK] Create P2P communication protocol with encryption, authentication, and connection management.
- [OK] Build AI-to-AI communication system with message types, response handling, and context sharing.
- [OK] Add user-to-user messaging with forwarding and history.
- [OK] Implement IMAP client for email access and mirroring.
- [OK] Add calendar integration with iCal/CalDAV support.
- [OK] Integrate text-based web browsing with history management.

## Phase 5: Plugin System & Customization
- Design plugin interface and manifest format.
- Build plugin manager service for discovery, installation, updates, and removal.
- Create plugin management UI.
- [x] Develop 3D avatar space UI and Tamagotchi-like stat system.
- [x] Implement room customization and voice profile training systems.

## Phase 6: Reliability & Polish
- Implement automated local and cloud backups with scheduling and encryption.
- Create Windows and Linux system service wrappers with auto-start and management.
- Setup single-file executable publishing and installer/setup process.
- Optimize performance including memory usage, startup time, and database operations.

## Phase 7: Testing & Documentation
- Create unit, integration, and end-to-end tests.
- Conduct security audits.
- Write user manuals, developer documentation, API specs, and setup guides.
- Ensure legal compliance including MIT license and GDPR.

---

This roadmap outlines the planned development stages and key milestones for the SwAIvyn project.
