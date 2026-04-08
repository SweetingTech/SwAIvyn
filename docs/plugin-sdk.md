# Plugin SDK — Developer Guide

> **Manifest spec:** [plugin-manifest.md](plugin-manifest.md)  
> **Reference plugin:** [`plugins/hello-world/`](../plugins/hello-world/)

---

## Overview

SwAIvyn's plugin system lets you extend the assistant with new capabilities
without forking the core codebase.  A plugin is an HTTP microservice that:

1. Ships a `plugin.json` manifest.
2. Implements the endpoints described in this guide.
3. Is installed by an admin via the Plugins UI or the REST API.

Plugins are **sandboxed**: they communicate with SwAIvyn only through
declared permissions and cannot access other users' data.

---

## Quick-start

```bash
# Clone the hello-world reference plugin
cp -r plugins/hello-world my-plugin
cd my-plugin

# Edit the manifest
$EDITOR plugin.json   # change id, name, description, ...

# Run locally
pip install fastapi uvicorn
uvicorn main:app --port 8080

# Install into SwAIvyn (admin credentials required)
curl -X POST http://localhost:8000/api/plugins/install \
  -H "Authorization: Bearer <admin-jwt>" \
  -H "Content-Type: application/json" \
  -d @plugin.json
```

---

## HTTP contract

Your plugin service must expose the following endpoints.

### `GET /health`

Called periodically by SwAIvyn to determine liveness.

**Response** (HTTP 200):
```json
{ "status": "ok" }
```

Any non-2xx response marks the plugin as `unhealthy` in the UI.

---

### `POST /invoke`

Called when SwAIvyn invokes your plugin as a tool.

**Request body:**
```json
{
  "input": { "key": "value" },
  "context": {
    "user_id": "...",
    "conversation_id": "..."
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `input` | object | Tool-use input data provided by the LLM or user. |
| `context` | object | Optional SwAIvyn context (user/conversation IDs). |

**Response body** (HTTP 200):
```json
{
  "output": { "result": "..." }
}
```

Return a non-2xx status code (with `{"detail": "..."}`) to signal an error.

---

### `GET /info` *(optional)*

Returns plugin metadata matching the manifest.  
Used by SwAIvyn for self-reported version / capability checks.

```json
{
  "manifest_version": "1",
  "id": "my-plugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "capabilities": ["tool-use"]
}
```

---

## Plugin lifecycle

```
Install  →  installed
Admin enables  →  enabled      (plugin appears in /api/agents/catalog)
Admin disables →  disabled
Admin uninstalls → (deleted)
```

The Plugins admin UI (`/plugins`) supports all lifecycle transitions.

---

## Admin REST API

All write endpoints require an admin JWT (`Authorization: Bearer <token>`).

### List plugins
```
GET /api/plugins
```

### Install / upgrade
```
POST /api/plugins/install
Content-Type: application/json

{ ...manifest... }
```

### Get plugin details
```
GET /api/plugins/{plugin_id}
```

### Enable / disable
```
PATCH /api/plugins/{plugin_id}/status
Content-Type: application/json

{ "status": "enabled" }   // or "disabled"
```

### Check health (server-side probe)
```
GET /api/plugins/{plugin_id}/health
```

### Uninstall
```
DELETE /api/plugins/{plugin_id}
```

### Plugin catalog (authenticated, any user)
```
GET /api/agents/catalog
```
Returns all `installed` and `enabled` plugins — this is the endpoint
the frontend and LLM tool-use layer consume.

---

## Permissions reference

| Token | Meaning |
|-------|---------|
| `tool-use` | Plugin may be invoked as an LLM tool via `/invoke`. |

Additional permissions (e.g. `read:conversations`, `write:memory`) will be
added in future releases.  Plugins should only request the minimum set they
actually need.

---

## Security notes

* `entry_point` and `health_endpoint` URLs are validated server-side to
  prevent SSRF.  Private / loopback addresses are blocked unless
  `ALLOW_PRIVATE_PLUGIN_URLS=true` is set (development only).
* Plugins receive only the data passed in the `invoke` request body.
  They cannot query the SwAIvyn database directly.
* Plugin API keys (if added in future) will be stored hashed (bcrypt).

---

## FAQ

**Can I write a plugin in any language?**  
Yes.  Your plugin just needs to be an HTTP server that implements the
endpoints above.  The reference implementation uses Python + FastAPI,
but Go, Node.js, Rust, etc. all work fine.

**How do I distribute my plugin?**  
Publish your `plugin.json` anywhere your users can reach it (GitHub,
Docker Hub description, personal site).  Admins paste the manifest into
the Install dialog.

**What happens if my plugin crashes?**  
The health check will mark it `unhealthy`.  SwAIvyn will not invoke an
unhealthy plugin for tool-use.  Fix the crash and the next health probe
will restore the `healthy` status.
