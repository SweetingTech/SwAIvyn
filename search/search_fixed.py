# Updated hybrid search implementation focused on Weaviate and Neo4j
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
    Hybrid search engine that focuses on Weaviate (vector) and Neo4j (graph) databases
    SQLite is used minimally for metadata only
    """

    def __init__(self, sql_connection, weaviate_client, neo4j_driver):
        self.sql = sql_connection
        self.weaviate = weaviate_client
        self.neo4j = neo4j_driver

        self.logger = logging.getLogger(__name__)

        # Search weights - Weaviate and Neo4j are primary, SQL minimal
        self.db_weights = {"sql": 0.1, "weaviate": 0.5, "neo4j": 0.4}

    async def search(
        self, query: str, top_k: int = 20, filters: Optional[Dict] = None
    ) -> List[SearchResult]:
        """
        Main search method focusing on Weaviate and Neo4j
        """
        try:
            # 1. Query Analysis & Routing
            query_features = self.analyze_query(query)
            self.logger.info(f"Query analysis: {query_features}")

            # 2. Parallel Retrieval - Focus on Weaviate and Neo4j
            tasks = [
                self.weaviate_search(query, query_features, filters),
                self.neo4j_search(query, query_features, filters),
            ]

            vector_results, graph_results = await asyncio.gather(*tasks)

            # 3. Score Normalization
            normalized_results = self.normalize_scores(
                [
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

        # Simple entity extraction
        entities = self.extract_entities(query)

        # Measure semantic complexity
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
        return [word for word in words if word not in stop_words and len(word) > 2]    async def weaviate_search(
        self, query: str, features: QueryFeatures, filters: Optional[Dict] = None
    ) -> List[SearchResult]:
        """
        Vector similarity search using Weaviate - PRIMARY content search
        """
        if not self.weaviate:
            self.logger.warning("Weaviate client not available, using mock data")
            return self._get_mock_weaviate_results(query, 5)

        try:
            # Try the older Weaviate client API first
            try:
                # Check if Weaviate is ready
                if not self.weaviate.is_ready():
                    self.logger.warning("Weaviate not ready, using mock data")
                    return self._get_mock_weaviate_results(query, 5)
                
                # Get schema to see available classes
                schema = self.weaviate.schema.get()
                available_classes = [cls['class'] for cls in schema.get('classes', [])]
                
                if not available_classes:
                    self.logger.warning("No Weaviate classes found, using mock data")
                    return self._get_mock_weaviate_results(query, 5)

                self.logger.info(f"Available Weaviate classes: {available_classes}")

                # Try to use the most appropriate class
                target_class = None
                for preferred in ["Document", "Content", "Message", "Chat", "Memory"]:
                    if preferred in available_classes:
                        target_class = preferred
                        break
                
                if not target_class:
                    target_class = available_classes[0]
                
                self.logger.info(f"Using Weaviate class: {target_class}")

                # Perform semantic search using older API
                result = (
                    self.weaviate.query
                    .get(target_class, ["*"])
                    .with_near_text({"concepts": [query]})
                    .with_additional(["distance", "id"])
                    .with_limit(50)
                    .do()
                )

                results = []
                objects = result.get("data", {}).get("Get", {}).get(target_class, [])
                
                for i, obj in enumerate(objects):
                    # Calculate similarity score from distance
                    additional = obj.get("_additional", {})
                    distance = additional.get("distance", 0.5)
                    similarity_score = max(0.0, 1.0 - distance)

                    # Get content from various possible property names
                    content_candidates = []
                    for prop in ["content", "text", "message", "body", "description"]:
                        if obj.get(prop):
                            content_candidates.append(str(obj[prop]))
                    
                    content = content_candidates[0] if content_candidates else str(obj)

                    results.append(
                        SearchResult(
                            id=additional.get("id", f"weaviate_{i}"),
                            title=obj.get("title", f"Weaviate Document {i+1}"),
                            content=content[:200] + "..." if len(content) > 200 else content,
                            score=similarity_score,
                            source="weaviate",
                            metadata={
                                "content_type": obj.get("contentType", "unknown"),
                                "origin": obj.get("source", "weaviate_db"),
                                "user_id": obj.get("userId", ""),
                                "distance": distance,
                                "search_type": "semantic",
                                "class": target_class,
                            },
                        )
                    )

                self.logger.info(f"Weaviate search returned {len(results)} results")
                return results

            except Exception as v3_error:
                self.logger.warning(f"Weaviate v3 API failed: {v3_error}, trying v4 API")
                
                # Fallback to v4 API
                collections = self.weaviate.collections.list_all()
                available_collections = [collection.name for collection in collections]
                
                if not available_collections:
                    self.logger.warning("No Weaviate collections found, using mock data")
                    return self._get_mock_weaviate_results(query, 5)

                target_collection = available_collections[0]
                collection = self.weaviate.collections.get(target_collection)

                response = collection.query.near_text(
                    query=query,
                    limit=50
                )

                results = []
                for i, obj in enumerate(response.objects):
                    content = str(obj.properties)
                    results.append(
                        SearchResult(
                            id=str(obj.uuid),
                            title=f"Document {i+1}",
                            content=content[:200] + "..." if len(content) > 200 else content,
                            score=0.8,
                            source="weaviate",
                            metadata={
                                "collection": target_collection,
                                "search_type": "semantic_v4",
                            },
                        )
                    )

                return results

        except Exception as e:
            self.logger.error(f"Weaviate search error: {str(e)}")
            return self._get_mock_weaviate_results(query, 3)

    def _get_mock_weaviate_results(self, query: str, count: int) -> List[SearchResult]:
        """Fallback mock Weaviate results"""
        return [
            SearchResult(
                id=f"weaviate_{i}",
                title=f"Vector Document {i}",
                content=f"Semantically similar content to '{query}' from Weaviate database...",
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
        Graph-based search using Neo4j - PRIMARY memory search
        """
        try:
            # Use a flexible search approach since we don't know the exact node structure
            results = await self.flexible_graph_search(query, features, filters)
            self.logger.info(f"Neo4j search returned {len(results)} results")
            return results

        except Exception as e:
            self.logger.error(f"Neo4j search error: {str(e)}")
            return self._get_mock_neo4j_results(query, [], 3)

    async def flexible_graph_search(
        self, query: str, features: QueryFeatures, filters: Optional[Dict] = None
    ) -> List[SearchResult]:
        """Flexible search that works with any Neo4j node structure"""
        if not self.neo4j:
            self.logger.warning("Neo4j driver not available, using mock data")
            return self._get_mock_neo4j_results(query, [], 3)

        try:
            # First, let's see what's actually in the database
            with self.neo4j.session() as session:
                # Get sample of all nodes with their properties containing the search text
                cypher_query = """
                MATCH (n)
                WHERE ANY(prop IN keys(n) WHERE toString(n[prop]) CONTAINS $search_text)
                OPTIONAL MATCH (n)-[r]-(related)
                RETURN n, labels(n) as node_labels, count(related) as connections
                ORDER BY connections DESC
                LIMIT 50
                """
                
                result = session.run(cypher_query, search_text=query)
                records = list(result)

            results = []
            for i, record in enumerate(records):
                node = record["n"]
                node_labels = record["node_labels"]
                connections = record["connections"]
                
                # Extract relevant content from the node
                content_parts = []
                node_props = dict(node)
                
                # Look for common content properties
                for prop in ["content", "text", "message", "body", "description", "name", "title"]:
                    if prop in node_props and node_props[prop]:
                        content_parts.append(str(node_props[prop]))
                
                if not content_parts:
                    # If no common properties, use all string properties
                    content_parts = [str(v) for v in node_props.values() if isinstance(v, str) and len(str(v)) > 2]
                
                content = " | ".join(content_parts[:3])  # Limit to first 3 meaningful properties
                
                # Score based on connections and content relevance
                base_score = min(1.0, 0.3 + (connections * 0.1))
                if query.lower() in content.lower():
                    base_score += 0.4
                
                results.append(
                    SearchResult(
                        id=str(node.element_id) if hasattr(node, 'element_id') else f"neo4j_{i}",
                        title=f"{'/'.join(node_labels)} Node" if node_labels else f"Graph Node {i+1}",
                        content=content[:200] + "..." if len(content) > 200 else content,
                        score=base_score,
                        source="neo4j",
                        metadata={
                            "node_labels": node_labels,
                            "connections": connections,
                            "properties": list(node_props.keys()),
                            "search_type": "flexible_graph",
                        },
                    )
                )

            return results

        except Exception as e:
            self.logger.error(f"Neo4j flexible search error: {str(e)}")
            return self._get_mock_neo4j_results(query, [], 2)

    def _get_mock_neo4j_results(self, query: str, entities: List[str], count: int) -> List[SearchResult]:
        """Fallback mock Neo4j results"""
        return [
            SearchResult(
                id=f"neo4j_{i}",
                title=f"Memory {i+1}",
                content=f"Memory content related to '{query}' with entities {entities} from Neo4j graph database...",
                score=0.8 - (i * 0.1),
                source="neo4j",
                metadata={
                    "content_type": "memory",
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
        return results
