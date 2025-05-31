import sqlite3
import os

# Connect to the database
db_path = os.path.join(os.path.dirname(__file__), "..", "data", "swai-vyn.db")
conn = sqlite3.connect(db_path)

# Get all table names
cursor = conn.execute("SELECT name FROM sqlite_master WHERE type='table';")
tables = [row[0] for row in cursor.fetchall()]

print("Tables in database:")
for table in tables:
    print(f"  {table}")

# Check specific tables and their schemas
for table in ["ChatIndices", "Memories", "Conversations"]:
    if table in tables:
        print(f"\n{table} schema:")
        cursor = conn.execute(f"PRAGMA table_info({table});")
        columns = cursor.fetchall()
        for col in columns:
            print(f"  {col[1]} ({col[2]})")
    else:
        print(f"\n{table}: Table not found")

conn.close()
