# External Agent Connection Guide

This guide explains how to develop and connect external AI agents to SwAIvyn. The external agent system enables you to build specialized AI services that can be invoked by SwAIvyn users while maintaining complete data isolation and security.

##  Overview

SwAIvyn's external agent system provides:

- **Multi-tenant Architecture**: Complete user data isolation
- **Secure Authentication**: JWT-based API authentication
- **Task Management**: Full lifecycle task tracking and management
- **Status Polling**: Efficient task status monitoring with polling endpoints
- **Flexible Integration**: Support for any type of external service

##  Architecture

```
-----------------    -----------------    -----------------
   SwAIvyn UI          SwAIvyn BFF         External Agent  
                                              Service      
+-----------------    +-----------------    +-----------------
 -  Agent Mgmt    -- -  Authentication-- -  Task Processor
 -  Task Monitor       -  Agent Registry     -  Result Handler
 -  Results View       -  Task Routing       -  Health Check  
+-----------------    +-----------------    +-----------------
```

##  Authentication

All external agent communications require JWT authentication. Each user's API token is scoped to their data only.

### Obtaining an API Token

1. **Via Frontend**: Login to SwAIvyn -> Settings -> API Keys -> Generate New Key
2. **Via API**: `POST /api/auth/login` with valid credentials

**Login Request:**
```http
POST /api/auth/login
Content-Type: application/json

{
    "username": "your_username",
    "password": "your_password"
}
```

**Response:**
```json
{
    "access_token": "eyJ0eXAiOiJKV1QiLCJhbGc...",
    "token_type": "bearer",
    "user": {
        "id": "user_123",
        "username": "your_username"
    }
}
```

### Using the Token

Include the JWT token in all API requests:

```bash
Authorization: Bearer <your-jwt-token>
```

##  Database Schema

SwAIvyn maintains three core tables for external agent management:

### Agent Registry (`agent_registry`)
Tracks available external agent services:

```sql
CREATE TABLE agent_registry (
    id VARCHAR(128) PRIMARY KEY,
    user_id VARCHAR(64) NOT NULL,  -- User isolation
    name VARCHAR(300) NOT NULL,
    description TEXT,
    endpoint_url VARCHAR(500) NOT NULL,
    agent_type VARCHAR(100),
    capabilities TEXT,  -- JSON array
    is_active BOOLEAN DEFAULT true,
    created_at VARCHAR(40) NOT NULL,
    updated_at VARCHAR(40) NOT NULL
);
```

### Agent Tasks (`agent_tasks`)
Manages task execution:

```sql
CREATE TABLE agent_tasks (
    id VARCHAR(128) PRIMARY KEY,
    user_id VARCHAR(64) NOT NULL,  -- User isolation
    registry_id VARCHAR(128) NOT NULL,
    task_type VARCHAR(100),
    status VARCHAR(32) DEFAULT 'pending',
    input_data TEXT,  -- JSON
    priority VARCHAR(20) DEFAULT 'normal',
    created_at VARCHAR(40) NOT NULL,
    updated_at VARCHAR(40) NOT NULL
);
```

### Agent Results (`agent_results`)
Stores task results:

```sql
CREATE TABLE agent_results (
    id VARCHAR(128) PRIMARY KEY,
    user_id VARCHAR(64) NOT NULL,  -- User isolation
    task_id VARCHAR(128) NOT NULL,
    status VARCHAR(32),
    result_data TEXT,  -- JSON
    error_message TEXT,
    processing_time_ms INTEGER,
    created_at VARCHAR(40) NOT NULL
);
```

##  SwAIvyn API Endpoints

### Agent Registry Management

#### Register New Agent
```http
POST /api/agents/register
Authorization: Bearer <jwt-token>
Content-Type: application/json

{
    "name": "Document Processor",
    "description": "Specialized document analysis and processing",
    "endpoint_url": "https://my-agent.example.com",
    "agent_type": "document_processor",
    "capabilities": ["pdf_analysis", "text_extraction", "summarization"]
}
```

