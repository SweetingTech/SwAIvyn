# Agent Stack Integration Guide

This comprehensive guide provides all technical specifications needed for LLMs and developers to build external agent systems that integrate with SwAIvyn. It covers networking, data formats, API structures, and implementation patterns for seamless agent integration.

## 🎯 Overview

SwAIvyn's external agent system enables distributed AI task processing while maintaining complete user data isolation and security. This guide provides the complete technical specification for building agents that can seamlessly integrate with SwAIvyn's architecture.

### Core Principles

- **User Data Isolation**: Every operation is scoped to specific users with no cross-contamination
- **Secure Communication**: JWT-based authentication for all agent-to-SwAIvyn communication
- **Data Format Standardization**: Consistent formats for all data types (text, images, vectors, structured data)
- **Service Discovery**: Automatic capability detection and health monitoring
- **Scalable Architecture**: Support for multiple concurrent agents and tasks

## 🌐 Network Configuration

### Port Requirements

SwAIvyn requires specific port configurations depending on your deployment environment:

#### Local Development Environment
```yaml
# SwAIvyn Core Services
frontend: 5173          # React/Vite development server
backend: 8000            # FastAPI BFF service
traefik: 80             # Reverse proxy and load balancer
database: 5432          # PostgreSQL database

# Infrastructure Services  
temporal: 7233          # Workflow orchestration
qdrant: 6333           # Vector database
neo4j: 7474/7687       # Graph database (HTTP/Bolt)
tts: 8081              # Fish Speech TTS service
stt: 9000              # Whisper STT service
elevenlabs: 8082       # ElevenLabs TTS adapter

# External Agent Ports (Recommended)
agent_services: 8500-8599   # Range for external agent services
agent_callbacks: 8600-8699  # Range for agent callback endpoints
```

#### Replit/Cloud Environment
```yaml
# SwAIvyn Core Services
frontend: 5000          # Combined frontend serving
backend: 8000           # FastAPI BFF service
database: 5432          # PostgreSQL database

# External Agent Ports
agent_services: 3000-3003, 8000-8081  # Available external ports
```

#### Production/Docker Swarm
```yaml
# All services route through Traefik
traefik: 80             # Main entry point
traefik_dashboard: 80   # Dashboard at traefik.localhost

# Service Discovery via Docker Labels
# Each service gets a *.localhost subdomain
```

### Network Security Requirements

```yaml
# Firewall Rules (if applicable)
ingress:
  - port: 80              # Traefik (local dev)
  - port: 5000            # Frontend (Replit)
  - port: 8000            # Backend API
  - port: 8500-8699       # Agent service range

# CORS Configuration
allowed_origins:
  - "http://localhost:5000"       # Frontend (Replit)
  - "http://localhost:5173"       # Frontend (local dev)
  - "http://127.0.0.1:5000"
  - "http://127.0.0.1:5173"
  - "*.repl.co"                   # Replit domains
  - "*.localhost"                 # Local development subdomains
```

## 🔐 Authentication System

### JWT Token Structure

SwAIvyn uses JWT tokens for all agent authentication. Each token is scoped to a specific user.

```json
{
  "header": {
    "alg": "HS256",
    "typ": "JWT"
  },
  "payload": {
    "user_id": "user_123",
    "username": "john_doe", 
    "role": "user",
    "exp": 1735689600,
    "iat": 1735603200
  }
}
```

### Obtaining Authentication Tokens

#### Method 1: API Login
```http
POST http://localhost:8000/api/auth/login
Content-Type: application/json

{
    "username": "your_username",
    "password": "your_password"
}

# Response
{
    "access_token": "eyJ0eXAiOiJKV1QiLCJhbGc...",
    "token_type": "bearer",
    "user": {
        "id": "user_123",
        "username": "your_username",
        "role": "user"
    }
}
```

#### Method 2: Frontend API Key Generation
1. Login to SwAIvyn UI
2. Navigate to Settings → API Keys  
3. Click "Generate New Key"
4. Copy the generated JWT token

### Using Authentication Headers

All agent API calls must include the JWT token:

```http
Authorization: Bearer <jwt-token>
Content-Type: application/json
X-Agent-ID: <your-agent-id>  # Optional: Agent identification
```

## 📊 API Structure

### Core SwAIvyn API Endpoints

#### Agent Registration
```http
POST /api/agents/register
Authorization: Bearer <jwt-token>
Content-Type: application/json

{
    "name": "Document Processor Agent",
    "description": "Processes documents and extracts key information",
    "endpoint_url": "https://your-agent.example.com",
    "agent_type": "document_processor",
    "capabilities": [
        "text_extraction",
        "pdf_processing", 
        "image_analysis",
        "vector_generation"
    ],
    "supported_formats": [
        "text/plain",
        "application/pdf",
        "image/jpeg", 
        "image/png",
        "application/json"
    ],
    "max_file_size": 10485760,  # 10MB in bytes
    "api_version": "1.0.0",
    "health_check_interval": 300  # 5 minutes
}

# Response
{
    "registry_id": "agent_reg_456",
    "status": "registered",
    "assigned_capabilities": ["text_extraction", "pdf_processing"],
    "health_check_url": "/api/agents/health/agent_reg_456"
}
```

