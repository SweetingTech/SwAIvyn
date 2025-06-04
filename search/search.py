import asyncio
import re
import logging
from collections import defaultdict
from typing import List, Dict, Any, Optional, Tuple
from dataclasses import dataclass
import json
from datetime import datetime
import os
import urllib.parse

# FastAPI + Uvicorn
from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse
from fastapi.middleware.cors import CORSMiddleware
import uvicorn

# Asynchronous SQLite
import aiosqlite

# Neo4j driver
from neo4j import AsyncGraphDatabase
from neo4j.exceptions import ServiceUnavailable

# Weaviate v4 client
import weaviate
import weaviate.classes as wvc  # for v4 filter API

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)
logger.info("Starting search.py script...")

@dataclass
class SearchResult:
    """Standardized search result format"""
    id: str
    title: str
    content: str
    score: float
    source: str
    metadata: Dict[str, Any]
    normalized_score: Optional[float] = None

@dataclass
class QueryFeatures:
    """Analyzed query characteristics"""
    has_structured_filters: bool
    semantic_complexity: float
    entity_mentions: List[str]
    query_type: str  # factual, exploratory, relational
    keywords: List[str]

class HybridSearchEngine:
    """
    Hybrid search engine that focuses on Weaviate (vector) and Neo4j (graph) databases.
    SQLite is used minimally for lightweight metadata.
    """

    def __init__(
        self,
        sql_connection_string: str,
        weaviate_url: str,
        neo4j_uri: str,
        neo4j_user: str,
        neo4j_password: str
    ):
        logger.info("Initializing HybridSearchEngine...")
        self.sql_connection_string = sql_connection_string
        self.weaviate_url = weaviate_url
        self.neo4j_uri = neo4j_uri
        self.neo4j_user = neo4j_user
        self.neo4j_password = neo4j_password

        self.sql: Optional[aiosqlite.Connection] = None
        self.weaviate: Optional[weaviate.Client] = None
        self.neo4j: Optional[AsyncGraphDatabase.driver] = None

        self.logger = logger
        # Weights for fusion
        self.db_weights = {"sql": 0.1, "weaviate": 0.5, "neo4j": 0.4}

        logger.info("HybridSearchEngine initialized.")

    async def connect(self):
        logger.info("Attempting to connect to databases...")

        # ---------- SQLite Connection ----------
        try:
            sqlite_path = self.sql_connection_string.replace("file:", "").split("?")[0]
            absolute_sql_path = os.path.abspath(sqlite_path)
            self.sql = await aiosqlite.connect(absolute_sql_path)
            logger.info(f"Connected to SQLite: {absolute_sql_path}")
        except Exception as e:
            logger.error(f"Failed to connect to SQLite: {e}")
            self.sql = None

        # ---------- Weaviate v4 Connection ----------
        try:
            # Parse the weaviate_url to extract host and port
            parsed = urllib.parse.urlparse(self.weaviate_url)
            host = parsed.hostname or "localhost"
            port = parsed.port or 8080

            self.weaviate = weaviate.connect_to_local(
                host=host,
                port=port,
                grpc_port=50051,
            )
            # Verify connection
            self.weaviate.is_connected()
            logger.info(f"Connected to Weaviate: {self.weaviate_url}")
        except Exception as e:
            logger.error(f"Failed to connect to Weaviate: {e}")
            self.weaviate = None

        # ---------- Neo4j Connection ----------
        try:
            driver_instance = AsyncGraphDatabase.driver(
                self.neo4j_uri,
                auth=(self.neo4j_user, self.neo4j_password)
            )
            await driver_instance.verify_connectivity()
            self.neo4j = driver_instance
            logger.info(f"Connected to Neo4j: {self.neo4j_uri}")
        except Exception as e:
            logger.error(f"Failed to connect to Neo4j: {e}")
            self.neo4j = None

        if not (self.sql or self.weaviate or self.neo4j):
            logger.critical("No database connections established. Hybrid search will not function.")
        else:
            logger.info("Database connection attempts completed.")

    async def disconnect(self):
        logger.info("Disconnecting from databases...")
        if self.sql:
            await self.sql.close()
            logger.info("Disconnected from SQLite.")
        if self.weaviate:
            self.weaviate.close()
            logger.info("Disconnected from Weaviate.")
        if self.neo4j:
            await self.neo4j.close()
            logger.info("Disconnected from Neo4j.")
        logger.info("All database connections closed.")

    async def search(
        self,
        query: str,
        top_k: int = 20,
        filters: Optional[Dict[str, Any]] = None
    ) -> List[SearchResult]:
        """
        Main search method focusing on Weaviate (vector) and Neo4j (graph).
        """
        logger.info(f"Received search query: '{query}' with filters: {filters}")
        try:
            # 1. Query Analysis
            query_features = self.analyze_query(query)
            self.logger.info(f"Query analysis: {query_features}")

            # 2. Parallel Retrieval
            tasks: List[asyncio.Task] = []

            if self.weaviate:
                tasks.append(self.weaviate_search(query, query_features, filters))
            else:
                logger.warning("Weaviate client not available, skipping Weaviate search.")
                tasks.append(asyncio.sleep(0, result=[]))

            if self.neo4j:
                tasks.append(self.neo4j_search(query, query_features, filters))
            else:
                logger.warning("Neo4j driver not available, skipping Neo4j search.")
                tasks.append(asyncio.sleep(0, result=[]))

            vector_results, graph_results = await asyncio.gather(*tasks)
            logger.info(f"Weaviate returned {len(vector_results)}, Neo4j returned {len(graph_results)}")

            # 3. Normalize Scores
            normalized_results = self.normalize_scores([
                ("weaviate", vector_results),
                ("neo4j", graph_results),
            ])
            logger.info(f"Normalized {len(normalized_results)} results")

            # 4. Fuse Results
            fused_results = self.reciprocal_rank_fusion(normalized_results)
            logger.info(f"Fused down to {len(fused_results)} results")

            # 5. (Optional) Re‐rank by cross‐encoder
            final_results = await self.cross_encoder_rerank(query, fused_results)
            logger.info(f"Final results count after re‐ranking: {len(final_results)}")

            return final_results[:top_k]

        except Exception as e:
            self.logger.error(f"Search error: {e}", exc_info=True)
            raise

    def analyze_query(self, query: str) -> QueryFeatures:
        """
        Analyze query to determine search strategy/routing.
        """
        has_filters = bool(
            re.search(r"\b(after|before|from|to|type|category):", query, re.IGNORECASE)
        )
        entities = self.extract_entities(query)
        complexity = min(1.0, len(query.split()) / 20.0)
        query_type = self.classify_query_type(query)
        keywords = self.extract_keywords(query)

        return QueryFeatures(
            has_structured_filters=has_filters,
            semantic_complexity=complexity,
            entity_mentions=entities,
            query_type=query_type,
            keywords=keywords,
        )

    def extract_entities(self, query: str) -> List[str]:
        """Extract named entities (simple heuristic)."""
        entities: List[str] = []
        capitalized = re.findall(r"\b[A-Z][a-z]+\b", query)
        entities.extend(capitalized)
        dates = re.findall(r"\b\d{4}[-/]\d{1,2}[-/]\d{1,2}\b", query)
        entities.extend(dates)
        return list(set(entities))

    def classify_query_type(self, query: str) -> str:
        """Simple rule-based query type classification."""
        ql = query.lower()
        if any(word in ql for word in ["what", "who", "when", "where", "how"]):
            return "factual"
        elif any(word in ql for word in ["related", "similar", "connected", "linked"]):
            return "relational"
        else:
            return "exploratory"

    def extract_keywords(self, query: str) -> List[str]:
        """Extract important keywords (filtering out common stop words)."""
        stop_words = {
            "the", "a", "an", "and", "or", "but", "in", "on", "at",
            "to", "for", "of", "with", "by"
        }
        words = re.findall(r"\b\w+\b", query.lower())
        return [w for w in words if w not in stop_words and len(w) > 2]

    async def weaviate_search(
        self,
        query: str,
        features: QueryFeatures,
        filters: Optional[Dict[str, Any]] = None
    ) -> List[SearchResult]:
        """
        Perform a vector similarity search in Weaviate.
        """
        if not self.weaviate:
            self.logger.warning("Weaviate client not available, returning mock data")
            return self._get_mock_weaviate_results(query, 5)

        try:
            collections_list = self.weaviate.collections.list_all()
            available_collections = [col.name for col in collections_list]

            if not available_collections:
                self.logger.warning("No Weaviate collections found; returning mock results")
                return self._get_mock_weaviate_results(query, 5)

            self.logger.info(f"Available Weaviate collections: {available_collections}")

            target_collection = None
            for preferred in ["Document", "Content", "Message", "Chat"]:
                if preferred in available_collections:
                    target_collection = preferred
                    break
            if not target_collection:
                target_collection = available_collections[0]

            self.logger.info(f"Using Weaviate collection: {target_collection}")
            collection = self.weaviate.collections.get(target_collection)

            where_filter = None
            if filters and filters.get("userId"):
                user_id = str(filters["userId"])
                try:
                    where_filter = wvc.query.Filter.by_property("userId").equal(user_id)
                except Exception as fe:
                    self.logger.warning(f"Could not build userId filter: {fe}")
                    where_filter = None

            if where_filter:
                response = collection.query.near_text(
                    query=query,
                    limit=50,
                    where=where_filter,
                    return_metadata=wvc.query.MetadataQuery(distance=True, score=True),
                )
            else:
                response = collection.query.near_text(
                    query=query,
                    limit=50,
                    return_metadata=wvc.query.MetadataQuery(distance=True, score=True),
                )

            results: List[SearchResult] = []
            for i, obj in enumerate(response.objects):
                distance = obj.metadata.distance if obj.metadata.distance else 0.5
                similarity_score = max(0.0, 1.0 - distance)

                content = (
                    obj.properties.get("content")
                    or obj.properties.get("text")
                    or obj.properties.get("message")
                    or obj.properties.get("body")
                    or str(obj.properties)
                )

                results.append(
                    SearchResult(
                        id=str(obj.uuid),
                        title=obj.properties.get("title", f"Weaviate Document {i+1}"),
                        content=content[:200] + "..." if len(content) > 200 else content,
                        score=similarity_score,
                        source="weaviate",
                        metadata={
                            "content_type": obj.properties.get("contentType", "unknown"),
                            "origin": obj.properties.get("source", "weaviate_db"),
                            "user_id": obj.properties.get("userId", ""),
                            "distance": distance,
                            "search_type": "semantic",
                            "collection": target_collection,
                        },
                    )
                )

            self.logger.info(f"Weaviate search returned {len(results)} results")
            return results

        except Exception as e:
            self.logger.error(f"Weaviate search error: {e}", exc_info=True)
            return self._get_mock_weaviate_results(query, 3)

    def _get_mock_weaviate_results(self, query: str, count: int) -> List[SearchResult]:
        """Fallback mock Weaviate results if live connection fails."""
        return [
            SearchResult(
                id=f"weaviate_mock_{i}",
                title=f"MockDoc {i}",
                content=f"Mock content for '{query}'",
                score=0.9 - (i * 0.05),
                source="weaviate",
                metadata={"method": "mock"},
            ) for i in range(count)
        ]

    async def neo4j_search(
        self,
        query: str,
        features: QueryFeatures,
        filters: Optional[Dict[str, Any]] = None
    ) -> List[SearchResult]:
        """
        Perform a graph-based search in Neo4j.
        """
        try:
            if features.entity_mentions:
                return await self.entity_graph_search(query, features.entity_mentions, filters)
            else:
                return await self.general_graph_search(query, features, filters)
        except Exception as e:
            self.logger.error(f"Neo4j search error: {e}", exc_info=True)
            return self._get_mock_neo4j_results(query, [], 3)

    async def entity_graph_search(
        self,
        query: str,
        entities: List[str],
        filters: Optional[Dict[str, Any]] = None
    ) -> List[SearchResult]:
        """
        Search for specific entities mentioned in Memory nodes.
        For SwAIvyn: Search memories that mention specific entities (no userID filtering).
        """
        if not self.neo4j:
            self.logger.warning("Neo4j driver not available, returning mock data")
            return self._get_mock_neo4j_results(query, entities, 3)

        try:
            # Call the backend's vector search API which we know is working
            import urllib.request
            import urllib.parse

            try:
                encoded_query = urllib.parse.quote(query)
                url = f"http://localhost:5000/api/brain/search?query={encoded_query}"

                with urllib.request.urlopen(url) as response:
                    if response.status == 200:
                        import json
                        backend_results = json.loads(response.read().decode())

                        results: List[SearchResult] = []
                        for i, result in enumerate(backend_results):
                            memory_id = result.get("id", f"memory_{i}")
                            content = result.get("metadata", {}).get("content", "") or ""
                            category = result.get("metadata", {}).get("category", "Personal") or "Personal"
                            user_id = result.get("metadata", {}).get("userId", "") or ""
                            score = float(result.get("score", 0.5))

                            results.append(
                                SearchResult(
                                    id=memory_id,
                                    title=f"Memory: {category} (Entity Match)",
                                    content=content[:500] + "..." if len(content) > 500 else content,
                                    score=score,
                                    source="neo4j",
                                    metadata={
                                        "entity_type": "memory",
                                        "category": category,
                                        "user_id": user_id,
                                        "search_type": "vector_memory_search",
                                    },
                                )
                            )

                        self.logger.info(f"Neo4j vector search via backend API returned {len(results)} results")
                        return results
                    else:
                        self.logger.warning(f"Backend vector search API returned {response.status}")

            except Exception as api_error:
                self.logger.warning(f"Failed to call backend vector search API: {api_error}")

            # Fallback to text-based search if API call fails
            cypher = """
            MATCH (m:Memory)
            WHERE ANY(entity IN $entity_list WHERE toLower(m.text) CONTAINS toLower(entity))
            RETURN m.id AS id,
                   m.text AS content,
                   m.category AS category,
                   m.userId AS userId,
                   m.createdAt AS createdAt,
                   m.isShared AS isShared
            ORDER BY m.createdAt DESC
            LIMIT 50
            """
            async with self.neo4j.session() as session:
                result = await session.run(cypher, entity_list=entities)
                records = await result.data()

            results: List[SearchResult] = []
            for i, record in enumerate(records):
                memory_id = record.get("id", f"memory_entity_{i}")
                content = record.get("content", "") or ""
                category = record.get("category", "Personal") or "Personal"
                user_id = record.get("userId", "") or ""
                created_at = record.get("createdAt", "") or ""
                is_shared = record.get("isShared", "False") or "False"

                # Calculate score based on entity matches
                entity_matches = sum(
                    1 for entity in entities if entity.lower() in content.lower()
                )
                base_score = 0.8 + (entity_matches * 0.1)  # Higher base score for entity matches
                score = min(1.0, base_score)

                results.append(
                    SearchResult(
                        id=memory_id,
                        title=f"Memory: {category} (Entity Match)",
                        content=content[:500] + "..." if len(content) > 500 else content,
                        score=score,
                        source="neo4j",
                        metadata={
                            "entity_type": "memory",
                            "category": category,
                            "user_id": user_id,
                            "created_at": created_at,
                            "is_shared": is_shared,
                            "entity_matches": entity_matches,
                            "matched_entities": [e for e in entities if e.lower() in content.lower()],
                            "search_type": "entity_memory_search",
                        },
                    )
                )

            self.logger.info(f"Neo4j entity search returned {len(results)} results")
            return results

        except Exception as e:
            self.logger.error(f"Neo4j entity_graph_search error: {e}", exc_info=True)
            return self._get_mock_neo4j_results(query, entities, 3)

    async def general_graph_search(
        self,
        query: str,
        features: QueryFeatures,
        filters: Optional[Dict[str, Any]] = None
    ) -> List[SearchResult]:
        """
        Vector-based memory search using Neo4j vector index.
        For SwAIvyn: Memories are global (no userID filtering) since it's a single-user app.
        """
        if not self.neo4j:
            self.logger.warning("Neo4j driver not available, returning mock data")
            return self._get_mock_neo4j_results(query, [], 2)

        try:
            # Call the backend's vector search API which we know is working
            import urllib.request
            import urllib.parse

            try:
                encoded_query = urllib.parse.quote(query)
                url = f"http://localhost:5000/api/brain/search?query={encoded_query}"

                with urllib.request.urlopen(url) as response:
                    if response.status == 200:
                        import json
                        backend_results = json.loads(response.read().decode())

                        results: List[SearchResult] = []
                        for i, result in enumerate(backend_results):
                            memory_id = result.get("id", f"memory_{i}")
                            content = result.get("metadata", {}).get("content", "") or ""
                            category = result.get("metadata", {}).get("category", "Personal") or "Personal"
                            user_id = result.get("metadata", {}).get("userId", "") or ""
                            score = float(result.get("score", 0.5))

                            results.append(
                                SearchResult(
                                    id=memory_id,
                                    title=f"Memory: {category}",
                                    content=content[:500] + "..." if len(content) > 500 else content,
                                    score=score,
                                    source="neo4j",
                                    metadata={
                                        "entity_type": "memory",
                                        "category": category,
                                        "user_id": user_id,
                                        "search_type": "vector_memory_search",
                                    },
                                )
                            )

                        self.logger.info(f"Neo4j vector search via backend API returned {len(results)} results")
                        return results
                    else:
                        self.logger.warning(f"Backend vector search API returned {response.status}")

            except Exception as api_error:
                self.logger.warning(f"Failed to call backend vector search API: {api_error}")

            # Fallback to text-based search if API call fails
            fallback_cypher = """
            MATCH (m:Memory)
            WHERE toLower(m.text) CONTAINS toLower($query)
               OR ANY(k IN $keywords WHERE toLower(m.text) CONTAINS toLower(k))
            RETURN m.id AS id,
                   m.text AS content,
                   m.category AS category,
                   m.userId AS userId,
                   m.createdAt AS createdAt,
                   m.isShared AS isShared
            ORDER BY m.createdAt DESC
            LIMIT 50
            """

            keywords = features.keywords or [query.lower()]
            async with self.neo4j.session() as session:
                result = await session.run(fallback_cypher, query=query, keywords=keywords)
                records = await result.data()

            results: List[SearchResult] = []
            for i, record in enumerate(records):
                memory_id = record.get("id", f"memory_{i}")
                content = record.get("content", "") or ""
                category = record.get("category", "Personal") or "Personal"
                user_id = record.get("userId", "") or ""
                created_at = record.get("createdAt", "") or ""
                is_shared = record.get("isShared", "False") or "False"

                # Calculate score based on keyword matches
                keyword_matches = sum(1 for kw in keywords if kw.lower() in content.lower())
                base_score = 0.7 + (keyword_matches * 0.1)
                if query.lower() in content.lower():
                    base_score += 0.2  # Boost for exact query match
                score = min(1.0, base_score)

                results.append(
                    SearchResult(
                        id=memory_id,
                        title=f"Memory: {category}",
                        content=content[:500] + "..." if len(content) > 500 else content,
                        score=score,
                        source="neo4j",
                        metadata={
                            "entity_type": "memory",
                            "category": category,
                            "user_id": user_id,
                            "created_at": created_at,
                            "is_shared": is_shared,
                            "keyword_matches": keyword_matches,
                            "search_type": "memory_search",
                        },
                    )
                )

            self.logger.info(f"Neo4j general search returned {len(results)} results")
            return results

        except Exception as e:
            self.logger.error(f"Neo4j general_graph_search error: {e}", exc_info=True)
            return self._get_mock_neo4j_results(query, [], 2)

    def _get_mock_neo4j_results(self, query: str, entities: List[str], count: int) -> List[SearchResult]:
        """Fallback mock Neo4j results."""
        return [
            SearchResult(
                id=f"neo4j_mock_{i}",
                title=f"MockGraph {i}",
                content=f"Mock graph knowledge about '{query}' and entities {entities}",
                score=0.8 - (i * 0.1),
                source="neo4j",
                metadata={"method": "mock"},
            ) for i in range(count)
        ]

    def normalize_scores(self, db_results: List[Tuple[str, List[SearchResult]]]) -> List[SearchResult]:
        """Normalize scores across different sources to a 0-1 range, then apply weights."""
        all_results: List[SearchResult] = []

        for source_name, results in db_results:
            if not results:
                continue
            scores = [r.score for r in results]
            if not scores:
                continue
            max_score = max(scores)
            min_score = min(scores)
            score_range = max_score - min_score

            for r in results:
                if score_range > 0:
                    normalized = (r.score - min_score) / score_range
                else:
                    normalized = 1.0
                r.normalized_score = normalized * self.db_weights.get(source_name, 1.0)
                all_results.append(r)

        return all_results

    def reciprocal_rank_fusion(
        self,
        results: List[SearchResult],
        k: int = 60
    ) -> List[SearchResult]:
        """
        Combine results from each source using Reciprocal Rank Fusion (RRF).
        """
        db_grouped: Dict[str, List[SearchResult]] = defaultdict(list)
        for r in results:
            db_grouped[r.source].append(r)

        # Sort each group by normalized_score descending
        for source, group in db_grouped.items():
            group.sort(key=lambda x: x.normalized_score or 0, reverse=True)

        # Compute RRF scores
        fused_scores: Dict[str, float] = {}
        fused_map: Dict[str, SearchResult] = {}

        for source, group in db_grouped.items():
            for rank, r in enumerate(group):
                rrf_score = 1.0 / (k + rank + 1)
                fused_scores[r.id] = fused_scores.get(r.id, 0.0) + rrf_score
                fused_map[r.id] = r

        # Sort final IDs by fused score descending
        sorted_ids = sorted(fused_scores.items(), key=lambda x: x[1], reverse=True)
        final_list: List[SearchResult] = []
        for rid, score in sorted_ids:
            r = fused_map[rid]
            r.score = score
            final_list.append(r)

        return final_list

    async def cross_encoder_rerank(
        self,
        query: str,
        results: List[SearchResult]
    ) -> List[SearchResult]:
        """
        Placeholder for cross-encoder re-ranking. Currently returns results as-is.
        """
        return results