**Response:**
```json
{
    "id": "agent_123456789",
    "user_id": "user_abc123",
    "name": "Document Processor",
    "endpoint_url": "https://my-agent.example.com",
    "is_active": true,
    "created_at": "2025-09-16T00:00:00Z"
}
```

#### List Available Agents
```http
GET /api/agents/available
Authorization: Bearer <jwt-token>
```

#### Get Agent Catalog
```http
GET /api/agents/catalog
Authorization: Bearer <jwt-token>
```

### Task Management

#### Create Task
```http
POST /api/agents/tasks
Authorization: Bearer <jwt-token>
Content-Type: application/json

{
    "registry_id": "agent_123456789",
    "task_type": "process_document",
    "input_data": {
        "document_url": "https://example.com/doc.pdf",
        "analysis_type": "summarization",
        "options": {
            "max_length": 500,
            "include_key_points": true
        }
    },
    "priority": "high"
}
```

**Response:**
```json
{
    "id": "task_987654321",
    "user_id": "user_abc123",
    "registry_id": "agent_123456789",
    "status": "pending",
    "created_at": "2025-09-16T00:00:00Z"
}
```

#### List User's Tasks
```http
GET /api/agents/tasks/my
Authorization: Bearer <jwt-token>
```

#### Get Task Details
```http
GET /api/agents/tasks/{task_id}
Authorization: Bearer <jwt-token>
```

### Results Management

#### Get Task Result
```http
GET /api/agents/tasks/{task_id}/results
Authorization: Bearer <jwt-token>
```

#### Submit Task Result (Called by External Agent)
```http
POST /api/agents/tasks/{task_id}/results
Authorization: Bearer <callback-auth-token>
Content-Type: application/json

{
    "status": "completed",
    "result_data": {
        "summary": "Document analysis complete",
        "key_points": ["Point 1", "Point 2", "Point 3"],
        "word_count": 1250,
        "confidence_score": 0.95
    },
    "processing_time_ms": 5430
}
```

**CRITICAL**: When your external agent posts results back to SwAIvyn, you MUST use the `callback_auth_token` provided in the task request as an `Authorization: Bearer <token>` header. Do NOT include the token in the request body.

#### List User's Tasks (Including Results)
```http
GET /api/agents/tasks/my
Authorization: Bearer <jwt-token>
```

Note: To get all results, query your tasks and then fetch individual task results via `/api/agents/tasks/{task_id}/results`.

##  Building External Agent Services

### Required Endpoints

Your external agent service must implement these endpoints:

#### Health Check
```http
GET /health

Response:
{
    "status": "healthy",
    "version": "1.0.0",
    "capabilities": ["document_processing", "text_analysis"]
}
```

#### Process Task
```http
POST /tasks
Content-Type: application/json
Authorization: Bearer <swaivy-api-token>

{
    "task_id": "task_987654321",
    "user_id": "user_abc123",
    "task_type": "process_document",
    "input_data": {
        "document_url": "https://example.com/doc.pdf",
        "options": {...}
    },
    "callback_url": "https://swaivy.example.com/api/agents/tasks/task_987654321/results",
    "auth_token": "<callback-auth-token>"
}

Response:
{
    "accepted": true,
    "estimated_completion": "2025-09-16T00:05:00Z"
}
```

#### Get Task Status
```http
GET /tasks/{task_id}
Authorization: Bearer <swaivy-api-token>

Response:
{
    "task_id": "task_987654321",
    "status": "in_progress",
    "progress": 65,
    "estimated_completion": "2025-09-16T00:03:00Z"
}
```

### Task Lifecycle

1. **Registration**: Agent registers with SwAIvyn via `/api/agents/register`
2. **Task Creation**: User creates task via SwAIvyn UI or API
3. **Task Dispatch**: SwAIvyn calls agent's `/tasks` endpoint
4. **Processing**: Agent processes task asynchronously
5. **Result Callback**: Agent posts results back to SwAIvyn
6. **Completion**: Task marked complete, user can view results

### Example FastAPI Implementation

