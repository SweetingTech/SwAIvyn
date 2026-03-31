# External Agent Guide

This guide covers how external services integrate with SwAIvyn's built-in external-agent API.

## Authentication model

There are two auth paths:

1. **User JWT** for user/admin calls (register/list/create/read).
2. **Agent header auth** for callbacks from agent services.

### User JWT

Obtain via:

```http
POST /api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "admin1234"
}
```

Use header:

```http
Authorization: Bearer <jwt>
```

### Agent callback auth

Agent callback endpoints require both headers:

```http
X-Agent-ID: <agent_id>
X-Agent-API-Key: <agent_api_key>
```

> The callback token pattern (`callback_auth_token`) is not the current implementation.

## Agent lifecycle APIs

## 1) Register agent (admin)

```http
POST /api/agents/register
Authorization: Bearer <admin-jwt>
Content-Type: application/json

{
  "agent_id": "doc-agent-1",
  "name": "Document Agent",
  "description": "Processes docs",
  "capabilities": ["extract", "summarize"],
  "version": "1.0.0",
  "health_endpoint": "https://agent.example.com/health",
  "api_key": "super-secret-key"
}
```

## 2) List available agents

```http
GET /api/agents/available
Authorization: Bearer <jwt>
```

## 3) Create task

```http
POST /api/agents/tasks
Authorization: Bearer <jwt>
Content-Type: application/json

{
  "agent_id": "doc-agent-1",
  "name": "Analyze report",
  "description": "Summarize uploaded report",
  "input_data": {"report_url": "https://..."},
  "priority": "normal"
}
```

Response:

```json
{ "task_id": "<uuid>", "success": true }
```

## 4) Agent updates task status

```http
PATCH /api/agents/tasks/{task_id}
X-Agent-ID: doc-agent-1
X-Agent-API-Key: super-secret-key
Content-Type: application/json

{
  "status": "working",
  "progress": "30%",
  "current_step": "extracting text"
}
```

## 5) Agent posts task result

```http
POST /api/agents/tasks/{task_id}/results
X-Agent-ID: doc-agent-1
X-Agent-API-Key: super-secret-key
Content-Type: application/json

{
  "result_type": "insight",
  "name": "Summary",
  "description": "Executive summary",
  "content": {"summary": "..."},
  "metadata": {"confidence": 0.93}
}
```

## 6) User reads tasks/results

```http
GET /api/agents/tasks/my
GET /api/agents/tasks/{task_id}
GET /api/agents/tasks/{task_id}/results
Authorization: Bearer <jwt>
```

## User isolation

- `agent_tasks` and `agent_results` are user-scoped.
- Non-admin users can only view their own tasks/results.
- Admin users can view all tasks/results.
