# SwAIvyn Database Implementation Plan

> **Note:** The active SwAIvyn stack now uses PostgreSQL via SQLAlchemy for the FastAPI backend. The SQLite-focused content below describes the legacy .NET implementation and is retained for historical context.

## Design Goals & Constraints

1. **One-click install / portable `.exe`** – no Docker, no external services that must be running
2. **Single-user desktop workload** – one human + background threads (STT, TTS, vector search)
3. **Local data lives in `Sqldatabase/` folder relative to application**
4. **Support "power mode" later** – allow pointing to external Postgres, Qdrant, etc.
5. **Automatic character loading** – load character cards from `frontend/AI` directory on startup
6. **Idempotent database operations** – safe to run setup scripts multiple times

## Database Technology Stack

| Concern                                | Engine                                                         | .NET package / lib                                           | Persistence file(s)                        | Authentication |
| -------------------------------------- | -------------------------------------------------------------- | ------------------------------------------------------------ | ------------------------------------------ | -------------- |
| Relational / config / auth             | **SQLite** (WAL mode)                                          | `Microsoft.EntityFrameworkCore.Sqlite`                       | `swai-vyn.db`                              | N/A |
| Vector search                          | **SQLite-VSS** (HNSW index as extension)                       | `sqlite-vss` DLL + `Microsoft.Data.Sqlite`                   | `swai-vyn.db` (VSS tables)                 | N/A |
| Graph database                         | **Neo4j** (embedded or remote)                                 | Custom HTTP client                                           | Neo4j database files                       | Username: `neo4j`<br>Password: `password`<br>Config: `appsettings.json`<br>Auth file: `%AppData%\SwAIvyn\neo4j\conf\auth` |
| Ephemeral cache                        | In-process `MemoryCache` (`IMemoryCache`)                      | built-in                                                     | N/A                                        | N/A |
| Chat messages                          | File system (JSON files)                                        | System.IO + System.Text.Json                                 | `/sessions/{conversationId}/{timestamp}.json` | N/A |
| Large binary blobs (avatar PNGs, WAVs) | File system                                                    | –                                                            | `/Assets/…`                                | N/A |

## Database Initialization & Setup

### Critical Requirements for SwAIvyn to Function

SwAIvyn is a **single-user application** that requires exactly one default user to exist in the database. The entire application depends on this base user existing with the correct schema.

#### Required Database Schema

The Users table **MUST** include the `LastSelectedCharacterId` column for the application to function:

```sql
CREATE TABLE Users (
    Id TEXT NOT NULL,
    Username TEXT NOT NULL,
    PasswordHash TEXT NOT NULL,
    PINCode TEXT NOT NULL,
    RecoveryPhrase TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    LastLogin TEXT NOT NULL,
    LastSelectedCharacterId TEXT NULL,  -- CRITICAL: Required for UserController
    CONSTRAINT PK_Users PRIMARY KEY (Id)
);
```

#### Default User Creation

The application expects a default user with this exact ID:
- **User ID**: `00000000-0000-0000-0000-000000000001`
- **Username**: `user`
- **All fields**: Must be populated including `LastSelectedCharacterId`

#### Character Loading Process

Characters are automatically loaded from the `frontend/AI` directory:

1. **Directory Structure Expected**:
   ```
   frontend/AI/
   ├── GLaDOS/
   │   ├── GLaDOS_Character_card.yaml
   │   └── char_img.jpg
   ├── Sam/
   │   └── Sam_Character_card.yaml
   └── Sherlock/
       └── Sherlock_Character_card.yaml
   ```

2. **Loading Logic**:
   - Scan `frontend/AI` directory for subdirectories
   - For each subdirectory, find `.yaml` file
   - Parse basic character info (name, description, personality)
   - Find image files (jpg, png, jpeg)
   - Insert into Avatars table with proper UserId

3. **Idempotent Behavior**: Characters are only created if they don't already exist

### Database Initialization Tools

Three tools handle database setup with **dynamic path resolution** (no hardcoded paths):

