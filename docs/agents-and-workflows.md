# Agents and Workflows (Overview)

This document explains how SwAIvyn (UI + BFF) integrates with SwAIvyn_Workers (orchestrator + MCP servers) and the direction for agents/workflows management in the UI.

## Current State

- Default Chat Workflow
  - Stored in BFF DB (`workflows` table) and executed by a small adapter in `POST /api/conversation/chat`.
  - Mirrors existing behavior: select LLM per user, build connection payload, call the engine-specific Temporal workflow, return reply (+ optional TTS URL).
- Agents Catalog
  - Workers orchestrator exposes `GET http://localhost:8000/api/agents` (YAML agents on disk).
  - BFF proxies this at `GET /api/agents/catalog` for use in the UI.
- Runtime Agent Activity
  - Workers notify BFF on start/stop of pipelines (`/api/agents/{id}/start|stop`), so the dashboard can display counts of Running/Completed/Failed.

## Near-Term UI Plan

- Agents & Workflows page with two tabs:
  - Catalog: union of global YAML agents (from Workers) and user-owned entries (future).
  - My Library: user-uploaded YAML agents/workflows with Create/Update/Delete.
- Per-User Toggles
  - Table `user_agent_prefs(user_id, agent_id, enabled)` controls enablement.
  - BFF enforces preference when exposing agent choices to chat or launching pipelines.
- Run Agent
  - BFF proxy `POST /api/agents/{agentId}/run` forwards to Workers `/api/run-pipeline` (validating user permissions and enablement).

## TTS and STT Integration

- TTS
  - UI reads and updates TTS settings via `/api/tts/settings`.
  - Synthesis uses `/api/tts/synthesize`. In dev, the backend may return a short silent WAV; production can route to Fish Speech or the ElevenLabs adapter.
- STT
  - STT is used by the Voice Room and can be wired per user in settings; deployment provides a Whisper container on `http://localhost:9000`.

## Workers YAML Agents

- Located in `SwAIvyn_Workers/agents/*.yaml`.
- CRUD plan (Workers):
  - `POST /api/agents` (create), `PUT /api/agents/{id}` (update), `DELETE /api/agents/{id}` (delete)
  - Used by BFF to materialize user-provided YAML into Workers container so catalog stays the source of truth.

## Seeding and Setup

- Accounts: `SwAIvyn/scripts/dev-seed-accounts.ps1 -Yes`
- Characters (Sam, Sherlock): `SwAIvyn/scripts/dev-seed-characters.ps1 -Yes`
- Default Chat Workflow: `SwAIvyn/scripts/dev-seed-workflows.ps1 -Yes`

## Open Questions

- Should user-owned agents be private by default or visible to admins for promotion?
- Chat auto-invocations: which agents should be auto-run (e.g., search) as part of the default chat workflow vs. kept as explicit actions?
- YAML quotas and validation limits for uploads.