#### Task Creation
```http
POST /api/agents/tasks
Authorization: Bearer <jwt-token>
Content-Type: application/json

{
    "registry_id": "agent_reg_456",
    "task_type": "process_document",
    "input_data": {
        "document_url": "https://storage.example.com/doc.pdf",
        "document_content": "base64encoded_content...",
        "processing_options": {
            "extract_text": true,
            "generate_embeddings": true,
            "extract_images": false
        }
    },
    "metadata": {
        "user_context": "Research project",
        "priority": "high",
        "deadline": "2025-01-15T10:00:00Z"
    },
    "callback_url": "https://your-callback.example.com/task-complete",
    "expected_duration": 300  # seconds
}

# Response  
{
    "task_id": "task_789",
    "status": "queued",
    "created_at": "2025-01-10T09:00:00Z",
    "estimated_completion": "2025-01-10T09:05:00Z",
    "status_url": "/api/agents/tasks/task_789/status"
}
```

#### Task Status Monitoring
```http
GET /api/agents/tasks/task_789/status
Authorization: Bearer <jwt-token>

# Response
{
    "task_id": "task_789",
    "status": "processing",  # queued, processing, completed, failed
    "progress": 65,  # 0-100 percentage
    "current_step": "generating_embeddings", 
    "steps_completed": ["document_parsing", "text_extraction"],
    "steps_remaining": ["vector_storage", "result_packaging"],
    "estimated_completion": "2025-01-10T09:03:00Z",
    "logs": [
        {
            "timestamp": "2025-01-10T09:01:30Z",
            "level": "info",
            "message": "Document parsing completed successfully"
        }
    ]
}
```

#### Results Retrieval
```http
GET /api/agents/tasks/task_789/results
Authorization: Bearer <jwt-token>

# Response
{
    "task_id": "task_789",
    "status": "completed",
    "completed_at": "2025-01-10T09:04:30Z",
    "results": {
        "extracted_text": "Full document text content...",
        "embeddings": [
            {
                "chunk_id": "chunk_001",
                "text": "First paragraph content...",
                "vector": [0.1, -0.3, 0.7, ...],  # 1536 dimensions
                "metadata": {
                    "page": 1,
                    "position": {"x": 50, "y": 100}
                }
            }
        ],
        "extracted_images": [
            {
                "image_id": "img_001", 
                "url": "/api/results/task_789/images/img_001.jpg",
                "format": "image/jpeg",
                "size": {"width": 800, "height": 600},
                "metadata": {
                    "page": 2,
                    "description": "Chart showing quarterly results"
                }
            }
        ],
        "document_metadata": {
            "pages": 10,
            "word_count": 2500,
            "language": "en",
            "confidence": 0.95
        }
    },
    "processing_time": 270,  # seconds
    "agent_info": {
        "agent_id": "agent_reg_456",
        "version": "1.0.0",
        "processing_model": "document-ai-v3"
    }
}
```

### Data Ingestion API

SwAIvyn provides endpoints for agents to send processed data back:

```http
POST /api/agents/ingest/text
Authorization: Bearer <jwt-token>
Content-Type: application/json

{
    "source_task_id": "task_789",
    "content": {
        "text": "Processed text content...",
        "chunks": [
            {
                "id": "chunk_001",
                "text": "First section content...", 
                "metadata": {
                    "section": "introduction",
                    "confidence": 0.92
                }
            }
        ],
        "summary": "Brief summary of the content...",
        "keywords": ["AI", "processing", "documents"],
        "language": "en",
        "processing_model": "text-analyzer-v2"
    },
    "storage_preferences": {
        "create_memory": true,
        "generate_embeddings": true,
        "category": "documents"
    }
}
```

```http
POST /api/agents/ingest/vectors
Authorization: Bearer <jwt-token>  
Content-Type: application/json

{
    "source_task_id": "task_789",
    "vectors": [
        {
            "id": "vec_001",
            "vector": [0.1, -0.3, 0.7, ...],  # 1536 dimensions (OpenAI format)
            "text": "Associated text content...",
            "metadata": {
                "source_document": "doc_123.pdf",
                "chunk_position": 1,
                "semantic_type": "paragraph"
            }
        }
    ],
    "collection_name": "user_documents",
    "embedding_model": "text-embedding-ada-002",
    "dimension": 1536
}
```

