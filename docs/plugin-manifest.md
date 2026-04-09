# Plugin Manifest Specification — v1

> **Manifest version:** `1`  
> **Status:** Stable  
> **Related:** [Plugin SDK / Developer Guide](plugin-sdk.md)

---

## Overview

A plugin manifest is a JSON document that describes a SwAIvyn plugin.
Every plugin **must** ship with a `plugin.json` in its root directory.
The manifest is the single source of truth that the plugin manager uses
for installation, upgrade, health monitoring, and capability discovery.

---

## Schema

```jsonc
{
  // ── Required fields ──────────────────────────────────────────────────────

  "manifest_version": "1",          // Always "1" for this spec version
  "id": "my-plugin",                // Unique, URL-safe slug (e.g. "hello-world")
  "name": "My Plugin",              // Human-readable display name
  "version": "1.2.3",              // SemVer string

  // ── Recommended fields ───────────────────────────────────────────────────

  "description": "One-line summary of what this plugin does.",
  "author": "Your Name <you@example.com>",

  // Base URL of the running plugin service.
  // Must be reachable from the SwAIvyn backend.
  "entry_point": "https://my-plugin.example.com",

  // GET endpoint that SwAIvyn polls for liveness (must return HTTP 2xx).
  "health_endpoint": "https://my-plugin.example.com/health",

  // ── Permissions ──────────────────────────────────────────────────────────
  // Declares what the plugin is allowed to do.
  // Only listed permissions will be granted by the admin during installation.
  // Omit or leave empty [] for a sandboxed, capability-only plugin.
  "permissions": [
    "tool-use"                // May be invoked as an LLM tool
    // "read:conversations"   // (future) read the calling user's chat history
    // "write:memory"         // (future) write to the calling user's memory store
  ],

  // ── Capabilities ─────────────────────────────────────────────────────────
  // Informational list of what this plugin can do.
  // Displayed in the Plugins UI and surfaced via /api/agents/catalog.
  "capabilities": [
    "tool-use",
    "summarise"
  ],

  // ── Optional metadata ────────────────────────────────────────────────────
  "metadata": {
    "homepage": "https://github.com/example/my-plugin",
    "license": "MIT",
    "tags": ["example", "utility"]
  }
}
```

---

## Field Reference

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `manifest_version` | string | ✅ | Spec version. Must be `"1"`. |
| `id` | string | ✅ | Unique plugin identifier. URL-safe slug, max 128 chars. |
| `name` | string | ✅ | Display name shown in the UI. |
| `version` | string | ✅ | Plugin version (SemVer recommended). |
| `description` | string | | One-line summary. |
| `author` | string | | Author name / email. |
| `entry_point` | string (URL) | | Base URL of the plugin service. Must be accessible from the SwAIvyn backend. |
| `health_endpoint` | string (URL) | | Endpoint polled by the health checker (`GET` → HTTP 2xx = healthy). |
| `permissions` | string[] | | Permission tokens the plugin requires. Currently only `"tool-use"` is defined. |
| `capabilities` | string[] | | Informational capability tags (e.g. `"tool-use"`, `"summarise"`). |
| `metadata` | object | | Arbitrary extra fields for display / filtering. |

---

## Validation rules

1. `id` must match `/^[a-z0-9][a-z0-9-]{0,126}[a-z0-9]$/` (lower-kebab-case).
2. `version` should follow SemVer (`MAJOR.MINOR.PATCH`).
3. `entry_point` and `health_endpoint` must use `http` or `https` schemes and must not resolve to private / loopback addresses (SSRF protection enforced server-side).
4. `permissions` values not in the allowed list are rejected at install time.

---

## Upgrade behaviour

Installing a plugin with an existing `id` performs an **upgrade**:
the manifest, version, and entry point are updated; the plugin's `status` is reset to `installed`.
The `installed_by` and `installed_at` fields are preserved.

---

## Example — hello-world

See [`plugins/hello-world/plugin.json`](../plugins/hello-world/plugin.json).
