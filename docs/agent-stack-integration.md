# Agent Stack Integration (Technical Spec)

This is the current integration contract for external agents.

## Network and service endpoints

Default local development endpoints:

- Frontend: `http://localhost:5173`
- BFF API: `http://localhost:5000`
- Traefik dashboard: `http://traefik.localhost:8088`
- Temporal: `localhost:7233`
- Postgres: `localhost:5432`
- Qdrant: `localhost:6333`
- Neo4j: `localhost:7474` / `bolt://localhost:7687`
- STT: `http://localhost:9000`
- TTS: `http://localhost:8081`
- 11Labs adapter: `http://localhost:8082`

## Authentication contract

### User endpoints

Use JWT:

```http
Authorization: Bearer <jwt>
```

### Agent callback endpoints

Use agent headers:

```http
X-Agent-ID: <agent_id>
X-Agent-API-Key: <agent_api_key>
```

CORS allows `Authorization`, `X-Agent-ID`, and `X-Agent-API-Key` headers.

## API contract

## Register agent (admin)

`POST /api/agents/register`

Request model:

```json
{
  "agent_id": "string",
  "name": "string",
  "description": "string|null",
  "capabilities": ["string"],
  "version": "string|null",
  "health_endpoint": "string|null",
  "api_key": "string|null"
}
```

## Create task (user)

`POST /api/agents/tasks`

```json
{
  "agent_id": "string",
  "name": "string",
  "description": "string|null",
  "input_data": {},
  "priority": "normal"
}
```

Response:

```json
{"task_id":"<uuid>","success":true}
```

## Agent status update callback

`PATCH /api/agents/tasks/{task_id}`

```json
{
  "status": "pending|working|completed|failed|cancelled",
  "progress": "string|null",
  "current_step": "string|null",
  "output_data": {},
  "error_message": "string|null",
  "estimated_completion": "ISO datetime|null"
}
```

## Agent result callback

`POST /api/agents/tasks/{task_id}/results`

```json
{
  "result_type": "file|insight|metric|data",
  "name": "string",
  "description": "string|null",
  "content": {},
  "file_path": "string|null",
  "file_size": "string|null",
  "mime_type": "string|null",
  "metadata": {}
}
```

## Read APIs

- `GET /api/agents/available`
- `GET /api/agents/tasks/my`
- `GET /api/agents/tasks/{task_id}`
- `GET /api/agents/tasks/{task_id}/results`

## Data model alignment

- `agent_registry`: global registry of available agents (`status`, `api_key` hash, capabilities).
- `agent_tasks`: task row includes `user_id` for strict user isolation.
- `agent_results`: result row includes `user_id` and `task_id` linkage.

## Implementation notes for external agents

1. Persist your own mapping from external job IDs to SwAIvyn `task_id`.
2. Always include `X-Agent-ID` and `X-Agent-API-Key` for status/result callbacks.
3. Send incremental `PATCH` updates for better UX in task views.
4. Keep payloads JSON-serializable and compact.