```http
POST /api/agents/ingest/images
Authorization: Bearer <jwt-token>
Content-Type: multipart/form-data

# Form data:
source_task_id: task_789
image_file: [binary image data]
metadata: {
    "original_filename": "chart.png",
    "description": "Quarterly revenue chart", 
    "extracted_text": "Q1: $1.2M, Q2: $1.5M...",
    "image_type": "chart",
    "processing_model": "vision-ai-v1"
}
```

## 📁 Data Format Standards

### Text Data Format

```json
{
    "format": "text/plain",
    "encoding": "utf-8",
    "content": {
        "raw_text": "Original unprocessed text...",
        "processed_text": "Cleaned and normalized text...", 
        "chunks": [
            {
                "id": "unique_chunk_id",
                "text": "Chunk content...",
                "start_position": 0,
                "end_position": 150,
                "metadata": {
                    "section": "introduction",
                    "importance_score": 0.85,
                    "language": "en",
                    "confidence": 0.92
                }
            }
        ],
        "extracted_entities": [
            {
                "text": "OpenAI",
                "type": "ORG", 
                "confidence": 0.95,
                "position": {"start": 45, "end": 51}
            }
        ],
        "summary": "Brief content summary...",
        "keywords": ["keyword1", "keyword2"],
        "language": "en",
        "word_count": 1250
    },
    "processing_info": {
        "model": "text-processor-v2",
        "timestamp": "2025-01-10T09:00:00Z",
        "processing_time": 2.3
    }
}
```

### Vector Data Format

SwAIvyn supports multiple vector formats but standardizes on OpenAI's embedding format:

```json
{
    "format": "vector/embeddings",
    "embedding_model": "text-embedding-ada-002",
    "dimension": 1536,
    "vectors": [
        {
            "id": "vec_unique_id",
            "vector": [
                0.123, -0.456, 0.789, 
                // ... 1536 total dimensions
            ],
            "text": "Source text for this embedding...",
            "metadata": {
                "document_id": "doc_123", 
                "chunk_index": 0,
                "semantic_type": "paragraph",
                "timestamp": "2025-01-10T09:00:00Z",
                "confidence": 0.94,
                "source": "pdf_extraction"
            }
        }
    ],
    "collection_metadata": {
        "total_vectors": 150,
        "average_similarity": 0.82,
        "processing_model": "embedding-generator-v3"
    }
}
```

### Image Data Format

```json
{
    "format": "image/multimodal",
    "images": [
        {
            "id": "img_unique_id",
            "image_data": {
                "format": "image/jpeg",  # jpeg, png, webp, gif
                "base64": "data:image/jpeg;base64,/9j/4AAQSkZJRgABA...",
                "url": "https://storage.example.com/images/img_123.jpg",
                "size": {
                    "width": 1920,
                    "height": 1080,
                    "file_size": 245760  # bytes
                }
            },
            "analysis": {
                "extracted_text": "Text found in image via OCR...",
                "description": "AI-generated description of image content...",
                "objects": [
                    {
                        "name": "person",
                        "confidence": 0.92,
                        "bounding_box": {"x": 100, "y": 150, "width": 200, "height": 300}
                    }
                ],
                "scene_type": "office",
                "color_palette": ["#3498db", "#2ecc71", "#f39c12"],
                "embedding": [0.1, -0.2, 0.8, ...]  # Image embedding vector
            },
            "metadata": {
                "source_document": "presentation.pdf",
                "page_number": 5,
                "extraction_timestamp": "2025-01-10T09:00:00Z",
                "processing_model": "vision-ai-v2"
            }
        }
    ]
}
```

### Structured Data Format

For complex structured data (JSON, CSV, databases):

```json
{
    "format": "structured/json",
    "schema": {
        "type": "object",
        "properties": {
            "customers": {
                "type": "array", 
                "items": {
                    "type": "object",
                    "properties": {
                        "id": {"type": "string"},
                        "name": {"type": "string"},
                        "email": {"type": "string"}
                    }
                }
            }
        }
    },
    "data": {
        "customers": [
            {"id": "cust_001", "name": "John Doe", "email": "john@example.com"}
        ]
    },
    "metadata": {
        "source_format": "csv",
        "row_count": 1500,
        "column_count": 8,
        "data_quality_score": 0.87,
        "processing_timestamp": "2025-01-10T09:00:00Z"
    },
    "transformations": [
        {
            "operation": "csv_to_json",
            "parameters": {"delimiter": ",", "header": true},
            "timestamp": "2025-01-10T09:00:00Z"
        }
    ]
}
```

## 🤖 Agent Registration Process

### Step 1: Agent Service Implementation

Your agent service must implement these required endpoints:

```python
# Required Agent Service Endpoints

@app.get("/health")
async def health_check():
    """Health check endpoint for SwAIvyn monitoring"""
    return {
        "status": "healthy",
        "version": "1.0.0",
        "timestamp": datetime.utcnow().isoformat(),
        "capabilities": ["text_processing", "pdf_analysis"],
        "current_load": 0.3,  # 0.0-1.0 scale
        "available_capacity": 10  # concurrent tasks
    }

@app.get("/capabilities")
async def get_capabilities():
    """Describe agent capabilities and requirements"""
    return {
        "supported_formats": [
            "text/plain", "application/pdf", "image/jpeg"
        ],
        "max_file_size": 10485760,  # 10MB
        "max_concurrent_tasks": 10,
        "estimated_processing_time": {
            "text": 5,     # seconds per 1000 words  
            "pdf": 30,    # seconds per MB
            "image": 15   # seconds per image
        },
        "output_formats": [
            "text/processed", "vector/embeddings", "image/analyzed"
        ]
    }

@app.post("/tasks")
async def process_task(task_request: TaskRequest):
    """Process task from SwAIvyn"""
    task_id = task_request.task_id
    input_data = task_request.input_data
    
    # Your processing logic here
    result = await process_input_data(input_data)
    
    # Send results back to SwAIvyn
    await send_results_to_swaivyn(task_id, result)
    
    return {"status": "accepted", "estimated_completion": "300"}
```

### Step 2: Register with SwAIvyn

```python
import httpx
import asyncio

async def register_agent():
    """Register this agent with SwAIvyn"""
    
    # Get JWT token (implement your auth method)
    token = await get_jwt_token()
    
    registration_data = {
        "name": "Advanced Document Processor",
        "description": "Processes documents with AI analysis and vector generation",
        "endpoint_url": "https://your-agent.yourdomain.com",
        "agent_type": "document_processor",
        "capabilities": [
            "text_extraction",
            "pdf_processing",
            "image_analysis", 
            "vector_generation",
            "entity_recognition"
        ],
        "supported_formats": [
            "text/plain",
            "application/pdf", 
            "image/jpeg",
            "image/png",
            "application/json"
        ],
        "max_file_size": 50485760,  # 50MB
        "api_version": "1.0.0",
        "health_check_interval": 300
    }
    
    async with httpx.AsyncClient() as client:
        response = await client.post(
            "http://localhost:8000/api/agents/register",
            headers={"Authorization": f"Bearer {token}"},
            json=registration_data
        )
        
        if response.status_code == 200:
            result = response.json()
            agent_id = result["registry_id"]
            print(f"Successfully registered agent: {agent_id}")
            return agent_id
        else:
            print(f"Registration failed: {response.text}")
            return None

# Run registration
agent_id = asyncio.run(register_agent())
```

### Step 3: Implement Task Processing Loop

```python
async def main_processing_loop():
    """Main loop for processing tasks from SwAIvyn"""
    
    while True:
        try:
            # Poll for new tasks
            tasks = await poll_for_tasks()
            
            for task in tasks:
                # Process task asynchronously
                asyncio.create_task(process_task_async(task))
            
            # Wait before next poll
            await asyncio.sleep(10)  # 10 second polling interval
            
        except Exception as e:
            print(f"Error in processing loop: {e}")
            await asyncio.sleep(30)  # Longer wait on error

async def process_task_async(task):
    """Process individual task"""
    try:
        task_id = task["task_id"]
        input_data = task["input_data"]
        
        # Update task status to processing
        await update_task_status(task_id, "processing", progress=0)
        
        # Process the data
        if task["task_type"] == "process_document":
            result = await process_document(input_data)
        elif task["task_type"] == "generate_embeddings":  
            result = await generate_embeddings(input_data)
        else:
            raise ValueError(f"Unknown task type: {task['task_type']}")
        
        # Send results back to SwAIvyn
        await send_results_to_swaivyn(task_id, result)
        
        # Mark task as completed
        await update_task_status(task_id, "completed", progress=100)
        
    except Exception as e:
        await update_task_status(task_id, "failed", error=str(e))
```

## 🔧 Service Registration

### Service Discovery Pattern

SwAIvyn uses a service registry pattern for agent discovery:

```python
# Service registration with capability advertising
service_config = {
    "service_name": "advanced-doc-processor",
    "version": "2.1.0",
    "capabilities": {
        "text_processing": {
            "languages": ["en", "es", "fr", "de"],
            "max_length": 100000,  # characters
            "features": ["summarization", "entity_extraction", "sentiment"]
        },
        "vector_generation": {
            "models": ["ada-002", "e5-large", "custom-v1"],
            "dimensions": [1536, 4096, 768],
            "batch_size": 1000
        },
        "image_analysis": {
            "formats": ["jpg", "png", "pdf", "tiff"],
            "max_resolution": "4K",
            "features": ["ocr", "object_detection", "scene_analysis"]
        }
    },
    "resource_requirements": {
        "cpu_cores": 4,
        "ram_gb": 16,
        "gpu_required": true,
        "gpu_memory_gb": 8
    },
    "sla": {
        "availability": 99.5,  # percentage
        "max_response_time": 300,  # seconds
        "max_queue_length": 50
    }
}
```

### Health Monitoring Implementation

