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
        return [word for word in words if word not in stop_words and len(word) > 2]

    async def weaviate_search(
        self, query: str, features: QueryFeatures, filters: Optional[Dict] = None
    ) -> List[SearchResult]:
        """
        Vector similarity search using Weaviate - PRIMARY content search
        """
        if not self.weaviate:
            self.logger.warning("Weaviate client not available, using mock data")
            return self._get_mock_weaviate_results(query, 5)

        try:
            # Get available collections
            collections = self.weaviate.collections.list_all()
            available_collections = [collection.name for collection in collections]
            
            if not available_collections:
                self.logger.warning("No Weaviate collections found, using mock data")
                return self._get_mock_weaviate_results(query, 5)

            self.logger.info(f"Available Weaviate collections: {available_collections}")

            # Try to use the most appropriate collection
            target_collection = None
            for preferred in ["Document", "Content", "Message", "Chat"]:
                if preferred in available_collections:
                    target_collection = preferred
                    break
            
            if not target_collection:
                target_collection = available_collections[0]
            
            self.logger.info(f"Using Weaviate collection: {target_collection}")

            # Get the collection
            collection = self.weaviate.collections.get(target_collection)

            # Build where filter for userId if provided
            where_filter = None
            user_id = filters.get("userId") if filters else "00000000-0000-0000-0000-000000000001"
            if user_id:
                try:
                    where_filter = wvc.query.Filter.by_property("userId").equal(str(user_id))
                except Exception as filter_error:
                    self.logger.warning(f"Could not apply userId filter: {filter_error}")
                    where_filter = None

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

                # Get content from various possible property names
                content = (
                    obj.properties.get("content") or 
                    obj.properties.get("text") or 
                    obj.properties.get("message") or 
                    obj.properties.get("body") or 
                    str(obj.properties)
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
        Graph-based search using Neo4j - Updated for actual database structure
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
            return self._get_mock_neo4j_results(query, [], 3)

    async def entity_graph_search(
        self, query: str, entities: List[str], filters: Optional[Dict] = None
    ) -> List[SearchResult]:
        """Search for specific entities by name in the knowledge graph"""
        if not self.neo4j:
            self.logger.warning("Neo4j driver not available, using mock data")
            return self._get_mock_neo4j_results(query, entities, 3)

        try:
            # Search for entities that match the query entities by name
            cypher_query = """
            MATCH (e)
            WHERE e.type = 'entity' AND e.name IN $entity_list
            OPTIONAL MATCH (e)-[r]-(related)
            WHERE related.type = 'entity'
            UNWIND e.observations AS obs
            RETURN e.name, e.entityType, obs as observation, 
                   count(DISTINCT related) as connections,
                   collect(DISTINCT related.name)[0..3] as related_names
            ORDER BY connections DESC
            LIMIT 50
            """

            with self.neo4j.session() as session:
                result = session.run(cypher_query, entity_list=entities)
                records = list(result)

            results = []
            for i, record in enumerate(records):
                # Score based on entity matches and connections
                connections = record["connections"] or 0
                base_score = min(1.0, 0.7 + (connections * 0.03))

                # Create content from observations
                observation = record["observation"] or ""
                entity_name = record["e.name"] or f"Entity {i+1}"
                entity_type = record["e.entityType"] or "Unknown"
                related_names = record["related_names"] or []

                results.append(
                    SearchResult(
                        id=f"neo4j_entity_{entity_name}_{i}",
                        title=f"{entity_name} ({entity_type})",
                        content=observation[:500] + "..." if len(observation) > 500 else observation,
                        score=base_score,
                        source="neo4j",
                        metadata={
                            "entity_name": entity_name,
                            "entity_type": entity_type,
                            "connections": connections,
                            "related_entities": related_names,
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
        """General search across all entities and observations in the knowledge graph"""
        if not self.neo4j:
            self.logger.warning("Neo4j driver not available, using mock data")
            return self._get_mock_neo4j_results(query, [], 2)

        try:
            # Extract keywords for searching observations
            keywords = features.keywords or [query.lower()]
            
            # Search for entities whose observations contain keywords
            cypher_query = """
            MATCH (e)
            WHERE e.type = 'entity'
            UNWIND e.observations AS obs
            WHERE ANY(keyword IN $keywords WHERE toLower(obs) CONTAINS toLower(keyword))
            OPTIONAL MATCH (e)-[r]-(related)
            WHERE related.type = 'entity'
            RETURN e.name, e.entityType, obs as observation,
                   count(DISTINCT related) as connections,
                   collect(DISTINCT related.name)[0..3] as related_entities
            ORDER BY connections DESC
            LIMIT 50
            """

            with self.neo4j.session() as session:
                result = session.run(cypher_query, keywords=keywords)
                records = list(result)

            results = []
            for i, record in enumerate(records):
                # Score based on keyword relevance and connections
                connections = record["connections"] or 0
                base_score = min(1.0, 0.6 + (connections * 0.02))

                # Boost score if multiple keywords match
                observation = record["observation"] or ""
                keyword_matches = sum(1 for kw in keywords if kw.lower() in observation.lower())
                if keyword_matches > 1:
                    base_score = min(1.0, base_score * (1 + keyword_matches * 0.1))

                entity_name = record["e.name"] or f"Entity {i+1}"
                entity_type = record["e.entityType"] or "Unknown"
                related_entities = record["related_entities"] or []

                results.append(
                    SearchResult(
                        id=f"neo4j_general_{entity_name}_{i}",
                        title=f"{entity_name} ({entity_type})",
                        content=observation[:500] + "..." if len(observation) > 500 else observation,
                        score=base_score,
                        source="neo4j",
                        metadata={
                            "entity_name": entity_name,
                            "entity_type": entity_type,
                            "connections": connections,
                            "related_entities": related_entities,
                            "keyword_matches": keyword_matches,
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
                id=f"neo4j_mock_{i}",
                title=f"SwAIvyn Knowledge {i+1}",
                content=f"Knowledge about '{query}' related to entities {entities} from Neo4j knowledge graph...",
                score=0.8 - (i * 0.1),
                source="neo4j",
                metadata={
                    "content_type": "knowledge",
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