# ---------- FastAPI setup ----------

app = FastAPI()
search_engine: Optional[HybridSearchEngine] = None

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.on_event("startup")
async def startup_event():
    logger.info("FastAPI startup event triggered.")
    global search_engine

    current_dir = os.getcwd()
    sql_db_path = os.path.join(current_dir, "..", "backend", "Sqldatabase", "swai-vyn.db")
    sql_conn_str = f"file:{sql_db_path}?mode=rw"  # Use "?mode=rwc" if DB may not exist

    weaviate_url = "http://stabled:8080"
    neo4j_uri = "bolt://localhost:7687"
    neo4j_user = "neo4j"
    neo4j_password = "password"  # Replace with real password or read from ENV

    search_engine = HybridSearchEngine(
        sql_conn_str,
        weaviate_url,
        neo4j_uri,
        neo4j_user,
        neo4j_password
    )
    await search_engine.connect()
    logger.info("FastAPI startup event completed.")

@app.on_event("shutdown")
async def shutdown_event():
    logger.info("FastAPI shutdown event triggered.")
    if search_engine:
        await search_engine.disconnect()
    logger.info("FastAPI shutdown event completed.")

@app.post("/search")
async def perform_search(request: Request):
    logger.info("API /search endpoint called.")
    if not search_engine:
        logger.error("Search engine not initialized.")
        raise HTTPException(status_code=503, detail="Search engine not initialized.")

    try:
        body = await request.json()
        query = body.get("query")
        user_id = body.get("userId")
        top_k = int(body.get("topK", 20))

        if not query:
            raise HTTPException(status_code=400, detail="Missing 'query' parameter")

        filters = {"userId": user_id} if user_id else None
        results = await search_engine.search(query, top_k, filters)
        return JSONResponse(content=json.loads(json.dumps([r.__dict__ for r in results])))

    except Exception as e:
        logger.error(f"API /search error: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Search failed: {e}")

@app.get("/health")
async def health_check():
    logger.info("API /health endpoint called.")
    if search_engine:
        return {
            "status": "ok",
            "databases": {
                "sqlite": "Connected" if search_engine.sql else "Disconnected",
                "weaviate": "Connected" if search_engine.weaviate else "Disconnected",
                "neo4j": "Connected" if search_engine.neo4j else "Disconnected"
            }
        }
    else:
        logger.warning("Search engine not initialized during health check.")
        return {"status": "initializing", "databases": {}}

if __name__ == "__main__":
    logger.info("Running uvicorn...")
    uvicorn.run(app, host="0.0.0.0", port=8001)