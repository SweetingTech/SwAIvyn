# SwAIvyn Roadmap — Open Issues Reference

This document is the authoritative reference for the five open GitHub Issues derived from the
uncompleted items in the [README.md roadmap](../README.md#roadmap).

Issues are created (or re-created) by the
[Create Roadmap Issues](./../.github/workflows/create-roadmap-issues.yml) workflow.
Run it from **Actions → Create Roadmap Issues → Run workflow**.

---

## How to run the workflow

1. Go to **Actions** → **Create Roadmap Issues**.
2. Click **Run workflow**.
3. Leave *Dry run* as `false` to create the issues, or set it to `true` to preview without creating.
4. Click **Run workflow** to confirm.

> **Note:** Re-running this workflow will create duplicate issues if the original issues are still
> open. Close or delete any existing roadmap issues before re-running if you want a clean slate.

---

## Issue 1 — Cross-instance AI federation

**Labels:** `roadmap`, `enhancement`, `federation`

**Overview:**
Enable SwAIvyn instances to discover each other, exchange tasks, and communicate — both AI-to-AI
and user-to-user — across a LAN or the internet.

**Acceptance criteria:**

- [ ] Peer discovery — Instances on the same local network auto-discover each other without manual configuration.
- [ ] P2P communication protocol — Encrypted, authenticated, connection-managed channel between instances.
- [ ] AI-to-AI communication — Structured message types, response handling, and context sharing between AI workers on different instances.
- [ ] User-to-user messaging — Users can send messages across instances with forwarding and conversation history.
- [ ] IMAP client integration — Access and mirror email accounts within the platform.
- [ ] Calendar integration — Read/write iCal/CalDAV calendars.
- [ ] Web browsing integration — Browsh-backed text-based browsing with history management.

**References:** `docs/roadmap.md` Phase 4, `docs/external-agent-guide.md`, `docs/agent-stack-integration.md`

---

## Issue 2 — Memory management UI enhancements

**Labels:** `roadmap`, `enhancement`, `memory`

**Overview:**
Improve the user-facing memory management experience. Each user already has a dedicated memory
space across Qdrant (vector), Neo4j (graph), and PostgreSQL (structured history). This issue
tracks the UI work needed to surface, explore, and control that memory.

**Acceptance criteria:**

- [ ] Memory browser — Users can list, search, and paginate their stored memory items.
- [ ] Memory editor — Users can view the full content of a memory item and edit or annotate it.
- [ ] Selective memory sharing — Users can mark specific memories as shareable with other users or agents.
- [ ] Memory deletion — Users can delete individual items or bulk-clear a memory store.
- [ ] Admin visibility panel — Admins can view memory stats per user and force-delete items if required.
- [ ] Memory search — Full-text and semantic search across a user's stored memories from the UI.
- [ ] Memory import / export — Export to JSON; import from a compatible JSON backup.

**References:** `README.md` "Per-user isolated memory", `docs/database-implementation.md`, `docs/neo4j-configuration.md`

---

## Issue 3 — Plugin system expansion

**Labels:** `roadmap`, `enhancement`, `plugin-system`

**Overview:**
Build a first-class plugin system so third-party developers (and power users) can extend SwAIvyn
with new capabilities without forking the codebase.

**Acceptance criteria:**

- [ ] Plugin interface spec — Versioned plugin manifest format (JSON/YAML) describing entry points, permissions, and metadata.
- [ ] Plugin manager service — Backend service for plugin discovery, installation, upgrade, and removal with sandboxing.
- [ ] Plugin management UI — Admin panel page to browse, install/uninstall, and view plugin health/logs.
- [ ] Plugin SDK / developer guide — Minimal SDK or documented HTTP contract for external developers.
- [ ] Plugin marketplace stub — `/api/agents/catalog` wired to the plugin registry.
- [ ] At least one reference plugin — A bundled example plugin that exercises the full plugin lifecycle.

**References:** `docs/roadmap.md` Phase 5, `docs/external-agent-guide.md`, `Services/bff/app/main.py` catalog stub

---

## Issue 4 — 3D avatar support

**Labels:** `roadmap`, `enhancement`, `avatar-3d`

**Overview:**
Upgrade the AI Room from a 2D flat avatar to a real-time 3D character with an interactive
environment — including a Tamagotchi-style stat/mood system, decorable room space, and voice
profile training.

**Acceptance criteria:**

- [ ] 3D avatar renderer — Replace or augment the 2D avatar with a 3D model (VRM or glTF) with lip-sync and idle animations.
- [ ] 3D avatar space UI — Full 3D room environment rendered in the browser (Three.js or Babylon.js).
- [ ] Tamagotchi-like stat system — Track and display AI persona stats that evolve based on interaction history.
- [ ] Room customization — Users can place and arrange virtual items with persistence in user settings.
- [ ] Voice profile training UI — Record reference audio and fine-tune the TTS voice for a character.
- [ ] Wake word detection — Background audio monitoring to trigger voice interaction without a button press.
- [ ] Character avatar upload — Accept 3D model uploads (VRM/glTF) alongside existing 2D image uploads.

**References:** `README.md` "Personalization Layer", `docs/roadmap.md` Phase 3 & 5, `docs/voice-management.md`, `docs/TTS.md`

---

## Issue 5 — Mobile companion app

**Labels:** `roadmap`, `enhancement`, `mobile`

**Overview:**
Build a native (or PWA) mobile companion application that gives users access to their SwAIvyn
instance from a phone or tablet, with text chat, voice interaction, and push notifications.

**Acceptance criteria:**

- [ ] Technology decision — Evaluate and document choice between React Native, Flutter, Expo, or PWA.
- [ ] Authentication — JWT-based auth with tokens stored securely on device (Keychain / Keystore).
- [ ] Text chat — Full conversation history, markdown rendering, send/receive messages, and conversation switching.
- [ ] Voice interaction — Microphone → STT → LLM → TTS pipeline mirroring the AI Room flow.
- [ ] Push notifications — Notify user when an agent task completes or a workflow fires.
- [ ] Settings sync — LLM engine/model selection and character preferences sync between desktop and mobile.
- [ ] Offline graceful degradation — Display last-known conversation history when the instance is unreachable.
- [ ] App distribution — CI pipeline builds the app and publishes artifacts to the Releases page.

**References:** `README.md` roadmap, `docs/architecture-and-dataflow.md`, existing BFF API endpoints

---

*This file is auto-generated / maintained alongside
`.github/workflows/create-roadmap-issues.yml`. Update both files together when the roadmap
changes.*