#### 1. `tools/CreateTables/Program.cs`
- **Purpose**: Complete database setup from scratch
- **Features**:
  - Creates all required tables (Users, Avatars, Prompts)
  - Creates default user with correct ID and schema
  - Loads character cards from `frontend/AI` directory
  - Idempotent operations (safe to run multiple times)
  - Dynamic path resolution for database location

#### 2. `tools/UpdateDatabase/Program.cs`
- **Purpose**: Add missing columns to existing database
- **Features**:
  - Adds `LastSelectedCharacterId` to Users table
  - Adds missing columns to Avatars table
  - Graceful error handling for existing columns
  - Dynamic path resolution

#### 3. `backend/Services/DirectDatabaseService.cs`
- **Purpose**: Runtime database initialization
- **Features**:
  - Called during application startup
  - Creates Users table with complete schema
  - Creates default user if missing
  - Integrates with Entity Framework

### Build Scripts

#### `scripts/first_run.ps1` - Complete Setup
- Frontend build (optional with `-SkipFrontend`)
- Backend compilation
- Database validation and initialization
- Character loading
- Database copying to application location

#### `scripts/re_run.ps1` - Development Rebuilds
- Quick rebuilds for development
- Skips database operations (assumes already set up)

#### `scripts/startup.ps1` - Nuclear Option
- Complete rebuild from scratch
- Fixes PowerShell syntax issues
- Verifies all components
- Runs complete setup process

## Implementation Steps

### 1. Entity Models

The following entity models have been implemented to support the folder structure, conversation management, and chat indexing:

```csharp
// AppUser.cs
public class AppUser
{
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string PINCode { get; set; }
    public string RecoveryPhrase { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastLogin { get; set; }
    public string? LastSelectedCharacterId { get; set; }  // CRITICAL: Required for UserController

    // Navigation properties
    public ICollection<MemoryItem> Memories { get; set; }
    public ICollection<AvatarInfo> Avatars { get; set; }
    public ICollection<Conversation> Conversations { get; set; }
}

// Folder.cs
public class Folder
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; }
    public Guid? ParentId { get; set; }
    public DateTime CreatedUtc { get; set; }

    // Navigation properties
    public AppUser User { get; set; }
    public Folder Parent { get; set; }
    public ICollection<Folder> Children { get; set; }
    public ICollection<Conversation> Conversations { get; set; }
}

// Conversation.cs
public class Conversation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? FolderId { get; set; }
    public string Title { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastOpenUtc { get; set; }

    // Navigation properties
    public AppUser User { get; set; }
    public Folder Folder { get; set; }
    public ICollection<ChatHistory> Messages { get; set; }
}

// ChatHistory.cs
public class ChatHistory
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; }
    public string Sender { get; set; }
    public DateTime Timestamp { get; set; }

    // Navigation properties
    public AppUser User { get; set; }
    public Conversation Conversation { get; set; }
}

// ChatIndex.cs
public class ChatIndex
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; }
    public string FilePath { get; set; }
    public DateTime CreatedUtc { get; set; }

    // Navigation properties
    public Conversation Conversation { get; set; }
}

// MemoryItem.cs
public class MemoryItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; }
    public string Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessed { get; set; }

    // Navigation properties
    public AppUser User { get; set; }
}

// Settings.cs
public class Settings
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
    public DateTime LastModified { get; set; }

    // Navigation properties
    public AppUser User { get; set; }
}

// Common settings keys include:
// - OllamaApiUrl
// - LmStudioApiUrl
// - Neo4jUri
// - Neo4jBoltPort
// - Neo4jHttpPort
// - DefaultLlmEngine
// - DefaultLlmModel

// AvatarInfo.cs
public class AvatarInfo
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; }
    public string ImagePath { get; set; }
    public string Personality { get; set; }
    public string VoiceSettings { get; set; }

    // Navigation properties
    public AppUser User { get; set; }
}
```

### 2. Entity Relationships Configuration

