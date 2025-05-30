import asyncio
import re
import logging
from collections import defaultdict
from typing import List, Dict, Any, Optional, Tuple
from dataclasses import dataclass
import json
from datetime import datetime

# Mock imports - replace with actual implementations
# from sentence_transformers import SentenceTransformer, CrossEncoder
# import weaviate
# import neo4j
# import sqlite3/psycopg2/etc


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
        
        # Initialize embedding model (replace with actual model)
        # self.embedding_model = SentenceTransformer('all-MiniLM-L6-v2')
        # self.cross_encoder = CrossEncoder('cross-encoder/ms-marco-MiniLM-L-2-v2')
        
        self.logger = logging.getLogger(__name__)
        
        # Search weights for different databases
        self.db_weights = {
            'sql': 0.3,
            'weaviate': 0.4,
            'neo4j': 0.3
        }
    
    async def search(self, query: str, top_k: int = 20, filters: Optional[Dict] = None) -> List[SearchResult]:
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
                self.neo4j_search(query, query_features, filters)
            ]
            
            sql_results, vector_results, graph_results = await asyncio.gather(*tasks)
            
            # 3. Score Normalization
            normalized_results = self.normalize_scores([
                ('sql', sql_results),
                ('weaviate', vector_results), 
                ('neo4j', graph_results)
            ])
            
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
        has_filters = bool(re.search(r'\b(after|before|from|to|type|category):', query, re.IGNORECASE))
        
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
            keywords=keywords
        )
    
    def extract_entities(self, query: str) -> List[str]:
        """Extract named entities from query (simplified version)"""
        # Simple regex-based entity extraction
        # Replace with proper NER model like spaCy or transformers
        entities = []
        
        # Capitalized words (potential proper nouns)
        capitalized = re.findall(r'\b[A-Z][a-z]+\b', query)
        entities.extend(capitalized)
        
        # Date patterns
        dates = re.findall(r'\b\d{4}[-/]\d{1,2}[-/]\d{1,2}\b', query)
        entities.extend(dates)
        
        return list(set(entities))
    
    def classify_query_type(self, query: str) -> str:
        """Classify the type of query to optimize search strategy"""
        query_lower = query.lower()
        
        if any(word in query_lower for word in ['what', 'who', 'when', 'where', 'how']):
            return 'factual'
        elif any(word in query_lower for word in ['related', 'similar', 'connected', 'linked']):
            return 'relational'
        else:
            return 'exploratory'
    
    def extract_keywords(self, query: str) -> List[str]:
        """Extract important keywords from query"""
        # Simple keyword extraction (replace with more sophisticated method)
        stop_words = {'the', 'a', 'an', 'and', 'or', 'but', 'in', 'on', 'at', 'to', 'for', 'of', 'with', 'by'}
        words = re.findall(r'\b\w+\b', query.lower())
        return [word for word in words if word not in stop_words and len(word) > 2]
    
    async def sql_search(self, query: str, features: QueryFeatures, filters: Optional[Dict] = None) -> List[SearchResult]:
        """
        SQL database search using full-text search and structured queries
        """
        try:
            results = []
            
            if features.has_structured_filters:
                results = await self.structured_sql_search(query, features, filters)
            else:
                results = await self.full_text_sql_search(query, features)
            
            self.logger.info(f"SQL search returned {len(results)} results")
            return results
            
        except Exception as e:
            self.logger.error(f"SQL search error: {str(e)}")
            return []
    
    async def structured_sql_search(self, query: str, features: QueryFeatures, filters: Optional[Dict]) -> List[SearchResult]:
        """Handle structured SQL queries with filters"""
        # Build SQL query based on filters and structured elements
        base_query = """
        SELECT id, title, content, created_at, content_type, source,
               ts_rank(to_tsvector('english', title || ' ' || content), 
                      plainto_tsquery('english', %s)) as score
        FROM documents 
        WHERE to_tsvector('english', title || ' ' || content) @@ plainto_tsquery('english', %s)
        """
        
        params = [query, query]
        
        # Add filters
        if filters:
            for key, value in filters.items():
                if key == 'content_type':
                    base_query += " AND content_type = %s"
                    params.append(value)
                elif key == 'date_after':
                    base_query += " AND created_at > %s"
                    params.append(value)
        
        base_query += " ORDER BY score DESC LIMIT 50"
        
        # Execute query (mock implementation)
        # cursor = await self.sql.execute(base_query, params)
        # rows = await cursor.fetchall()
        
        # Mock results
        rows = [
            ('1', 'Sample Title', 'Sample content...', datetime.now(), 'document', 'sql_db', 0.8),
            ('2', 'Another Doc', 'More content...', datetime.now(), 'article', 'sql_db', 0.6)
        ]
        
        results = []
        for row in rows:
            results.append(SearchResult(
                id=row[0],
                title=row[1],
                content=row[2][:200] + "...",
                score=row[6],
                source='sql',
                metadata={
                    'created_at': row[3],
                    'content_type': row[4],
                    'origin': row[5]
                }
            ))
        
        return results
    
    async def full_text_sql_search(self, query: str, features: QueryFeatures) -> List[SearchResult]:
        """Handle full-text SQL search"""
        # Use PostgreSQL full-text search or similar
        sql_query = """
        SELECT id, title, content, created_at, content_type, source,
               ts_rank(to_tsvector('english', title || ' ' || content), 
                      plainto_tsquery('english', %s)) as score
        FROM documents 
        WHERE to_tsvector('english', title || ' ' || content) @@ plainto_tsquery('english', %s)
        ORDER BY score DESC LIMIT 50
        """
        
        # Mock implementation
        results = [
            SearchResult(
                id=f"sql_{i}",
                title=f"SQL Document {i}",
                content=f"Content related to {query}...",
                score=0.9 - (i * 0.1),
                source='sql',
                metadata={'content_type': 'document', 'method': 'full_text'}
            )
            for i in range(3)
        ]
        
        return results
    
    async def weaviate_search(self, query: str, features: QueryFeatures, filters: Optional[Dict] = None) -> List[SearchResult]:
        """
        Vector similarity search using Weaviate
        """
        try:
            # Embed the query
            # query_vector = self.embedding_model.encode(query).tolist()
            
            # Mock Weaviate query
            # result = self.weaviate.query.get("Document", ["title", "content", "contentType", "source"]) \
            #     .with_near_vector({"vector": query_vector}) \
            #     .with_limit(50) \
            #     .do()
            
            # Mock results
            mock_weaviate_results = [
                {
                    'title': f'Vector Document {i}',
                    'content': f'Semantically similar content to {query}...',
                    'contentType': 'article',
                    'source': 'weaviate_db',
                    '_additional': {'distance': 0.1 + (i * 0.05), 'id': f'weaviate_{i}'}
                }
                for i in range(4)
            ]
            
            results = []
            for item in mock_weaviate_results:
                # Convert distance to similarity score
                similarity_score = 1.0 - item['_additional']['distance']
                
                results.append(SearchResult(
                    id=item['_additional']['id'],
                    title=item['title'],
                    content=item['content'][:200] + "...",
                    score=similarity_score,
                    source='weaviate',
                    metadata={
                        'content_type': item['contentType'],
                        'origin': item['source'],
                        'distance': item['_additional']['distance']
                    }
                ))
            
            self.logger.info(f"Weaviate search returned {len(results)} results")
            return results
            
        except Exception as e:
            self.logger.error(f"Weaviate search error: {str(e)}")
            return []
    
    async def neo4j_search(self, query: str, features: QueryFeatures, filters: Optional[Dict] = None) -> List[SearchResult]:
        """
        Graph-based search using Neo4j for relationship discovery
        """
        try:
            results = []
            
            if features.entity_mentions:
                results = await self.entity_graph_search(query, features.entity_mentions)
            else:
                results = await self.general_graph_search(query, features)
            
            self.logger.info(f"Neo4j search returned {len(results)} results")
            return results
            
        except Exception as e:
            self.logger.error(f"Neo4j search error: {str(e)}")
            return []
    
    async def entity_graph_search(self, query: str, entities: List[str]) -> List[SearchResult]:
        """Search based on entity relationships in the graph"""
        # Cypher query to find documents related to entities
        cypher_query = """
        MATCH (d:Document)-[:MENTIONS|:RELATED_TO]-(e:Entity)
        WHERE e.name IN $entities
        RETURN d.id, d.title, d.content, d.contentType, d.source,
               count(e) as entity_matches
        ORDER BY entity_matches DESC, d.created_at DESC
        LIMIT 50
        """
        
        # Mock Neo4j execution
        # session = self.neo4j.session()
        # result = session.run(cypher_query, entities=entities)
        
        # Mock results
        mock_neo4j_results = [
            {
                'd.id': f'neo4j_{i}',
                'd.title': f'Graph Document {i}',
                'd.content': f'Content with entities: {", ".join(entities)}...',
                'd.contentType': 'report',
                'd.source': 'neo4j_db',
                'entity_matches': 3 - i
            }
            for i in range(3)
        ]
        
        results = []
        for record in mock_neo4j_results:
            # Score based on entity matches and graph centrality
            score = min(1.0, record['entity_matches'] / len(entities))
            
            results.append(SearchResult(
                id=record['d.id'],
                title=record['d.title'],
                content=record['d.content'][:200] + "...",
                score=score,
                source='neo4j',
                metadata={
                    'content_type': record['d.contentType'],
                    'origin': record['d.source'],
                    'entity_matches': record['entity_matches'],
                    'search_type': 'entity_based'
                }
            ))
        
        return results
    
    async def general_graph_search(self, query: str, features: QueryFeatures) -> List[SearchResult]:
        """General graph search using text matching and graph traversal"""
        # Cypher query for general text search with graph context
        cypher_query = """
        MATCH (d:Document)
        WHERE d.title CONTAINS $query OR d.content CONTAINS $query
        OPTIONAL MATCH (d)-[r]-(related:Document)
        RETURN d.id, d.title, d.content, d.contentType, d.source,
               count(related) as connection_count
        ORDER BY connection_count DESC
        LIMIT 50
        """
        
        # Mock results
        mock_results = [
            {
                'd.id': f'neo4j_general_{i}',
                'd.title': f'Connected Document {i}',
                'd.content': f'General content matching {query}...',
                'd.contentType': 'article',
                'd.source': 'neo4j_db',
                'connection_count': 5 - i
            }
            for i in range(2)
        ]
        
        results = []
        for record in mock_results:
            # Score based on text relevance and graph connectivity
            base_score = 0.7  # Base text match score
            connectivity_bonus = min(0.3, record['connection_count'] / 10)
            score = base_score + connectivity_bonus
            
            results.append(SearchResult(
                id=record['d.id'],
                title=record['d.title'],
                content=record['d.content'][:200] + "...",
                score=score,
                source='neo4j',
                metadata={
                    'content_type': record['d.contentType'],
                    'origin': record['d.source'],
                    'connection_count': record['connection_count'],
                    'search_type': 'general_graph'
                }
            ))
        
        return results
    
    def normalize_scores(self, result_sets: List[Tuple[str, List[SearchResult]]]) -> List[Tuple[str, List[SearchResult]]]:
        """
        Normalize scores across different databases to make them comparable
        """
        normalized_sets = []
        
        for db_name, results in result_sets:
            if not results:
                normalized_sets.append((db_name, results))
                continue
            
            scores = [r.score for r in results]
            if not scores:
                normalized_sets.append((db_name, results))
                continue
                
            min_score, max_score = min(scores), max(scores)
            
            # Avoid division by zero
            if max_score == min_score:
                for result in results:
                    result.normalized_score = 1.0
            else:
                for result in results:
                    # Min-max normalization to [0,1]
                    result.normalized_score = (result.score - min_score) / (max_score - min_score)
            
            normalized_sets.append((db_name, results))
        
        return normalized_sets
    
    def reciprocal_rank_fusion(self, result_sets: List[Tuple[str, List[SearchResult]]], k: int = 60) -> List[SearchResult]:
        """
        Combine results from multiple databases using Reciprocal Rank Fusion
        """
        doc_scores = defaultdict(float)
        doc_objects = {}
        
        for db_name, results in result_sets:
            db_weight = self.db_weights.get(db_name, 1.0)
            
            for rank, result in enumerate(results, 1):
                doc_id = result.id
                
                # RRF formula: weight * (1/(k + rank))
                rrf_score = db_weight * (1.0 / (k + rank))
                doc_scores[doc_id] += rrf_score
                
                # Store the document object (prefer higher-scored versions)
                if doc_id not in doc_objects or result.normalized_score > doc_objects[doc_id].normalized_score:
                    doc_objects[doc_id] = result
        
        # Create final ranked list
        fused_results = []
        for doc_id, combined_score in doc_scores.items():
            result = doc_objects[doc_id]
            result.score = combined_score  # Update with combined RRF score
            fused_results.append(result)
        
        # Sort by combined RRF score
        fused_results.sort(key=lambda x: x.score, reverse=True)
        
        self.logger.info(f"RRF fusion produced {len(fused_results)} unique results")
        return fused_results
    
    async def cross_encoder_rerank(self, query: str, candidates: List[SearchResult], top_n: int = 50) -> List[SearchResult]:
        """
        Re-rank top candidates using a cross-encoder model for better relevance
        """
        if len(candidates) <= 1:
            return candidates
        
        # Take top candidates for re-ranking to avoid computational overhead
        rerank_candidates = candidates[:top_n]
        
        try:
            # Prepare query-document pairs for cross-encoder
            pairs = [(query, candidate.content) for candidate in rerank_candidates]
            
            # Get relevance scores from cross-encoder
            # rerank_scores = self.cross_encoder.predict(pairs)
            
            # Mock cross-encoder scores
            rerank_scores = [0.8 - (i * 0.05) for i in range(len(pairs))]
            
            # Combine RRF scores with rerank scores (weighted average)
            alpha = 0.7  # Weight for RRF score
            beta = 0.3   # Weight for rerank score
            
            for i, candidate in enumerate(rerank_candidates):
                if i < len(rerank_scores):
                    combined_score = alpha * candidate.score + beta * rerank_scores[i]
                    candidate.score = combined_score
                    candidate.metadata['rerank_score'] = rerank_scores[i]
            
            # Re-sort by combined score
            rerank_candidates.sort(key=lambda x: x.score, reverse=True)
            
            # Combine with remaining candidates
            final_results = rerank_candidates + candidates[top_n:]
            
            self.logger.info(f"Re-ranked top {len(rerank_candidates)} results")
            return final_results
            
        except Exception as e:
            self.logger.error(f"Re-ranking error: {str(e)}")
            return candidates
    
    def explain_results(self, query: str, results: List[SearchResult]) -> Dict[str, Any]:
        """
        Provide explanation of how results were obtained and ranked
        """
        explanation = {
            'query': query,
            'total_results': len(results),
            'databases_searched': ['sql', 'weaviate', 'neo4j'],
            'fusion_method': 'reciprocal_rank_fusion',
            'reranking_applied': any('rerank_score' in r.metadata for r in results),
            'source_distribution': {}
        }
        
        # Analyze source distribution
        for result in results:
            source = result.source
            if source not in explanation['source_distribution']:
                explanation['source_distribution'][source] = 0
            explanation['source_distribution'][source] += 1
        
        return explanation


# Example usage and testing
async def main():
    """Example usage of the HybridSearchEngine"""
    
    # Mock database connections (replace with actual connections)
    sql_conn = None  # Your SQL connection
    weaviate_client = None  # Your Weaviate client
    neo4j_driver = None  # Your Neo4j driver
    
    # Initialize search engine
    search_engine = HybridSearchEngine(sql_conn, weaviate_client, neo4j_driver)
    
    # Example search
    query = "machine learning algorithms for natural language processing"
    results = await search_engine.search(query, top_k=10)
    
    # Display results
    print(f"Search results for: '{query}'")
    print("=" * 50)
    
    for i, result in enumerate(results, 1):
        print(f"{i}. {result.title}")
        print(f"   Source: {result.source}")
        print(f"   Score: {result.score:.3f}")
        print(f"   Content: {result.content[:100]}...")
        print(f"   Metadata: {result.metadata}")
        print()
    
    # Get explanation
    explanation = search_engine.explain_results(query, results)
    print("Search Explanation:")
    print(json.dumps(explanation, indent=2, default=str))


if __name__ == "__main__":
    # Configure logging
    logging.basicConfig(level=logging.INFO)
    
    # Run example
    asyncio.run(main())