```python
from fastapi import FastAPI, HTTPException, Depends, BackgroundTasks
from pydantic import BaseModel
import httpx
import asyncio
from typing import Dict, Any, Optional

app = FastAPI(title="SwAIvyn External Agent")

# Data models
class TaskRequest(BaseModel):
    task_id: str
    user_id: str
    task_type: str
    input_data: Dict[str, Any]
    callback_url: str
    auth_token: str

class TaskResponse(BaseModel):
    accepted: bool
    estimated_completion: Optional[str] = None
    error: Optional[str] = None

class TaskStatus(BaseModel):
    task_id: str
    status: str  # pending, in_progress, completed, failed
    progress: Optional[int] = None
    estimated_completion: Optional[str] = None

class ResultData(BaseModel):
    task_id: str
    status: str
    result_data: Optional[Dict[str, Any]] = None
    error_message: Optional[str] = None
    processing_time_ms: int

# In-memory task storage (use Redis/DB for production)
tasks: Dict[str, Dict[str, Any]] = {}

@app.get("/health")
async def health_check():
    return {
        "status": "healthy",
        "version": "1.0.0",
        "capabilities": ["document_processing", "text_analysis"]
    }

@app.post("/tasks", response_model=TaskResponse)
async def create_task(
    task_request: TaskRequest,
    background_tasks: BackgroundTasks
):
    """Accept a new task from SwAIvyn."""
    
    # Validate task type
    if task_request.task_type not in ["process_document", "analyze_text"]:
        raise HTTPException(400, "Unsupported task type")
    
    # Store task
    tasks[task_request.task_id] = {
        "status": "pending",
        "user_id": task_request.user_id,
        "task_type": task_request.task_type,
        "input_data": task_request.input_data,
        "callback_url": task_request.callback_url,
        "auth_token": task_request.auth_token,
        "progress": 0
    }
    
    # Start processing in background
    background_tasks.add_task(process_task, task_request.task_id)
    
    return TaskResponse(
        accepted=True,
        estimated_completion="2025-09-16T00:05:00Z"
    )

@app.get("/tasks/{task_id}", response_model=TaskStatus)
async def get_task_status(task_id: str):
    """Get current status of a task."""
    
    if task_id not in tasks:
        raise HTTPException(404, "Task not found")
    
    task = tasks[task_id]
    return TaskStatus(
        task_id=task_id,
        status=task["status"],
        progress=task.get("progress", 0),
        estimated_completion=task.get("estimated_completion")
    )

async def process_task(task_id: str):
    """Background task processor."""
    
    task = tasks[task_id]
    task["status"] = "in_progress"
    
    try:
        # Simulate processing
        for i in range(0, 101, 10):
            task["progress"] = i
            await asyncio.sleep(0.5)  # Simulate work
        
        # Generate result
        result_data = {
            "summary": f"Processed {task['task_type']} successfully",
            "output": "Task completed with mock data",
            "metadata": {
                "processing_method": "example_processor",
                "timestamp": "2025-09-16T00:00:00Z"
            }
        }
        
        # Post result back to SwAIvyn
        await post_result_to_swaivy(task, result_data)
        
        task["status"] = "completed"
        
    except Exception as e:
        task["status"] = "failed"
        task["error"] = str(e)
        
        # Post error back to SwAIvyn
        await post_result_to_swaivy(task, None, str(e))

async def post_result_to_swaivy(
    task: Dict[str, Any], 
    result_data: Optional[Dict[str, Any]], 
    error_message: Optional[str] = None
):
    """Post task result back to SwAIvyn."""
    
    result_payload = ResultData(
        task_id=task["task_id"],
        status="completed" if result_data else "failed",
        result_data=result_data,
        error_message=error_message,
        processing_time_ms=5000  # Mock processing time
    )
    
    headers = {"Authorization": f"Bearer {task['auth_token']}"}
    
    async with httpx.AsyncClient() as client:
        try:
            response = await client.post(
                task["callback_url"],
                json=result_payload.dict(),
                headers=headers
            )
            response.raise_for_status()
        except Exception as e:
            print(f"Failed to post result: {e}")

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
```

