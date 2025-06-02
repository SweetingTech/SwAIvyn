import uvicorn
import asyncio
import json
import logging
import traceback
from typing import Optional, Dict, Any
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

# Import the existing search engine
from search import HybridSearchEngine

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(title="SwAIvyn Hybrid Search API", version="1.0.0")

# Add CORS middleware to allow requests from the C# backend
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # In production, specify your backend URL
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

class SearchRequest(BaseModel):
    query: str
    userId: str
    topK: int = 10
    filters: Optional[Dict[str, Any]] = None

class HealthResponse(BaseModel):
    status: str
    message: str
    timestamp: str

# Global search engine instance
search_engine: Optional[HybridSearchEngine] = None

@app.on_event("startup")
async def startup_event():
    """Initialize the search engine with database connections"""
    global search_engine
    
    try:
        logger.info("Initializing search engine...")
        
        # Initialize real database connections
        import sqlite3
        import weaviate
        from neo4j import GraphDatabase
        import os
        
        # SQLite connection - using same database as C# backend
        sqlite_path = os.path.join(os.path.dirname(__file__), "..", "data", "swai-vyn.db")
        sql_connection = sqlite3.connect(sqlite_path, check_same_thread=False)
        logger.info(f"✅ Connected to SQLite database: {sqlite_path}")        # Weaviate connection - using v4 client API
        weaviate_client = None
        try:
            # Skip gRPC init checks since we're connecting to remote instance
            weaviate_client = weaviate.connect_to_local(
                host="stabled",
                port=8080,
                grpc_port=50051,
                skip_init_checks=True
            )
            
            # Test Weaviate connection
            collections = weaviate_client.collections.list_all()
            logger.info(f"✅ Connected to Weaviate at http://stabled:8080 with {len(list(collections))} collections")

            # Define the Document collection schema
            document_collection_name = "Document"
            document_collection_properties = [
                wvc.config.Property(name="content", data_type=wvc.config.DataType.TEXT),
                wvc.config.Property(name="title", data_type=wvc.config.DataType.TEXT),
                wvc.config.Property(name="contentType", data_type=wvc.config.DataType.TEXT),
                wvc.config.Property(name="source", data_type=wvc.config.DataType.TEXT),
                wvc.config.Property(name="userId", data_type=wvc.config.DataType.TEXT),
            ]

            # Check if the collection exists, if not, create it
            if not weaviate_client.collections.exists(document_collection_name):
                logger.info(f"Creating Weaviate collection: {document_collection_name}")
                weaviate_client.collections.create(
                    name=document_collection_name,
                    properties=document_collection_properties,
                    vectorizer_config=wvc.config.Configure.Vectorizer.multi2vec_clip(
                        vectorize_collection_name=False
                    ),
                )
                logger.info(f"Successfully created Weaviate collection: {document_collection_name}")
            else:
                logger.info(f"Weaviate collection '{document_collection_name}' already exists.")

            # Temporary: Add some sample data to the Document collection for testing
            try:
                document_collection = weaviate_client.collections.get(document_collection_name)
                
                # Check if data already exists to avoid duplicates on reload
                if document_collection.query.aggregate(total_count=True).total_count == 0:
                    logger.info("Adding sample data to Weaviate 'Document' collection...")
                    document_collection.data.insert({
                        "content": "SwAIvyn is a comprehensive AI-powered personal assistant designed to enhance productivity and streamline daily tasks.",
                        "title": "SwAIvyn Project Overview",
                        "contentType": "text",
                        "source": "internal_docs",
                        "userId": "00000000-0000-0000-0000-000000000001"
                    })
                    logger.info("Successfully added sample data to Weaviate.")
                else:
                    logger.info("Weaviate 'Document' collection already contains data. Skipping sample data insertion.")
            except Exception as data_err:
                logger.warning(f"⚠️ Failed to add sample data to Weaviate: {data_err}")

        except Exception as we:
            logger.warning(f"⚠️ Failed to connect to Weaviate: {we}. Will use mock data.")
            weaviate_client = None
        
        # Neo4j connection - using same settings as C# backend
        neo4j_driver = None
        try:
            neo4j_driver = GraphDatabase.driver(
                "bolt://localhost:7687",
                auth=("neo4j", "password"),
                connection_timeout=10,
                max_connection_lifetime=3600
            )
            
            # Test Neo4j connection
            with neo4j_driver.session() as session:
                session.run("RETURN 1")
            logger.info("✅ Connected to Neo4j at bolt://localhost:7687")
        except Exception as ne:
            logger.warning(f"⚠️ Failed to connect to Neo4j: {ne}. Will use mock data.")
            neo4j_driver = None
        
        search_engine = HybridSearchEngine(
            sql_connection=sql_connection,
            weaviate_client=weaviate_client,
            neo4j_driver=neo4j_driver
        )
        
        logger.info("Search engine initialized successfully")
        
    except Exception as e:
        logger.error(f"Failed to initialize search engine: {str(e)}")
        logger.error(traceback.format_exc())
        raise

