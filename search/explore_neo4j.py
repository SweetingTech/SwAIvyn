#!/usr/bin/env python3
"""
Script to explore the actual Neo4j database structure
"""

from neo4j import GraphDatabase
import json

def explore_database():
    driver = GraphDatabase.driver("bolt://localhost:7687", auth=("neo4j", "password"))
    
    try:
        with driver.session() as session:
            print("=== EXPLORING NEO4J DATABASE STRUCTURE ===\n")
            
            # 1. Get all node labels
            print("1. NODE LABELS:")
            result = session.run("CALL db.labels()")
            labels = [record["label"] for record in result]
            print(f"Found labels: {labels}")
            print()
            
            # 2. Get all relationship types
            print("2. RELATIONSHIP TYPES:")
            result = session.run("CALL db.relationshipTypes()")
            rel_types = [record["relationshipType"] for record in result]
            print(f"Found relationship types: {rel_types}")
            print()
            
            # 3. For each label, get sample nodes and their properties
            for label in labels:
                print(f"3. SAMPLE NODES FOR LABEL '{label}':")
                query = f"MATCH (n:{label}) RETURN n LIMIT 5"
                result = session.run(query)
                
                count = 0
                for record in result:
                    node = record["n"]
                    count += 1
                    print(f"  Node {count}:")
                    print(f"    Labels: {list(node.labels)}")
                    print(f"    Properties: {dict(node)}")
                    print()
                
                # Get total count for this label
                count_query = f"MATCH (n:{label}) RETURN count(n) as total"
                total_result = session.run(count_query)
                total = total_result.single()["total"]
                print(f"  Total {label} nodes: {total}")
                print()
            
            # 4. Get schema information
            print("4. SCHEMA CONSTRAINTS AND INDEXES:")
            try:
                # Get constraints
                result = session.run("SHOW CONSTRAINTS")
                print("  Constraints:")
                for record in result:
                    print(f"    {dict(record)}")
            except Exception as e:
                print(f"  Could not get constraints: {e}")
            
            try:
                # Get indexes
                result = session.run("SHOW INDEXES")
                print("  Indexes:")
                for record in result:
                    print(f"    {dict(record)}")
            except Exception as e:
                print(f"  Could not get indexes: {e}")
            
            print()
            
            # 5. Sample relationships
            print("5. SAMPLE RELATIONSHIPS:")
            result = session.run("MATCH (a)-[r]->(b) RETURN type(r) as rel_type, labels(a) as from_labels, labels(b) as to_labels LIMIT 10")
            for record in result:
                print(f"  {record['from_labels']} -[{record['rel_type']}]-> {record['to_labels']}")
            
    except Exception as e:
        print(f"Error exploring database: {e}")
    finally:
        driver.close()

if __name__ == "__main__":
    explore_database()