```python
@app.get("/health/detailed")
async def detailed_health():
    """Comprehensive health check for monitoring"""
    
    # Check system resources
    cpu_usage = psutil.cpu_percent(interval=1)
    memory = psutil.virtual_memory()
    disk = psutil.disk_usage('/')
    
    # Check dependencies
    db_healthy = await check_database_connection()
    model_loaded = await check_ai_model_status()
    
    # Check current load
    active_tasks = await get_active_task_count()
    queue_length = await get_queue_length()
    
    health_data = {
        "status": "healthy" if all([db_healthy, model_loaded]) else "degraded",
        "timestamp": datetime.utcnow().isoformat(),
        "version": "2.1.0",
        "uptime_seconds": int(time.time() - start_time),
        
        # Resource utilization
        "resources": {
            "cpu_usage": cpu_usage,
            "memory_usage": memory.percent,
            "disk_usage": disk.percent,
            "gpu_usage": await get_gpu_usage() if gpu_available else None
        },
        
        # Service status
        "services": {
            "database": "healthy" if db_healthy else "error",
            "ai_model": "loaded" if model_loaded else "error",
            "vector_store": await check_vector_store_status()
        },
        
        # Load information
        "load": {
            "active_tasks": active_tasks,
            "queue_length": queue_length,
            "capacity_utilization": active_tasks / max_concurrent_tasks,
            "estimated_queue_time": queue_length * average_task_time
        },
        
        # Capability status
        "capabilities": {
            "text_processing": model_loaded,
            "vector_generation": model_loaded,
            "image_analysis": await check_vision_model_status()
        }
    }
    
    return health_data
```

## 📥 Data Ingestion Patterns

### Real-time Data Streaming

For high-volume or real-time data processing:

```python
import asyncio
import websockets
from typing import AsyncGenerator

async def stream_results_to_swaivyn(task_id: str, results: AsyncGenerator):
    """Stream processing results in real-time"""
    
    ws_url = f"ws://localhost:8000/api/agents/stream/{task_id}"
    headers = {"Authorization": f"Bearer {jwt_token}"}
    
    async with websockets.connect(ws_url, extra_headers=headers) as websocket:
        async for result_chunk in results:
            # Send incremental results
            message = {
                "type": "partial_result",
                "task_id": task_id,
                "timestamp": datetime.utcnow().isoformat(),
                "data": result_chunk,
                "progress": result_chunk.get("progress", 0)
            }
            
            await websocket.send(json.dumps(message))
            
        # Send completion signal
        completion_message = {
            "type": "task_complete",
            "task_id": task_id,
            "timestamp": datetime.utcnow().isoformat(),
            "final_result": True
        }
        
        await websocket.send(json.dumps(completion_message))
```

### Batch Data Processing

For large datasets or batch operations:

```python
async def process_large_dataset(task_id: str, dataset_url: str):
    """Process large datasets in chunks with progress updates"""
    
    # Download and prepare dataset
    dataset = await download_dataset(dataset_url)
    total_items = len(dataset)
    batch_size = 100
    
    results = []
    
    for i in range(0, total_items, batch_size):
        batch = dataset[i:i + batch_size]
        
        # Process batch
        batch_results = await process_batch(batch)
        results.extend(batch_results)
        
        # Update progress
        progress = int((i + batch_size) / total_items * 100)
        await update_task_progress(task_id, progress)
        
        # Send intermediate results
        await send_batch_results(task_id, batch_results, progress)
    
    # Send final consolidated results
    await send_final_results(task_id, results)

async def send_batch_results(task_id: str, batch_results: list, progress: int):
    """Send batch processing results to SwAIvyn"""
    
    payload = {
        "task_id": task_id,
        "type": "batch_complete",
        "progress": progress,
        "batch_size": len(batch_results),
        "results": batch_results,
        "timestamp": datetime.utcnow().isoformat()
    }
    
    async with httpx.AsyncClient() as client:
        await client.post(
            f"http://localhost:8000/api/agents/ingest/batch",
            headers={"Authorization": f"Bearer {jwt_token}"},
            json=payload
        )
```

### File Processing Pipeline

Complete file processing implementation:

```python
async def process_file_upload(task_id: str, file_data: bytes, file_metadata: dict):
    """Complete file processing pipeline"""
    
    file_type = file_metadata.get("content_type", "application/octet-stream")
    filename = file_metadata.get("filename", "unknown")
    
    try:
        # Step 1: File validation and preprocessing
        await update_task_status(task_id, "processing", 10, "Validating file")
        
        if not await validate_file(file_data, file_type):
            raise ValueError("Invalid or corrupted file")
        
        # Step 2: Extract content based on file type
        await update_task_status(task_id, "processing", 30, "Extracting content")
        
        if file_type == "application/pdf":
            content = await extract_pdf_content(file_data)
        elif file_type.startswith("image/"):
            content = await process_image(file_data)
        elif file_type == "text/plain":
            content = {"text": file_data.decode('utf-8')}
        else:
            content = await generic_file_processing(file_data, file_type)
        
        # Step 3: Generate embeddings if text content exists  
        if content.get("text"):
            await update_task_status(task_id, "processing", 60, "Generating embeddings")
            embeddings = await generate_text_embeddings(content["text"])
            content["embeddings"] = embeddings
        
        # Step 4: Extract and process images if any
        if content.get("images"):
            await update_task_status(task_id, "processing", 80, "Processing images")
            for image in content["images"]:
                image["analysis"] = await analyze_image(image["data"])
        
        # Step 5: Send processed results to SwAIvyn
        await update_task_status(task_id, "processing", 95, "Sending results")
        
        result_payload = {
            "task_id": task_id,
            "original_file": {
                "filename": filename,
                "size": len(file_data),
                "type": file_type
            },
            "processed_content": content,
            "processing_metadata": {
                "agent_version": "2.1.0",
                "processing_time": time.time() - start_time,
                "timestamp": datetime.utcnow().isoformat()
            }
        }
        
        # Send to appropriate ingestion endpoint based on content type
        if content.get("embeddings"):
            await ingest_vectors(task_id, content["embeddings"])
        
        if content.get("images"):
            await ingest_images(task_id, content["images"])
        
        if content.get("text"):
            await ingest_text(task_id, content["text"])
        
        # Mark task as completed
        await update_task_status(task_id, "completed", 100, "Processing complete")
        
    except Exception as e:
        await update_task_status(task_id, "failed", error=str(e))
        raise

async def ingest_vectors(task_id: str, embeddings: list):
    """Send vector embeddings to SwAIvyn vector storage"""
    
    vectors_payload = {
        "source_task_id": task_id,
        "vectors": embeddings,
        "collection_name": f"user_task_{task_id}",
        "embedding_model": "text-embedding-ada-002",
        "dimension": 1536
    }
    
    async with httpx.AsyncClient() as client:
        response = await client.post(
            "http://localhost:8000/api/agents/ingest/vectors",
            headers={"Authorization": f"Bearer {jwt_token}"},
            json=vectors_payload
        )
        
        if response.status_code != 200:
            raise Exception(f"Vector ingestion failed: {response.text}")

async def ingest_images(task_id: str, images: list):
    """Send processed images to SwAIvyn storage"""
    
    for image in images:
        # Prepare multipart form data
        files = {
            "image_file": (
                image.get("filename", "image.jpg"),
                image["data"],
                image.get("content_type", "image/jpeg")
            )
        }
        
        data = {
            "source_task_id": task_id,
            "metadata": json.dumps(image.get("metadata", {}))
        }
        
        async with httpx.AsyncClient() as client:
            response = await client.post(
                "http://localhost:8000/api/agents/ingest/images",
                headers={"Authorization": f"Bearer {jwt_token}"},
                files=files,
                data=data
            )
            
            if response.status_code != 200:
                raise Exception(f"Image ingestion failed: {response.text}")
```

## 🧪 Testing and Validation

### Agent Testing Framework

```python
import pytest
import asyncio
from unittest.mock import AsyncMock, patch

class TestAgentIntegration:
    
    @pytest.fixture
    async def agent_client(self):
        """Create test agent client"""
        return AgentTestClient(base_url="http://localhost:8000")
    
    @pytest.mark.asyncio
    async def test_agent_registration(self, agent_client):
        """Test agent registration process"""
        
        registration_data = {
            "name": "Test Agent",
            "description": "Test agent for integration testing",
            "endpoint_url": "http://localhost:8500",
            "agent_type": "test_processor",
            "capabilities": ["text_processing"]
        }
        
        response = await agent_client.register_agent(registration_data)
        
        assert response.status_code == 200
        assert "registry_id" in response.json()
        
        # Verify agent appears in registry
        agents = await agent_client.list_agents()
        agent_names = [agent["name"] for agent in agents.json()]
        assert "Test Agent" in agent_names
    
    @pytest.mark.asyncio
    async def test_task_processing_workflow(self, agent_client):
        """Test complete task processing workflow"""
        
        # Create task
        task_data = {
            "registry_id": "test_agent_id",
            "task_type": "process_text",
            "input_data": {"text": "Test document content"}
        }
        
        task_response = await agent_client.create_task(task_data)
        task_id = task_response.json()["task_id"]
        
        # Monitor task progress
        max_wait = 30  # seconds
        wait_time = 0
        
        while wait_time < max_wait:
            status = await agent_client.get_task_status(task_id)
            if status.json()["status"] == "completed":
                break
            await asyncio.sleep(1)
            wait_time += 1
        
        assert status.json()["status"] == "completed"
        
        # Verify results
        results = await agent_client.get_task_results(task_id)
        assert results.status_code == 200
        assert "results" in results.json()
    
    @pytest.mark.asyncio 
    async def test_data_ingestion(self, agent_client):
        """Test data ingestion endpoints"""
        
        # Test text ingestion
        text_data = {
            "source_task_id": "test_task_123",
            "content": {
                "text": "Test ingested content",
                "summary": "Test summary",
                "keywords": ["test", "content"]
            }
        }
        
        response = await agent_client.ingest_text(text_data)
        assert response.status_code == 200
        
        # Test vector ingestion
        vector_data = {
            "source_task_id": "test_task_123", 
            "vectors": [{
                "id": "test_vec_1",
                "vector": [0.1] * 1536,
                "text": "Test vector content"
            }]
        }
        
        response = await agent_client.ingest_vectors(vector_data)
        assert response.status_code == 200

class AgentTestClient:
    def __init__(self, base_url: str):
        self.base_url = base_url
        self.token = None
    
    async def authenticate(self, username: str, password: str):
        """Authenticate and get JWT token"""
        async with httpx.AsyncClient() as client:
            response = await client.post(
                f"{self.base_url}/api/auth/login",
                json={"username": username, "password": password}
            )
            if response.status_code == 200:
                self.token = response.json()["access_token"]
            return response
    
    async def register_agent(self, agent_data: dict):
        """Register test agent"""
        headers = {"Authorization": f"Bearer {self.token}"}
        async with httpx.AsyncClient() as client:
            return await client.post(
                f"{self.base_url}/api/agents/register",
                headers=headers,
                json=agent_data
            )
```

