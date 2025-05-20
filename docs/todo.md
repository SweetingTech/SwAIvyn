# SwAIvyn Development To‑Do List (Updated Roadmap)

> **Legend**  ✅ Done    🟡 In Progress    ⬜ To Do    ⚡ New / Revised Task

---

## 🚀 Phase 1: Core Infrastructure (Foundation) ✅

### Backend Setup

* ✅ Create .NET 9 project structure
* ✅ Setup ASP.NET Core hosting
* ✅ Configure SQLite with EF Core (WAL mode)
* ✅ Implement basic user authentication

  * ✅ Username/password login
  * ✅ PIN code system
  * ✅ Recovery phrase generation & storage
* ✅ Create database models

  * ✅ User profile
  * ✅ AI character profile
  * ✅ Folders (hierarchical)
  * ✅ Conversations (folder relationships)
  * ✅ Chat index (file‑based)
  * ✅ Memory items
  * ✅ Settings
* ✅ **Startup health guard** — abort boot if SQLite or Neo4j unavailable
* ✅ **Seed default AI profile** on first run

### Frontend Foundations

* ✅ React project (Remix)
* ✅ TailwindCSS configured
* ✅ Routing scaffold
* ✅ Basic layout components
* ✅ Login/setup flow & auth screens

### Core Services

* ✅ Local LLM connector (Ollama)
  * ✅ Ollama API integration
  * ✅ LM Studio API integration
  * ✅ User-configurable LLM settings
* ✅ Prompt manager & context window
* ✅ File storage service (JSON chat logs)
* ✅ Folder & conversation management
* ✅ Brain service

  * ✅ Vector storage (SQLite‑VSS)
  * ✅ Graph DB (Neo4j)
  * ✅ Semantic search merge
  * ✅ **Remote vector fallback hook**
* ✅ Settings service (user prefs / system config)
  * ✅ Database-backed settings storage
  * ✅ User-specific and global settings
  * ✅ LLM engine and model preferences
* ✅ **HealthCheck API endpoints**

  * `/api/health/sqlite`
  * `/api/health/neo4j`
  * `/api/health/vector`

---

## 🌐 Phase 2: Chat Interface & Basic Features ✅

### Text Chat Implementation

* ✅ Chat UI (bubbles, input, uploads, history)
* ✅ Real‑time SignalR
* ✅ Markdown rendering
* ✅ Conversation CRUD
* ✅ **Folder management UI**

  * ✅ Folder tree view
  * ✅ Add / rename / delete
  * ✅ Drag‑and‑drop conversations
* ✅ **Conversation search**

  * ✅ Title & content search
  * ✅ Folder filter
  * ✅ Date sort

### Character System

* ✅ Character schema & card import/export
* ✅ Character editor UI
* ✅ 2D avatar management

### Brain System

* ✅ Vector store + memory retrieval
* ✅ Graph relations in Neo4j
* ✅ **Brain explorer UI**

  * ✅ Memory browser (search / edit / delete)
  * ✅ Graph visualization (Bloom/D3)
  * ✅ Relationship editor
* ✅ Manual memory & edge CRUD from UI

---

## 🎤 Phase 3: Voice Integration & Room Interface ⬜

### Voice System

* ⬜ STT (Whisper) integration

  * ⬜ Browser audio capture
  * ⬜ Streaming to backend
  * ⬜ Transcript injection into chat
* ⬜ TTS engine

  * ⬜ Voice picker
  * ⬜ Audio stream to frontend
  * ⬜ Playback controls
* ⬜ Wake‑word detection

  * ⬜ Background monitoring
  * ⬜ Config UI
  * ⬜ Notification event

### AI Room Interface

* ⬜ 2D room canvas (avatar, background)
* ⬜ Voice‑first controls (mic button, status, replay)
* ⬜ Minimizable text chat (slide/auto‑hide)

---

## 🔄 Phase 4: Federation & Advanced Features ⬜

### Federation System

* ⬜ Local peer discovery
* ⬜ P2P protocol (encrypted, authenticated)
* ⬜ AI‑to‑AI messaging & context share
* ⬜ User‑to‑user messaging (forwarding + history)

### Email & Calendar

* ⬜ IMAP client + local mirror
* ⬜ iCal/CalDAV integration & availability checks

### Web Access

* ⬜ Browsh integration for text browsing
* ⬜ History manager

---

## 🧩 Phase 5: Plugin System & Customization ⬜

*(unchanged – begins after Phase 4)*

---

## 💾 Phase 6: Reliability & Polish 🟡

### Backup System

* ✅ Automated local DB backups
* ⚡ ⬜ Include `/sessions/**` JSON logs in backup
* ⬜ NAS / cloud encrypted backup + restore

### Windows / Linux Service

* ⬜ Windows service wrapper & tray icon
* ⬜ Linux systemd unit

### Packaging & Distribution

* ⬜ Single‑file publish (win‑x64, macOS, linux)
* ⬜ Installer & upgrade migration

### Performance

* ✅ SQLite tuned, WAL mode
* ✅ Efficient JSON message writes
* ⬜ Memory profiling & startup optimisation
* ⬜ Vector search tuning & cache

---

## 🧪 Phase 7: Testing & Documentation ⬜

### Testing

* ⬜ Unit tests for Folder/Conversation/Brain services
* ⬜ Integration: startup health + message flow
* ⬜ End‑to‑end UI tests
* ⬜ Security audit

### Documentation

* ✅ Architecture, schema, data‑flow docs
* ⬜ User manual
* ⬜ Developer guide + API docs
* ⬜ Setup / configuration guide

### Legal & Compliance

* ⬜ MIT license, privacy policy, GDPR handling

---

## 🌟 Future Enhancements (Post‑MVP) ⬜

*(mobile app, 3D avatar system, multi‑user support, advanced AI reasoning, etc.)*

---

### Immediate Next Focus

1. **Health checks + startup guard** ✅
2. **Folder management & search UIs** ✅
3. **Brain explorer view** ✅
4. **User-configurable LLM settings** ✅
5. **LLM settings UI** ✅
6. **Chat session management** ✅
   - Auto-save with UUID assignment
   - Folder organization
   - Rename, edit, delete sessions
7. **Whisper audio capture POC** ⬜
