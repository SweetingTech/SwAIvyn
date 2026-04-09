# Hello World — Reference Plugin

This directory is the **reference plugin** for the SwAIvyn plugin system (Phase 5).  
It demonstrates the full plugin lifecycle: manifest declaration, installation via the
admin UI, health checking, enable/disable, and uninstall.

## What it does

The plugin exposes a single `tool-use` capability.  
When called, it returns a personalised greeting — nothing more.  
Its purpose is to verify that the plugin infrastructure works end-to-end.

## Files

| File | Purpose |
|------|---------|
| `plugin.json` | Versioned plugin manifest (see `docs/plugin-manifest.md`) |
| `main.py` | Minimal FastAPI server that implements the plugin contract |
| `Dockerfile` | Container image for local development |

## Running locally

```bash
cd plugins/hello-world
pip install fastapi uvicorn
uvicorn main:app --port 8080
```

The plugin will be reachable at `http://localhost:8080`.

## Installing into SwAIvyn

1. Open the SwAIvyn admin UI and navigate to **Plugins**.
2. Click **Install Plugin**.
3. Paste the contents of `plugin.json` into the manifest editor.
4. Click **Install**.

Alternatively, use the REST API directly:

```bash
curl -X POST http://localhost:8000/api/plugins/install \
  -H "Authorization: Bearer <admin-jwt>" \
  -H "Content-Type: application/json" \
  -d @plugin.json
```

## Plugin contract

The plugin must expose at least:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Returns `{"status":"ok"}` with HTTP 200 when healthy |
| `/invoke` | POST | Accepts `{"input": {...}}` and returns `{"output": {...}}` |

See `docs/plugin-sdk.md` for the full HTTP contract.