## 📈 Performance Optimization

### Caching Strategies

```python
from functools import lru_cache
import redis
import json

# Redis connection for distributed caching
redis_client = redis.Redis(host='localhost', port=6379, db=0)

@lru_cache(maxsize=1000)
def cache_embeddings_locally(text: str) -> list:
    """Local LRU cache for embeddings"""
    return generate_embeddings_sync(text)

async def cache_embeddings_redis(text: str) -> list:
    """Distributed Redis cache for embeddings"""
    cache_key = f"embedding:{hash(text)}"
    
    # Try to get from cache
    cached = redis_client.get(cache_key)
    if cached:
        return json.loads(cached)
    
    # Generate new embedding
    embedding = await generate_embeddings(text)
    
    # Cache with TTL (24 hours)
    redis_client.setex(
        cache_key, 
        86400,  # 24 hours
        json.dumps(embedding)
    )
    
    return embedding

async def batch_process_with_cache(texts: list[str]) -> list:
    """Batch process with intelligent caching"""
    results = []
    uncached_texts = []
    
    # Check cache for each text
    for text in texts:
        cached = await get_cached_result(text)
        if cached:
            results.append(cached)
        else:
            uncached_texts.append(text)
    
    # Process uncached texts in batch
    if uncached_texts:
        batch_results = await batch_generate_embeddings(uncached_texts)
        
        # Cache results
        for text, result in zip(uncached_texts, batch_results):
            await cache_result(text, result)
        
        results.extend(batch_results)
    
    return results
```

### Resource Management

```python
import asyncio
from asyncio import Semaphore
from contextlib import asynccontextmanager

class ResourceManager:
    def __init__(self, max_concurrent: int = 10, max_memory_mb: int = 4096):
        self.semaphore = Semaphore(max_concurrent)
        self.max_memory_mb = max_memory_mb
        self.current_memory_mb = 0
        self.memory_lock = asyncio.Lock()
    
    @asynccontextmanager
    async def acquire_resources(self, estimated_memory_mb: int = 100):
        """Acquire processing resources with memory management"""
        
        # Wait for available slot
        async with self.semaphore:
            # Check memory availability
            async with self.memory_lock:
                if self.current_memory_mb + estimated_memory_mb > self.max_memory_mb:
                    raise ResourceExhaustedException(
                        f"Insufficient memory: need {estimated_memory_mb}MB, "
                        f"have {self.max_memory_mb - self.current_memory_mb}MB available"
                    )
                self.current_memory_mb += estimated_memory_mb
            
            try:
                yield
            finally:
                # Release memory
                async with self.memory_lock:
                    self.current_memory_mb -= estimated_memory_mb

# Usage in task processing
resource_manager = ResourceManager(max_concurrent=10, max_memory_mb=8192)

async def process_large_document(document_data: bytes):
    """Process large document with resource management"""
    
    estimated_memory = len(document_data) // (1024 * 1024)  # MB
    
    async with resource_manager.acquire_resources(estimated_memory):
        # Process document within resource constraints
        result = await perform_heavy_processing(document_data)
        return result
```

## 🔍 Monitoring and Debugging

### Comprehensive Logging

