import sqlite3
import os

# Connect to the database
db_path = os.path.join(os.path.dirname(__file__), "..", "data", "swai-vyn.db")
conn = sqlite3.connect(db_path)

# Check Users table schema and data
print("Users table schema:")
cursor = conn.execute("PRAGMA table_info(Users);")
columns = cursor.fetchall()
for col in columns:
    print(f"  {col[1]} ({col[2]})")

print("\nUsers table data:")
cursor = conn.execute("SELECT * FROM Users LIMIT 5;")
rows = cursor.fetchall()
for row in rows:
    print(f"  {row}")

# Check Avatars table too
print("\nAvatars table schema:")
cursor = conn.execute("PRAGMA table_info(Avatars);")
columns = cursor.fetchall()
for col in columns:
    print(f"  {col[1]} ({col[2]})")

print("\nAvatars table sample data:")
cursor = conn.execute("SELECT Id, UserId, Name, Personality FROM Avatars LIMIT 3;")
rows = cursor.fetchall()
for row in rows:
    print(f"  {row}")

# Check Prompts table
print("\nPrompts table schema:")
cursor = conn.execute("PRAGMA table_info(Prompts);")
columns = cursor.fetchall()
for col in columns:
    print(f"  {col[1]} ({col[2]})")

print("\nPrompts table sample data:")
cursor = conn.execute("SELECT * FROM Prompts LIMIT 3;")
rows = cursor.fetchall()
for row in rows:
    print(f"  {row}")

conn.close()