The entity relationships have been configured in the `OnModelCreating` method of `ApplicationDbContext`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // AppUser
    modelBuilder.Entity<AppUser>()
        .HasKey(u => u.Id);

    modelBuilder.Entity<AppUser>()
        .HasIndex(u => u.Username)
        .IsUnique();

    // Folder
    modelBuilder.Entity<Folder>()
        .HasKey(f => f.Id);

    modelBuilder.Entity<Folder>()
        .HasOne(f => f.User)
        .WithMany()
        .HasForeignKey(f => f.UserId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<Folder>()
        .HasOne(f => f.Parent)
        .WithMany(f => f.Children)
        .HasForeignKey(f => f.ParentId)
        .OnDelete(DeleteBehavior.Restrict)
        .IsRequired(false);

    // Conversation
    modelBuilder.Entity<Conversation>()
        .HasKey(c => c.Id);

    modelBuilder.Entity<Conversation>()
        .HasOne(c => c.User)
        .WithMany(u => u.Conversations)
        .HasForeignKey(c => c.UserId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<Conversation>()
        .HasOne(c => c.Folder)
        .WithMany(f => f.Conversations)
        .HasForeignKey(c => c.FolderId)
        .OnDelete(DeleteBehavior.SetNull)
        .IsRequired(false);

    modelBuilder.Entity<Conversation>()
        .HasIndex(c => new { c.UserId, c.CreatedUtc });

    // ChatHistory
    modelBuilder.Entity<ChatHistory>()
        .HasKey(ch => ch.Id);

    modelBuilder.Entity<ChatHistory>()
        .HasOne(ch => ch.Conversation)
        .WithMany(c => c.Messages)
        .HasForeignKey(ch => ch.ConversationId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<ChatHistory>()
        .HasOne(ch => ch.User)
        .WithMany()
        .HasForeignKey(ch => ch.UserId)
        .OnDelete(DeleteBehavior.NoAction);

    // ChatIndex
    modelBuilder.Entity<ChatIndex>()
        .HasKey(ci => ci.Id);

    modelBuilder.Entity<ChatIndex>()
        .HasOne(ci => ci.Conversation)
        .WithMany()
        .HasForeignKey(ci => ci.ConversationId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<ChatIndex>()
        .HasIndex(ci => new { ci.ConversationId, ci.CreatedUtc });

    // MemoryItem
    modelBuilder.Entity<MemoryItem>()
        .HasKey(m => m.Id);

    modelBuilder.Entity<MemoryItem>()
        .HasOne(m => m.User)
        .WithMany(u => u.Memories)
        .HasForeignKey(m => m.UserId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<MemoryItem>()
        .HasIndex(m => new { m.UserId, m.Category });

    // Settings
    modelBuilder.Entity<Settings>()
        .HasKey(s => s.Id);

    modelBuilder.Entity<Settings>()
        .HasOne(s => s.User)
        .WithMany()
        .HasForeignKey(s => s.UserId)
        .OnDelete(DeleteBehavior.Cascade)
        .IsRequired(false);

    modelBuilder.Entity<Settings>()
        .HasIndex(s => new { s.UserId, s.Key })
        .IsUnique();

    // AvatarInfo
    modelBuilder.Entity<AvatarInfo>()
        .HasKey(a => a.Id);

    modelBuilder.Entity<AvatarInfo>()
        .HasOne(a => a.User)
        .WithMany(u => u.Avatars)
        .HasForeignKey(a => a.UserId)
        .OnDelete(DeleteBehavior.Cascade);

    // Add SQL trigger for folder cascade delete
    modelBuilder.Entity<Folder>()
        .ToTable(tb => tb.HasTrigger("DeleteFolderCascade"));
}
```

### 3. Enable WAL Mode and Connection Pooling

Update the database configuration in `Program.cs`:

```csharp
// Register DbContext with WAL mode and connection pooling
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlite(connectionString + ";Pooling=true;Cache=Shared");
});

// Add a singleton service to initialize the database
builder.Services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
```

### 4. Create Database Initializer Service

```csharp
public interface IDatabaseInitializer
{
    Task InitializeAsync();
}

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ISimpleLoggerService _logger;

    public DatabaseInitializer(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ISimpleLoggerService logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInfo("Initializing database...");

            // Create database if it doesn't exist
            using (var context = await _dbContextFactory.CreateDbContextAsync())
            {
                await context.Database.EnsureCreatedAsync();

                // Enable WAL mode
                await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");

                _logger.LogInfo("Database initialization completed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical("Failed to initialize database", ex);
            throw;
        }
    }
}
```

### 5. Implement Vector Search for Memory System

Create a SQLite-VSS extension loader and vector store service:

```csharp
public interface IVectorStore
{
    Task InitializeAsync();
    Task<int> StoreVectorAsync(Guid id, float[] embedding);
    Task<List<(Guid Id, float Score)>> SearchAsync(float[] queryVector, int limit = 10);
    Task DeleteVectorAsync(Guid id);
}

public class SqliteVectorStore : IVectorStore
{
    private readonly string _connectionString;
    private readonly ISimpleLoggerService _logger;

    public SqliteVectorStore(
        IConfiguration configuration,
        ISimpleLoggerService logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Load the VSS extension
            connection.EnableExtensions();
            var result = connection.ExecuteScalar<string>("SELECT load_extension('sqlite-vss');");

            // Create the vector table if it doesn't exist
            await connection.ExecuteAsync(@"
                CREATE VIRTUAL TABLE IF NOT EXISTS MemoryVectors
                USING vss0(
                    id TEXT PRIMARY KEY,
                    embedding BLOB,
                    dims(768),
                    distance('cosine')
                );");

            _logger.LogInfo("Vector store initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical("Failed to initialize vector store", ex);
            throw;
        }
    }

    public async Task<int> StoreVectorAsync(Guid id, float[] embedding)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var blob = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, blob, 0, blob.Length);

        return await connection.ExecuteAsync(
            "INSERT OR REPLACE INTO MemoryVectors(id, embedding) VALUES (@Id, @Embedding)",
            new { Id = id.ToString(), Embedding = blob });
    }

    public async Task<List<(Guid Id, float Score)>> SearchAsync(float[] queryVector, int limit = 10)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var blob = new byte[queryVector.Length * sizeof(float)];
        Buffer.BlockCopy(queryVector, 0, blob, 0, blob.Length);

        var results = await connection.QueryAsync<VectorSearchResult>(
            @"SELECT id, vss_distance(MemoryVectors) AS score
              FROM MemoryVectors
              WHERE vss_search(MemoryVectors, @QueryVector, @Limit);",
            new { QueryVector = blob, Limit = limit });

        return results.Select(r => (Guid.Parse(r.id), r.score)).ToList();
    }

    public async Task DeleteVectorAsync(Guid id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            "DELETE FROM MemoryVectors WHERE id = @Id",
            new { Id = id.ToString() });
    }

    private class VectorSearchResult
    {
        public string id { get; set; }
        public float score { get; set; }
    }
}
```

### 6. Register Vector Store Service

Add to `Program.cs`:

```csharp
// Register vector store service
builder.Services.AddSingleton<IVectorStore, SqliteVectorStore>();
```

### 7. Initialize Database on Startup

Update the application startup in `Program.cs`:

```csharp
// Initialize database
var dbInitializer = app.Services.GetRequiredService<IDatabaseInitializer>();
await dbInitializer.InitializeAsync();