@app.get("/health", response_model=HealthResponse)
async def health_check():
    """Health check endpoint"""
    from datetime import datetime
    
    if search_engine is None:
        raise HTTPException(status_code=503, detail="Search engine not initialized")
    
    return HealthResponse(
        status="healthy",
        message="Hybrid search service is running",
        timestamp=datetime.utcnow().isoformat()
    )

@app.post("/search")
async def search(request: SearchRequest):
    """
    Main search endpoint that performs hybrid search across all databases
    """
    if search_engine is None:
        raise HTTPException(status_code=503, detail="Search engine not initialized")
    
    try:
        logger.info(f"Search request: query='{request.query}', userId={request.userId}, topK={request.topK}")
        
        # Add userId to filters if not present
        filters = request.filters or {}
        filters["userId"] = request.userId
        
        # Perform the search
        results = await search_engine.search(
            query=request.query,
            top_k=request.topK,
            filters=filters
        )
        
        # Convert results to JSON-serializable format
        json_results = []
        for result in results:
            json_results.append({
                "id": result.id,
                "title": result.title,
                "content": result.content,
                "score": result.score,
                "source": result.source,
                "metadata": result.metadata,
                "normalized_score": result.normalized_score
            })
        
        logger.info(f"Search completed: returned {len(json_results)} results")
        return {
            "results": json_results,
            "total_count": len(json_results),
            "query": request.query,
            "user_id": request.userId
        }
        
    except Exception as e:
        logger.error(f"Search error: {str(e)}")
        logger.error(traceback.format_exc())
        raise HTTPException(status_code=500, detail=f"Search failed: {str(e)}")

@app.get("/search/explain/{query}")
async def explain_search(query: str, user_id: str):
    """
    Get explanation of how search results were obtained
    """
    if search_engine is None:
        raise HTTPException(status_code=503, detail="Search engine not initialized")
    
    try:
        # Perform a search to get results
        filters = {"userId": user_id}
        results = await search_engine.search(query=query, top_k=10, filters=filters)
        
        # Get explanation
        explanation = search_engine.explain_results(query, results)
        
        return explanation
        
    except Exception as e:
        logger.error(f"Explain search error: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Explanation failed: {str(e)}")

@app.get("/search/status")
async def search_status():
    """
    Get detailed status of the search engine and its components
    """
    if search_engine is None:
        raise HTTPException(status_code=503, detail="Search engine not initialized")
    
    try:
        return {
            "search_engine": "initialized",
            "databases": {
                "sql": "connected" if search_engine.sql else "mock",
                "weaviate": "connected" if search_engine.weaviate else "mock", 
                "neo4j": "connected" if search_engine.neo4j else "mock"
            },
            "weights": search_engine.db_weights,
            "status": "operational"
        }
        
    except Exception as e:
        logger.error(f"Status check error: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Status check failed: {str(e)}")

if __name__ == "__main__":
    logger.info("Starting SwAIvyn Hybrid Search API...")
    uvicorn.run(
        app, 
        host="0.0.0.0", 
        port=8001,
        log_level="info",
        reload=False  # Set to True for development
    )
