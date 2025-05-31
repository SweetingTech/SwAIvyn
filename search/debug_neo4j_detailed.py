#!/usr/bin/env python3
import logging
from neo4j import GraphDatabase

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

NEO4J_URI = "bolt://localhost:7687"
NEO4J_USER = "neo4j"
NEO4J_PASSWORD = "password"

def debug_neo4j_structure():
    try:
        driver = GraphDatabase.driver(NEO4J_URI, auth=(NEO4J_USER, NEO4J_PASSWORD))
        
        with driver.session() as session:
            print("=" * 60)
            print("NEO4J DATABASE ANALYSIS")
            print("=" * 60)
            
            # 1. Check what node types exist
            result = session.run("CALL db.labels()")
            labels = [record["label"] for record in result]
            print(f"\n📊 Available node labels: {labels}")
            
            # 2. Check total node count
            result = session.run("MATCH (n) RETURN count(n) as total_nodes")
            total_nodes = result.single()["total_nodes"]
            print(f"\n📈 Total nodes in database: {total_nodes}")
            
            # 3. Check relationship types
            result = session.run("CALL db.relationshipTypes()")
            rel_types = [record["relationshipType"] for record in result]
            print(f"\n🔗 Relationship types: {rel_types}")
            
            # 4. Sample of all nodes with their properties
            print(f"\n🔍 SAMPLE NODES (first 10):")
            result = session.run("MATCH (n) RETURN n, labels(n) as node_labels LIMIT 10")
            for i, record in enumerate(result):
                node = record["n"]
                node_labels = record["node_labels"]
                node_props = dict(node)
                print(f"  Node {i+1}: {node_labels}")
                for key, value in node_props.items():
                    print(f"    {key}: {str(value)[:100]}{'...' if len(str(value)) > 100 else ''}")
                print()
            
            # 5. Search for nodes containing "cujo"
            print(f"\n🐕 SEARCHING FOR 'cujo':")
            result = session.run("""
                MATCH (n)
                WHERE ANY(prop IN keys(n) WHERE toString(n[prop]) CONTAINS 'cujo')
                RETURN n, labels(n) as node_labels
                LIMIT 10
            """)
            
            cujo_results = list(result)
            if cujo_results:
                print(f"Found {len(cujo_results)} nodes containing 'cujo':")
                for i, record in enumerate(cujo_results):
                    node = record["n"]
                    node_labels = record["node_labels"]
                    node_props = dict(node)
                    print(f"  Result {i+1}: {node_labels}")
                    for key, value in node_props.items():
                        if 'cujo' in str(value).lower():
                            print(f"    ✅ {key}: {value}")
                        else:
                            print(f"    {key}: {str(value)[:50]}{'...' if len(str(value)) > 50 else ''}")
                    print()
            else:
                print("❌ No nodes found containing 'cujo'")
                
                # Try case-insensitive search
                print("\n🔍 Trying case-insensitive search for 'cujo':")
                result = session.run("""
                    MATCH (n)
                    WHERE ANY(prop IN keys(n) WHERE toLower(toString(n[prop])) CONTAINS 'cujo')
                    RETURN n, labels(n) as node_labels
                    LIMIT 10
                """)
                cujo_results_ci = list(result)
                if cujo_results_ci:
                    print(f"Found {len(cujo_results_ci)} nodes with case-insensitive search:")
                    for i, record in enumerate(cujo_results_ci):
                        node = record["n"]
                        node_labels = record["node_labels"]
                        node_props = dict(node)
                        print(f"  Result {i+1}: {node_labels}")
                        for key, value in node_props.items():
                            if 'cujo' in str(value).lower():
                                print(f"    ✅ {key}: {value}")
                            else:
                                print(f"    {key}: {str(value)[:50]}{'...' if len(str(value)) > 50 else ''}")
                        print()
                else:
                    print("❌ Still no results with case-insensitive search")
            
            # 6. Check for nodes with specific properties
            print(f"\n📋 PROPERTY ANALYSIS:")
            result = session.run("""
                MATCH (n)
                UNWIND keys(n) as key
                RETURN DISTINCT key, count(*) as usage_count
                ORDER BY usage_count DESC
                LIMIT 20
            """)
            print("Most common properties:")
            for record in result:
                print(f"  {record['key']}: used in {record['usage_count']} nodes")
                
        driver.close()
        
    except Exception as e:
        print(f"❌ Error: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    debug_neo4j_structure()