```python
import logging
import json
from datetime import datetime
from typing import Any, Dict

class StructuredLogger:
    def __init__(self, service_name: str):
        self.service_name = service_name
        self.logger = logging.getLogger(service_name)
        
        # Configure structured logging
        handler = logging.StreamHandler()
        formatter = logging.Formatter(
            '%(asctime)s %(levelname)s %(name)s %(message)s'
        )
        handler.setFormatter(formatter)
        self.logger.addHandler(handler)
        self.logger.setLevel(logging.INFO)
    
    def log_structured(self, level: str, message: str, **kwargs):
        """Log with structured data"""
        log_entry = {
            "timestamp": datetime.utcnow().isoformat(),
            "service": self.service_name,
            "message": message,
            **kwargs
        }
        
        getattr(self.logger, level)(json.dumps(log_entry))
    
    def log_task_start(self, task_id: str, task_type: str, user_id: str):
        self.log_structured(
            "info",
            "Task processing started",
            task_id=task_id,
            task_type=task_type,
            user_id=user_id,
            event_type="task_start"
        )
    
    def log_task_progress(self, task_id: str, progress: int, step: str):
        self.log_structured(
            "info", 
            "Task progress update",
            task_id=task_id,
            progress=progress,
            current_step=step,
            event_type="task_progress"
        )
    
    def log_task_complete(self, task_id: str, processing_time: float, result_size: int):
        self.log_structured(
            "info",
            "Task completed successfully",
            task_id=task_id,
            processing_time_seconds=processing_time,
            result_size_bytes=result_size,
            event_type="task_complete"
        )
    
    def log_error(self, task_id: str, error: Exception, context: Dict[str, Any]):
        self.log_structured(
            "error",
            "Task processing failed", 
            task_id=task_id,
            error_type=type(error).__name__,
            error_message=str(error),
            context=context,
            event_type="task_error"
        )

# Usage
logger = StructuredLogger("document-processor-agent")

async def process_task_with_logging(task_id: str, task_data: dict):
    """Process task with comprehensive logging"""
    
    start_time = time.time()
    
    try:
        logger.log_task_start(
            task_id=task_id,
            task_type=task_data.get("task_type"),
            user_id=task_data.get("user_id")
        )
        
        # Process task with progress updates
        for step, progress in [("parsing", 20), ("analyzing", 50), ("generating", 80)]:
            logger.log_task_progress(task_id, progress, step)
            await perform_processing_step(step, task_data)
        
        result = await finalize_processing(task_data)
        
        logger.log_task_complete(
            task_id=task_id,
            processing_time=time.time() - start_time,
            result_size=len(json.dumps(result))
        )
        
        return result
        
    except Exception as e:
        logger.log_error(
            task_id=task_id,
            error=e,
            context={
                "task_type": task_data.get("task_type"),
                "input_size": len(str(task_data)),
                "processing_time": time.time() - start_time
            }
        )
        raise
```

## 🚀 Deployment Patterns

### Docker Container Deployment

```dockerfile
# Agent service Dockerfile
FROM python:3.11-slim

# Install system dependencies
RUN apt-get update && apt-get install -y \
    build-essential \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Copy requirements and install Python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY . .

# Create non-root user
RUN useradd -m -u 1000 agent && chown -R agent:agent /app
USER agent

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8500/health || exit 1

# Expose port
EXPOSE 8500

# Run application
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8500"]
```

```yaml
# docker-compose.yml for agent deployment
version: '3.8'

services:
  document-processor-agent:
    build: .
    ports:
      - "8500:8500"
    environment:
      - SWAIVYN_API_URL=http://host.docker.internal:8000
      - AGENT_NAME=Document Processor
      - LOG_LEVEL=INFO
      - MAX_CONCURRENT_TASKS=10
      - REDIS_URL=redis://redis:6379
    depends_on:
      - redis
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8500/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped
    
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    command: redis-server --appendonly yes
    volumes:
      - redis_data:/data
    restart: unless-stopped

volumes:
  redis_data:
```

### Kubernetes Deployment

```yaml
# kubernetes-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: document-processor-agent
spec:
  replicas: 3
  selector:
    matchLabels:
      app: document-processor-agent
  template:
    metadata:
      labels:
        app: document-processor-agent
    spec:
      containers:
      - name: agent
        image: your-registry/document-processor-agent:latest
        ports:
        - containerPort: 8500
        env:
        - name: SWAIVYN_API_URL
          value: "http://swaivyn-service:8000"
        - name: MAX_CONCURRENT_TASKS
          value: "5"
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "2Gi" 
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8500
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 8500
          initialDelaySeconds: 5
          periodSeconds: 5

---
apiVersion: v1
kind: Service
metadata:
  name: document-processor-agent-service
spec:
  selector:
    app: document-processor-agent
  ports:
  - port: 8500
    targetPort: 8500
  type: ClusterIP
```

This comprehensive guide provides everything needed to build external agents that integrate seamlessly with SwAIvyn's architecture. The specifications cover networking, authentication, data formats, and implementation patterns to ensure reliable and secure agent integration.