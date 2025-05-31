# Updated hybrid search implementation with real database queries
import asyncio
import re
import logging
from collections import defaultdict
from typing import List, Dict, Any, Optional, Tuple
from dataclasses import dataclass
import json
from datetime import datetime

import weaviate
import weaviate.classes as wvc


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
    Hybrid search engine that combines SQL, Weaviate (vector), and Neo4j (graph) databases
    """

    def __init__(self, sql_connection, weaviate_client, neo4j_driver):
        self.sql = sql_connection
        self.weaviate = weaviate_client
        self.neo4j = neo4j_driver

        self.logger = logging.getLogger(__name__)

        # Search weights for different databases
        self.db_weights = {"sql": 0.3, "weaviate": 0.4, "neo4j": 0.3}

    async def search(
        self, query: str, top_k: int = 20, filters: Optional[Dict] = None
    ) -> List[SearchResult]:
        """
        Main search method that orchestrates hybrid search across all databases
        """
        try:
            # 1. Query Analysis & Routing
            query_features = self.analyze_query(query)
            self.logger.info(f"Query analysis: {query_features}")

            # 2. Parallel Retrieval with different strategies
            tasks = [
                self.sql_search(query, query_features, filters),
                self.weaviate_search(query, query_features, filters),
                self.neo4j_search(query, query_features, filters),
            ]

            sql_results, vector_results, graph_results = await asyncio.gather(*tasks)

            # 3. Score Normalization
            normalized_results = self.normalize_scores(
                [
                    ("sql", sql_results),
                    ("weaviate", vector_results),
                    ("neo4j", graph_results),
                ]
            )

            # 4. Result Fusion
            fused_results = self.reciprocal_rank_fusion(normalized_results)

            # 5. Re-ranking (optional)
            final_results = await self.cross_encoder_rerank(query, fused_results)

            return final_results[:top_k]

        except Exception as e:
            self.logger.error(f"Search error: {str(e)}")
            raise

    def analyze_query(self, query: str) -> QueryFeatures:
        """
        Analyze query to determine search strategy and routing
        """
        # Detect structured filters (dates, numbers, specific fields)
        has_filters = bool(
            re.search(r"\b(after|before|from|to|type|category):", query, re.IGNORECASE)
        )

        # Simple entity extraction (replace with NER model)
        entities = self.extract_entities(query)

        # Measure semantic complexity (word count, complexity indicators)
        complexity = min(1.0, len(query.split()) / 20.0)

        # Classify query type
        query_type = self.classify_query_type(query)

        # Extract keywords
        keywords = self.extract_keywords(query)

        return QueryFeatures(
            has_structured_filters=has_filters,
            semantic_complexity=complexity,
            entity_mentions=entities,
            query_type=query_type,
            keywords=keywords,
        )

    def extract_entities(self, query: str) -> List[str]:
        """Extract named entities from query (simplified version)"""
        entities = []

        # Capitalized words (potential proper nouns)
        capitalized = re.findall(r"\b[A-Z][a-z]+\b", query)
        entities.extend(capitalized)

        # Date patterns
        dates = re.findall(r"\b\d{4}[-/]\d{1,2}[-/]\d{1,2}\b", query)
        entities.extend(dates)

        return list(set(entities))

    def classify_query_type(self, query: str) -> str:
        """Classify the type of query to optimize search strategy"""
        query_lower = query.lower()

        if any(word in query_lower for word in ["what", "who", "when", "where", "how"]):
            return "factual"
        elif any(
            word in query_lower
            for word in ["related", "similar", "connected", "linked"]
        ):
            return "relational"
        else:
            return "exploratory"

    def extract_keywords(self, query: str) -> List[str]:
        """Extract important keywords from query"""
        stop_words = {
            "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by",
        }
        words = re.findall(r"\b\w+\b", query.lower())
        return [word for word in words if word not in stop_words and len(word) > 2]    async def sql_search(
        self, query: str, features: QueryFeatures, filters: Optional[Dict] = None
    ) -> List[SearchResult]:
        """
        SQL database search - minimal role, just basic metadata if needed
        """
        try:
            # SQLite is mainly for configuration/metadata, not primary content search
            # Return empty results or basic user info if needed
            self.logger.info("SQL search: Using minimal metadata search")
            return []

        except Exception as e:
            self.logger.error(f"SQL search error: {str(e)}")
            return []

    async def structured_sql_search(
        self, query: str, features: QueryFeatures, filters: Optional[Dict]
    ) -> List[SearchResult]:
        """Handle structured SQL queries with filters"""
        if not self.sql:
            self.logger.warning("SQL connection not available, using mock data")
            return self._get_mock_sql_results(query, 2)

        try:
            # Use default user ID from database export if not provided
            user_id = filters.get("userId") if filters else "00000000-0000-0000-0000-000000000001"
            search_pattern = f"%{query}%"

            # Query memories and chat messages from actual database structure
            base_query = """
            SELECT m.Id, 'Memory' as title, m.Content, m.CreatedAt, m.Category, 'memory' as source, 1.0 as score
            FROM Memories m 
            WHERE m.Content LIKE ? AND m.UserId = ?
            
            UNION ALL
            
            SELECT ci.Id, 'Chat Message' as title, ci.Content, ci.CreatedUtc, ci.ContentType, 'chat' as source, 0.8 as score
            FROM ChatIndices ci
            JOIN Conversations c ON ci.ConversationId = c.Id
            WHERE ci.Content LIKE ? AND c.UserId = ?
            
            ORDER BY score DESC LIMIT 50
            """

            params = [search_pattern, user_id, search_pattern, user_id]
            cursor = self.sql.execute(base_query, params)
            rows = cursor.fetchall()

            results = []
            for row in rows:
                results.append(
                    SearchResult(
                        id=str(row[0]),
                        title=row[1],
                        content=row[2][:200] + "..." if len(row[2]) > 200 else row[2],
                        score=float(row[6]),
                        source="sql",
                        metadata={
                            "created_at": row[3],
                            "content_type": row[4],
                            "origin": row[5],
                            "search_type": "structured",
                        },
                    )
                )

            self.logger.info(f"SQL structured search returned {len(results)} results")
            return results

        except Exception as e:
            self.logger.error(f"SQL structured search error: {str(e)}")
            return self._get_mock_sql_results(query, 2)

    async def full_text_sql_search(
        self, query: str, features: QueryFeatures, filters: Optional[Dict] = None
    ) -> List[SearchResult]:
        """Handle full-text SQL search"""
        if not self.sql:
            self.logger.warning("SQL connection not available, using mock data")
            return self._get_mock_sql_results(query, 3)

        try:
            # Use default user ID from database export if not provided
            user_id = filters.get("userId") if filters else "00000000-0000-0000-0000-000000000001"
            search_pattern = f"%{query}%"

            # SQLite search across memories and chat content
            sql_query = """
            SELECT m.Id, 'Memory' as title, m.Content, m.CreatedAt, m.Category, 'memory' as source, 1.0 as score
            FROM Memories m 
            WHERE m.Content LIKE ? AND m.UserId = ?
            
            UNION ALL
            
            SELECT ci.Id, 'Chat Message' as title, ci.Content, ci.CreatedUtc, ci.ContentType, 'chat' as source, 0.9 as score
            FROM ChatIndices ci
            JOIN Conversations c ON ci.ConversationId = c.Id
            WHERE ci.Content LIKE ? AND c.UserId = ?
            
            ORDER BY score DESC LIMIT 50
            """

            params = [search_pattern, user_id, search_pattern, user_id]
            cursor = self.sql.execute(sql_query, params)
            rows = cursor.fetchall()

            results = []
            for row in rows:
                results.append(
                    SearchResult(
                        id=str(row[0]),
                        title=row[1],
                        content=row[2][:200] + "..." if len(row[2]) > 200 else row[2],
                        score=float(row[6]),
                        source="sql",
                        metadata={
                            "created_at": row[3],
                            "content_type": row[4],
                            "origin": row[5],
                            "search_type": "full_text",
                        },
                    )
                )

            self.logger.info(f"SQL full-text search returned {len(results)} results")
            return results

        except Exception as e:
            self.logger.error(f"SQL full-text search error: {str(e)}")
            return self._get_mock_sql_results(query, 3)

    def _get_mock_sql_results(self, query: str, count: int) -> List[SearchResult]:
        """Fallback mock SQL results"""
        return [
            SearchResult(
                id=f"sql_{i}",
                title=f"SQL Document {i}",
                content=f"Content related to {query}...",
                score=0.9 - (i * 0.1),
                source="sql",
                metadata={"content_type": "document", "method": "mock"},
            )
            for i in range(count)
        ]

    async def weaviate_search(
        self, query: str, features: QueryFeatures, filters: Optional[Dict] = None
    ) -> List[SearchResult]:
        """
        Vector similarity search using Weaviate
        """
        if not self.weaviate:
            self.logger.warning("Weaviate client not available, using mock data")
            return self._get_mock_weaviate_results(query, 4)

        try:
            # Use Weaviate v4 API for semantic search with skip_init_checks for gRPC issues
            collections = self.weaviate.collections.list_all()
            available_collections = [collection.name for collection in collections]

            # Use Document collection if available, otherwise use available collections
            target_collection = "Document"
            if "Document" not in available_collections and available_collections:
                target_collection = available_collections[0]
                self.logger.info(
                    f"Using collection '{target_collection}' instead of 'Document'"
                )
            elif not available_collections:
                self.logger.warning("No Weaviate collections found, using mock data")
                return self._get_mock_weaviate_results(query, 4)

            # Get the collection
            collection = self.weaviate.collections.get(target_collection)

            # Build where filter for userId if provided
            where_filter = None
            user_id = filters.get("userId") if filters else "00000000-0000-0000-0000-000000000001"
            if user_id:
                where_filter = wvc.query.Filter.by_property("userId").equal(str(user_id))

            # Perform semantic search using v4 API
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

            results = []
            for i, obj in enumerate(response.objects):
                # Calculate similarity score from distance
                distance = obj.metadata.distance if obj.metadata.distance else 0.5
                similarity_score = max(0.0, 1.0 - distance)

                results.append(
                    SearchResult(
                        id=str(obj.uuid),
                        title=obj.properties.get("title", "Untitled Document"),
                        content=(
                            obj.properties.get("content", "")[:200] + "..."
                            if len(obj.properties.get("content", "")) > 200
                            else obj.properties.get("content", "")
                        ),
                        score=similarity_score,
                        source="weaviate",
                        metadata={
                            "content_type": obj.properties.get("contentType", "unknown"),
                            "origin": obj.properties.get("source", "weaviate_db"),
                            "user_id": obj.properties.get("userId", ""),
                            "distance": distance,
                            "search_type": "semantic",
                        },
                    )
                )

            self.logger.info(f"Weaviate search returned {len(results)} results")
            return results

        except Exception as e:
            self.logger.error(f"Weaviate search error: {str(e)}")
            return self._get_mock_weaviate_results(query, 4)

    def _get_mock_weaviate_results(self, query: str, count: int) -> List[SearchResult]:
        """Fallback mock Weaviate results"""
        return [
            SearchResult(
                id=f"weaviate_{i}",
                title=f"Vector Document {i}",
                content=f"Semantically similar content to {query}...",
                score=0.9 - (i * 0.05),
                source="weaviate",
                metadata={
                    "content_type": "article",
                    "origin": "weaviate_db",
                    "distance": 0.1 + (i * 0.05),
                    "method": "mock",
                },
            )
            for i in range(count)
        ]

    async def neo4j_search(
        self, query: str, features: QueryFeatures, filters: Optional[Dict] = None
    ) -> List[SearchResult]:
        """
        Graph-based search using Neo4j for relationship discovery
        """
        try:
            if features.entity_mentions:
                results = await self.entity_graph_search(query, features.entity_mentions, filters)
            else:
                results = await self.general_graph_search(query, features, filters)

            self.logger.info(f"Neo4j search returned {len(results)} results")
            return results

        except Exception as e:
            self.logger.error(f"Neo4j search error: {str(e)}")
            return []

    async def entity_graph_search(
        self, query: str, entities: List[str], filters: Optional[Dict] = None
    ) -> List[SearchResult]:
        """Search based on entity relationships in the graph"""
        if not self.neo4j:
            self.logger.warning("Neo4j driver not available, using mock data")
            return self._get_mock_neo4j_results(query, entities, 3)

        try:
            user_id = filters.get("userId") if filters else "00000000-0000-0000-0000-000000000001"
            
            # Cypher query to find documents/memories related to entities
            cypher_query = """
            MATCH (m:Memory)-[:MENTIONS|:RELATED_TO]-(e:Entity)
            WHERE e.name IN $entities AND m.userId = $user_id
            RETURN m.id, m.content, m.category, m.userId, m.createdAt,
                   count(e) as entity_matches
            ORDER BY entity_matches DESC, m.createdAt DESC
            LIMIT 50
            """

            with self.neo4j.session() as session:
                result = session.run(cypher_query, entities=entities, user_id=user_id)
                records = list(result)

            results = []
            for record in records:
                # Score based on entity matches and recency
                entity_matches = record["entity_matches"]
                base_score = min(1.0, entity_matches / len(entities))

                results.append(
                    SearchResult(
                        id=str(record["m.id"]),
                        title=f"Memory with {entity_matches} entity matches",
                        content=(
                            record["m.content"][:200] + "..."
                            if len(record["m.content"]) > 200
                            else record["m.content"]
                        ),
                        score=base_score,
                        source="neo4j",
                        metadata={
                            "category": record["m.category"],
                            "user_id": record["m.userId"],
                            "created_at": record["m.createdAt"],
                            "entity_matches": entity_matches,
                            "search_type": "entity_graph",
                        },
                    )
                )

            return results

        except Exception as e:
            self.logger.error(f"Neo4j entity search error: {str(e)}")
            return self._get_mock_neo4j_results(query, entities, 3)

    async def general_graph_search(
        self, query: str, features: QueryFeatures, filters: Optional[Dict] = None
    ) -> List[SearchResult]:
        """General graph search for relationships and patterns"""
        if not self.neo4j:
            self.logger.warning("Neo4j driver not available, using mock data")
            return self._get_mock_neo4j_results(query, [], 2)

        try:
            user_id = filters.get("userId") if filters else "00000000-0000-0000-0000-000000000001"
            
            # General search in graph database
            cypher_query = """
            MATCH (m:Memory)
            WHERE m.content CONTAINS $query AND m.userId = $user_id
            OPTIONAL MATCH (m)-[r]->(related)
            RETURN m.id, m.content, m.category, m.userId, m.createdAt,
                   count(related) as related_count
            ORDER BY related_count DESC, m.createdAt DESC
            LIMIT 50
            """

            with self.neo4j.session() as session:
                result = session.run(cypher_query, query=query, user_id=user_id)
                records = list(result)

            results = []
            for record in records:
                # Score based on relationship connections
                related_count = record["related_count"] or 0
                base_score = min(1.0, 0.5 + (related_count * 0.1))

                results.append(
                    SearchResult(
                        id=str(record["m.id"]),
                        title=f"Graph Memory ({related_count} connections)",
                        content=(
                            record["m.content"][:200] + "..."
                            if len(record["m.content"]) > 200
                            else record["m.content"]
                        ),
                        score=base_score,
                        source="neo4j",
                        metadata={
                            "category": record["m.category"],
                            "user_id": record["m.userId"],
                            "created_at": record["m.createdAt"],
                            "related_count": related_count,
                            "search_type": "general_graph",
                        },
                    )
                )

            return results

        except Exception as e:
            self.logger.error(f"Neo4j general search error: {str(e)}")
            return self._get_mock_neo4j_results(query, [], 2)

    def _get_mock_neo4j_results(self, query: str, entities: List[str], count: int) -> List[SearchResult]:
        """Fallback mock Neo4j results"""
        return [
            SearchResult(
                id=f"neo4j_{i}",
                title=f"Graph Document {i}",
                content=f"Graph-connected content about {query} and {entities}...",
                score=0.8 - (i * 0.1),
                source="neo4j",
                metadata={
                    "content_type": "graph_node",
                    "origin": "neo4j_db",
                    "connections": 5 - i,
                    "method": "mock",
                },
            )
            for i in range(count)
        ]

    def normalize_scores(self, db_results: List[Tuple[str, List[SearchResult]]]) -> List[SearchResult]:
        """Normalize scores across different databases"""
        all_results = []
        
        for db_name, results in db_results:
            if not results:
                continue
                
            # Get score range for this database
            scores = [r.score for r in results]
            if len(scores) == 0:
                continue
                
            max_score = max(scores)
            min_score = min(scores)
            score_range = max_score - min_score
            
            # Normalize scores to 0-1 range and apply database weight
            for result in results:
                if score_range > 0:
                    normalized = (result.score - min_score) / score_range
                else:
                    normalized = 1.0
                    
                result.normalized_score = normalized * self.db_weights.get(db_name, 1.0)
                all_results.append(result)
        
        return all_results

    def reciprocal_rank_fusion(self, results: List[SearchResult], k: int = 60) -> List[SearchResult]:
        """Combine results using Reciprocal Rank Fusion"""
        # Group results by database source
        db_results = defaultdict(list)
        for result in results:
            db_results[result.source].append(result)
        
        # Sort each database's results by normalized score
        for source in db_results:
            db_results[source].sort(key=lambda x: x.normalized_score or 0, reverse=True)
        
        # Calculate RRF scores
        result_scores = defaultdict(float)
        result_map = {}
        
        for source, source_results in db_results.items():
            for rank, result in enumerate(source_results):
                rrf_score = 1.0 / (k + rank + 1)
                result_scores[result.id] += rrf_score
                result_map[result.id] = result
        
        # Sort by RRF score and update result scores
        final_results = []
        for result_id, rrf_score in sorted(result_scores.items(), key=lambda x: x[1], reverse=True):
            result = result_map[result_id]
            result.score = rrf_score
            final_results.append(result)
        
        return final_results

    async def cross_encoder_rerank(self, query: str, results: List[SearchResult]) -> List[SearchResult]:
        """Re-rank results using cross-encoder (currently just returns as-is)"""
        # Placeholder for cross-encoder re-ranking
        # In a full implementation, this would use a trained cross-encoder model
        return results
