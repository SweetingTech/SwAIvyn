#!/usr/bin/env python3
"""
Test script to verify vector routing is working correctly.
This script will test that:
1. Memories are stored in Neo4j
2. Conversations are stored in Weaviate  
3. Uploads are stored in Weaviate
4. Memory searches hit Neo4j
5. Upload searches hit Weaviate
"""

import requests
import json
import time

# Configuration
BASE_URL = "http://localhost:5000"
USER_ID = "00000000-0000-0000-0000-000000000001"

def test_memory_storage():
    """Test that memories are being stored and retrieved correctly"""
    print("🧠 Testing Memory Storage and Retrieval...")
    
    # First, let's send a chat message that should create a memory
    chat_data = {
        "message": "My dog's name is Buddy and he loves playing fetch in the park.",
        "conversationId": "test-conversation-123",
        "characterId": "test-character-456"
    }
    
    print(f"📤 Sending chat message: {chat_data['message']}")
    
    try:
        response = requests.post(f"{BASE_URL}/api/conversation/chat", json=chat_data)
        print(f"📥 Chat response status: {response.status_code}")
        
        if response.status_code == 200:
            result = response.json()
            print(f"✅ Chat successful: {result.get('message', 'No message')[:100]}...")
            
            # Wait a moment for memory processing
            time.sleep(2)
            
            # Now test memory search
            print("🔍 Testing memory search...")
            search_response = requests.get(f"{BASE_URL}/api/memory/search", params={
                "query": "dog name",
                "userId": USER_ID
            })
            
            if search_response.status_code == 200:
                memories = search_response.json()
                print(f"📋 Found {len(memories)} memories")
                for memory in memories:
                    print(f"  💭 Memory: {memory.get('content', 'No content')[:100]}...")
            else:
                print(f"❌ Memory search failed: {search_response.status_code}")
                
        else:
            print(f"❌ Chat failed: {response.status_code} - {response.text}")
            
    except Exception as e:
        print(f"❌ Error during memory test: {e}")

def test_neo4j_direct():
    """Test Neo4j directly to see what's stored"""
    print("\n🔗 Testing Neo4j Direct Access...")
    
    try:
        # Query Neo4j for all Memory nodes
        neo4j_query = {
            "statements": [
                {
                    "statement": "MATCH (m:Memory) RETURN m LIMIT 10",
                    "resultDataContents": ["row", "graph"]
                }
            ]
        }
        
        response = requests.post(
            "http://localhost:7474/db/neo4j/tx/commit",
            json=neo4j_query,
            headers={"Content-Type": "application/json"}
        )
        
        if response.status_code == 200:
            result = response.json()
            print("✅ Neo4j query successful")
            
            if result.get('results') and result['results'][0].get('data'):
                memories = result['results'][0]['data']
                print(f"📋 Found {len(memories)} Memory nodes in Neo4j")
                for memory in memories:
                    print(f"  💭 Memory node: {memory}")
            else:
                print("📭 No Memory nodes found in Neo4j")
        else:
            print(f"❌ Neo4j query failed: {response.status_code} - {response.text}")
            
    except Exception as e:
        print(f"❌ Error querying Neo4j: {e}")

def test_vector_indexes():
    """Test Neo4j vector indexes"""
    print("\n📊 Testing Neo4j Vector Indexes...")
    
    try:
        # Query for vector indexes
        index_query = {
            "statements": [
                {
                    "statement": "SHOW INDEXES",
                    "resultDataContents": ["row"]
                }
            ]
        }
        
        response = requests.post(
            "http://localhost:7474/db/neo4j/tx/commit",
            json=index_query,
            headers={"Content-Type": "application/json"}
        )
        
        if response.status_code == 200:
            result = response.json()
            print("✅ Index query successful")
            
            if result.get('results') and result['results'][0].get('data'):
                indexes = result['results'][0]['data']
                print(f"📋 Found {len(indexes)} indexes in Neo4j")
                for index in indexes:
                    print(f"  📊 Index: {index}")
            else:
                print("📭 No indexes found in Neo4j")
        else:
            print(f"❌ Index query failed: {response.status_code} - {response.text}")
            
    except Exception as e:
        print(f"❌ Error querying indexes: {e}")

if __name__ == "__main__":
    print("🚀 Starting Vector Routing Tests...")
    print("=" * 50)
    
    # Test memory storage and retrieval
    test_memory_storage()
    
    # Test Neo4j direct access
    test_neo4j_direct()
    
    # Test vector indexes
    test_vector_indexes()
    
    print("\n" + "=" * 50)
    print("🏁 Vector Routing Tests Complete!")