### Authentication & Security

#### API Token Validation
```python
from fastapi import HTTPException, Security
from fastapi.security import HTTPBearer
import jwt

security = HTTPBearer()

async def validate_swaivy_token(token: str = Security(security)):
    """Validate JWT token from SwAIvyn."""
    try:
        # Decode and validate JWT (replace with your secret)
        payload = jwt.decode(token.credentials, "your-secret", algorithms=["HS256"])
        return payload
    except jwt.InvalidTokenError:
        raise HTTPException(401, "Invalid authentication token")
```

#### User Data Isolation
Always respect the `user_id` field and ensure:
- Tasks are only processed for the authenticated user
- Results are returned only to the task owner
- No cross-user data leakage occurs

##  Monitoring & Logging

### Health Monitoring
SwAIvyn will periodically check your agent's `/health` endpoint. Ensure it returns quickly and accurately reflects your service status.

### Logging Best Practices
```python
import logging

# Configure structured logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)

logger = logging.getLogger("swaivy-agent")

async def process_task(task_id: str):
    logger.info(f"Starting task processing: {task_id}")
    
    try:
        # Process task
        logger.info(f"Task {task_id} completed successfully")
    except Exception as e:
        logger.error(f"Task {task_id} failed: {str(e)}")
```

### Metrics & Performance
Track key metrics:
- Task processing times
- Success/failure rates
- Resource utilization
- Queue depths

##  Error Handling

### Common Error Scenarios

#### Authentication Errors
```json
{
    "error": "authentication_failed",
    "message": "Invalid or expired JWT token",
    "code": 401
}
```

#### Task Validation Errors
```json
{
    "error": "invalid_task",
    "message": "Unsupported task type: unknown_type",
    "code": 400
}
```

#### Processing Errors
```json
{
    "error": "processing_failed",
    "message": "Unable to access input document",
    "code": 500,
    "details": {
        "stage": "document_download",
        "retry_after": 300
    }
}
```

### Error Recovery
- Implement exponential backoff for transient failures
- Provide clear error messages for user feedback
- Support task retry mechanisms where appropriate

##  Testing Your Agent

### Unit Testing
```python
import pytest
from fastapi.testclient import TestClient
from your_agent import app

client = TestClient(app)

def test_health_check():
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json()["status"] == "healthy"

def test_create_task():
    task_data = {
        "task_id": "test_task_123",
        "user_id": "test_user",
        "task_type": "process_document",
        "input_data": {"document": "test content"},
        "callback_url": "http://localhost/callback",
        "auth_token": "test_token"
    }
    
    response = client.post("/tasks", json=task_data)
    assert response.status_code == 200
    assert response.json()["accepted"] is True
```

### Integration Testing
```bash
# Test agent registration with SwAIvyn
curl -X POST http://localhost:5000/api/agent-registry \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Agent",
    "description": "Testing agent integration",
    "endpoint_url": "http://localhost:8000",
    "agent_type": "test_processor"
  }'

# Test task creation
curl -X POST http://localhost:5000/api/agent-tasks \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "registry_id": "YOUR_AGENT_ID",
    "task_type": "test_task",
    "input_data": {"test": "data"}
  }'
```

##  Additional Resources

- [SwAIvyn API Documentation](../README.md#api-endpoints)
- [Authentication Guide](./AUTHENTICATION.md)
- [Deployment Guide](./DEPLOYMENT.md)
- [Example Agents Repository](https://github.com/SweetingTech/SwAIvyn-Agents)

##  Support

For technical support:
1. Check the [troubleshooting guide](./TROUBLESHOOTING.md)
2. Review [common issues](./FAQ.md)
3. Join the [community Discord](https://discord.gg/swaivy)
4. File an issue on [GitHub](https://github.com/SweetingTech/SwAIvyn/issues)

##  License

External agents connecting to SwAIvyn must comply with the [MIT License](../LICENSE) terms.