// Initialize vector store
var vectorStore = app.Services.GetRequiredService<IVectorStore>();
await vectorStore.InitializeAsync();
```

## Troubleshooting Guide

### Common Issues and Solutions

#### 1. 500 Internal Server Error on `/api/user/default`

**Symptoms**: Frontend shows "Failed to load user profile" error

**Root Cause**: Missing `LastSelectedCharacterId` column in Users table

**Solution**:
```powershell
# Run database update tool
cd tools\UpdateDatabase
dotnet run -c Release

# Or run complete setup
.\scripts\first_run.ps1 -SkipFrontend
```

#### 2. Characters Not Loading

**Symptoms**: Character selector is empty or shows no avatars

**Root Causes**:
- `frontend/AI` directory missing or empty
- Default user doesn't exist
- Character loading failed during setup

**Solution**:
```powershell
# Verify frontend/AI directory exists with YAML files
# Run CreateTables tool to load characters
cd tools\CreateTables
dotnet run -c Release
```

#### 3. Database File Locked

**Symptoms**: "The process cannot access the file because it is being used by another process"

**Solution**:
```powershell
# Kill all .NET processes
Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name 'SwAIvyn' -ErrorAction SilentlyContinue | Stop-Process -Force

# Then retry build
.\scripts\first_run.ps1 -SkipFrontend
```

#### 4. PowerShell Script Syntax Errors

**Symptoms**: Unicode character errors in PowerShell scripts

**Solution**:
```powershell
# Use startup script to fix syntax issues
.\scripts\startup.ps1 -FixScriptsOnly
```

### Database Validation Commands

```powershell
# Check if default user exists
cd tools\CreateTables
dotnet run -c Release --no-build

# Check database schema
sqlite3 Sqldatabase\swai-vyn.db ".schema Users"

# Count records
sqlite3 Sqldatabase\swai-vyn.db "SELECT COUNT(*) FROM Users; SELECT COUNT(*) FROM Avatars;"

# Test API endpoint
Invoke-RestMethod -Uri 'http://localhost:5000/api/user/default' -Method GET
```

## Current Implementation Status

### ✅ Completed Features

1. **Database Schema**: Complete Users table with `LastSelectedCharacterId` column
2. **Default User Creation**: Automatic creation of required default user
3. **Character Loading**: Automatic loading from `frontend/AI` directory
4. **Database Tools**: Three tools for different initialization scenarios
5. **Build Scripts**: Complete build automation with database setup
6. **Idempotent Operations**: Safe to run setup multiple times
7. **Dynamic Path Resolution**: No hardcoded paths, works anywhere
8. **Error Handling**: Graceful handling of existing data and missing files
9. **API Functionality**: `/api/user/default` endpoint working correctly
10. **Character Management**: GLaDOS, Sam, and Sherlock characters loaded

### 🔧 Current Database State

- **Users**: 1 (default user with ID `00000000-0000-0000-0000-000000000001`)
- **Avatars**: 3 (GLaDOS, Sam, Sherlock loaded from YAML files)
- **Schema**: Complete with all required columns
- **Location**: `Sqldatabase/swai-vyn.db` and copied to application runtime location

### 🚀 Ready for Production

The database implementation is now **fully functional** and ready for production use. All critical issues have been resolved:

- ✅ No more 500 Internal Server Errors
- ✅ User profile loads successfully
- ✅ Character selector populated with 3 characters
- ✅ Complete build automation
- ✅ Robust error handling and recovery

## Next Steps

1. **Create data directory structure** on startup ✅
2. **Implement backup service** for automated backups ✅
3. **Add migration support** for future schema changes ✅
4. **Update controllers** to use the refined data models ✅
5. **Implement memory embedding** with the vector store ✅
6. **Implement user-configurable LLM settings** ✅
7. **Create settings UI** for configuring LLM connections and preferences ✅
8. **Integrate chat functionality** with user-selected LLM settings ✅
9. **Implement folder management for organizing conversations** ✅
   - Create, rename, and delete folders
   - Hierarchical folder structure
   - Automatic deletion of contained conversations when folder is deleted
10. **Implement automatic chat session management** ✅
   - Start with empty chat session
   - Assign UUID on first message
   - Auto-save sessions
   - Generate title from first message
   - Rename, edit, and delete sessions
11. **Database initialization and character loading** ✅
   - Automatic default user creation
   - Character card loading from filesystem
   - Idempotent database operations
   - Dynamic path resolution for portability
