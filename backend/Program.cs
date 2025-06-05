using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SwAIvyn.Data;
using SwAIvyn.Services;
using SwAIvyn.Services.VectorStore;
using SwAIvyn.Services.Graph; // Ensure this is present
using SwAIvyn.Hubs;
using SwAIvyn.Middleware;
using SwAIvyn.HostedServices;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using SQLitePCL;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using YamlDotNet.Serialization;
using System.Linq;
using System.Collections.Generic;

// Initialize SQLitePCL.raw for extension loading
Batteries_V2.Init();

// Targeted Neo4j port cleanup method
static void CleanupAllNeo4jProcesses()
{
    try
    {
        Console.WriteLine("[CLEANUP] Starting targeted Neo4j port cleanup...");

        // Kill any processes using ports 7474 or 7687 (Neo4j's default ports)
        KillProcessesUsingPorts([7474, 7687]);

        // Also kill any processes specifically named neo4j (but not all Java)
        var neo4jProcesses = Process.GetProcesses()
            .Where(p => p.ProcessName.Contains("neo4j", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (neo4jProcesses.Count > 0)
        {
            Console.WriteLine($"[CLEANUP] Found {neo4jProcesses.Count} Neo4j-named processes to terminate");
            foreach (var process in neo4jProcesses)
            {
                try
                {
                    Console.WriteLine($"[CLEANUP] Killing Neo4j process {process.Id}: {process.ProcessName}");
                    process.Kill();
                    process.WaitForExit(3000);
                    Console.WriteLine($"[CLEANUP] Neo4j process {process.Id} terminated");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CLEANUP] Failed to kill Neo4j process {process.Id}: {ex.Message}");
                }
            }
        }
        else
        {
            Console.WriteLine("[CLEANUP] No Neo4j-named processes found");
        }

        Console.WriteLine("[CLEANUP] Neo4j port cleanup completed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[CLEANUP] Error during Neo4j port cleanup: {ex.Message}");
    }
}

// Helper method to kill processes using specific ports
static void KillProcessesUsingPorts(int[] ports)
{
    try
    {
        foreach (var port in ports)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains($":{port}") && line.Contains("LISTENING"))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && int.TryParse(parts[^1], out var pid))
                        {
                            try
                            {
                                var processToKill = Process.GetProcessById(pid);
                                Console.WriteLine($"[CLEANUP] Killing process {pid} using port {port}: {processToKill.ProcessName}");
                                processToKill.Kill();
                                processToKill.WaitForExit(3000);
                                Console.WriteLine($"[CLEANUP] Process {pid} terminated");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[CLEANUP] Failed to kill process {pid} on port {port}: {ex.Message}");
                            }
                        }
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[CLEANUP] Error checking ports: {ex.Message}");
    }
}

// Kill any existing SwAIvyn processes and orphaned Neo4j processes to prevent conflicts
try
{
    var currentProcess = Process.GetCurrentProcess();
    var existingProcesses = Process.GetProcessesByName("SwAIvyn")
        .Where(p => p.Id != currentProcess.Id)
        .ToList();

    if (existingProcesses.Any())
    {
        Console.WriteLine($"[STARTUP] Found {existingProcesses.Count} existing SwAIvyn process(es). Terminating...");
        foreach (var process in existingProcesses)
        {
            try
            {
                Console.WriteLine($"[STARTUP] Killing process {process.Id}...");
                process.Kill();
                process.WaitForExit(5000); // Wait up to 5 seconds
                Console.WriteLine($"[STARTUP] Process {process.Id} terminated successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[STARTUP] Failed to kill process {process.Id}: {ex.Message}");
            }
        }

        // Give processes time to fully clean up
        Console.WriteLine("[STARTUP] Waiting 3 seconds for cleanup...");
        Thread.Sleep(3000);
    }
    else
    {
        Console.WriteLine("[STARTUP] No existing SwAIvyn processes found");
    }

    // Kill all orphaned Neo4j processes (aggressive cleanup)
    Console.WriteLine("[STARTUP] Performing aggressive Neo4j process cleanup...");
    CleanupAllNeo4jProcesses();
}
catch (Exception ex)
{
    Console.WriteLine($"[STARTUP] Error checking for existing processes: {ex.Message}");
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Configure API behavior to show model validation errors
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
        new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(context.ModelState);
});

// Register the SQLite-VSS extension interceptor
// TEMPORARILY COMMENTED OUT - SQLite-VSS extension loading causes errors
// builder.Services.AddSingleton<SqliteVssExtensionInterceptor>(sp =>
//     new SqliteVssExtensionInterceptor(
//         sp.GetRequiredService<IConfiguration>()
//           .GetValue<string>("AppSettings:VssExtensionPath", "sqlite-vss.dll"),
//         sp.GetRequiredService<ISimpleLoggerService>()
//     )
// );

// Register DbContext with WAL mode and connection pooling
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    var appSettingsDataDir = configuration["AppSettings:DataDirectory"] ?? "../data";
    string resolvedDataDirectory;

    if (Path.IsPathRooted(appSettingsDataDir))
    {
        resolvedDataDirectory = appSettingsDataDir;
    }
    else
    {
        resolvedDataDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, appSettingsDataDir));
    }

    var csBuilder = new SqliteConnectionStringBuilder(connectionString);
    if (!string.IsNullOrEmpty(csBuilder.DataSource) && !Path.IsPathRooted(csBuilder.DataSource))
    {
        // Ensure the DataSource path is made absolute, relative to the application base directory
        // This handles cases like "Data Source=Sqldatabase/swai-vyn.db" or "Data Source=../data/swai-vyn.db"
        // by preserving the full relative path structure.
        csBuilder.DataSource = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, csBuilder.DataSource));
    }
    connectionString = csBuilder.ToString();
    var loggerForDb = sp.GetRequiredService<ISimpleLoggerService>(); // Assuming ISimpleLoggerService is registered as Singleton or Scoped
    loggerForDb.LogInfo($"Using resolved connection string for ApplicationDbContext: {connectionString}");

    options
        .UseSqlite(connectionString);
        // TEMPORARILY COMMENTED OUT - SQLite-VSS extension loading causes errors
        // .AddInterceptors(sp.GetRequiredService<SqliteVssExtensionInterceptor>());
});

// Add DbContextFactory for background services
builder.Services.AddDbContextFactory<ApplicationDbContext>((sp, options) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    var appSettingsDataDir = configuration["AppSettings:DataDirectory"] ?? "../data";
    string resolvedDataDirectory;

    if (Path.IsPathRooted(appSettingsDataDir))
    {
        resolvedDataDirectory = appSettingsDataDir;
    }
    else
    {
        resolvedDataDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, appSettingsDataDir));
    }

    var csBuilder = new SqliteConnectionStringBuilder(connectionString);
    if (!string.IsNullOrEmpty(csBuilder.DataSource) && !Path.IsPathRooted(csBuilder.DataSource))
    {
        // Ensure the DataSource path is made absolute, relative to the application base directory
        csBuilder.DataSource = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, csBuilder.DataSource));
    }
    connectionString = csBuilder.ToString();
    var loggerForDbFactory = sp.GetRequiredService<ISimpleLoggerService>();  // Assuming ISimpleLoggerService is registered as Singleton or Scoped
    loggerForDbFactory.LogInfo($"Using resolved connection string for ApplicationDbContext (Factory): {connectionString}");

    options
        .UseSqlite(connectionString);
        // TEMPORARILY COMMENTED OUT - SQLite-VSS extension loading causes errors
        // .AddInterceptors(sp.GetRequiredService<SqliteVssExtensionInterceptor>());
}, ServiceLifetime.Scoped);

// Add database initializer service
builder.Services.AddScoped<IDatabaseInitializer, DatabaseInitializerService>();

// Add direct database service for Users table creation
builder.Services.AddScoped<IDirectDatabaseService, DirectDatabaseService>();

// Add directory initializer service
builder.Services.AddSingleton<DirectoryInitializerService>();

// Add backup service
builder.Services.AddHostedService<SwAIvyn.HostedServices.BackupService>();

// Add search service hosted service
builder.Services.AddHostedService<SearchServiceHostedService>();

// Add conversation and folder services
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IFolderService, FolderService>();
builder.Services.AddScoped<IAgentService, AgentService>();

// Register the simple logger service first (no dependencies)
builder.Services.AddSingleton<ISimpleLoggerService, SimpleLoggerService>();

// Register the settings provider (configuration-based, no database dependency)
builder.Services.AddSingleton<ISettingsService, SettingsService>();

// Register the settings service (database-based, for user settings)
builder.Services.AddScoped<ISettingsService, SettingsService>();

// Register the configuration service
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();

// Add vector store and brain services
builder.Services.AddSingleton<IEmbeddingService, SimpleEmbeddingService>();

// Register individual vector stores
builder.Services.AddScoped<Neo4jVectorStore>();        // brain memories (scoped because it depends on INeo4jService)
builder.Services.AddHttpClient<WeaviateVectorStore>();
builder.Services.AddSingleton<WeaviateVectorStore>();     // uploads
builder.Services.AddHttpClient();

// Register vector router (orchestrator) - scoped because it depends on Neo4jVectorStore
builder.Services.AddScoped<IVectorRouter, VectorRouter>();

// Register BrainService with IVectorRouter instead of IVectorStore
builder.Services.AddScoped<IBrainService, BrainService>();

// Register HybridSearchService for calling search.py API
builder.Services.AddHttpClient<IHybridSearchService, HybridSearchService>();
builder.Services.AddHttpClient<ITtsService, ElevenLabsTtsService>();

// Add Neo4j and BrainGraph services
builder.Services.AddScoped<INeo4jService, Neo4jService>();
// builder.Services.AddSingleton<Neo4jRuntimeService>(); // Temporarily commented out
builder.Services.AddScoped<IBrainGraphService, BrainGraphService>();

// Add LLM and AI chat services
builder.Services.AddScoped<ILlmConnectorService, LlmConnectorService>();
builder.Services.AddScoped<IAiChatService, AiChatService>();

// Add memory re-indexing service
builder.Services.AddScoped<SwAIvyn.Services.Memory.MemoryReindexService>();

// Add character services
builder.Services.AddScoped<ICharacterService, CharacterService>();
builder.Services.AddScoped<CharacterCardLoaderService>();
builder.Services.AddScoped<IDefaultCharacterService, DefaultCharacterService>();

// Add document upload and processing services
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();
builder.Services.AddHttpClient<ITtsService, ElevenLabsTtsService>();

// Add agent service
builder.Services.AddScoped<IAgentService, AgentService>();

builder.Services.AddSignalR().AddJsonProtocol();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register the application monitor service
builder.Services.AddHostedService<ApplicationMonitorService>();

// Make the logger available via dependency injection
builder.Services.AddTransient<ILogger>(sp => sp.GetRequiredService<ILogger<Program>>());

// Configure standard logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", corsBuilder =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Development: Allow common dev server ports
            corsBuilder.WithOrigins(
                "http://localhost:3000",   // Create React App
                "http://localhost:5000",   // ASP.NET Core
                "http://localhost:5173",   // Vite default
                "http://localhost:5174",   // Vite alternate
                "https://localhost:5001"   // ASP.NET Core HTTPS
            );
        }
        else
        {
            // Production: Only allow the configured base URL
            var baseUrl = builder.Configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";
            corsBuilder.WithOrigins(baseUrl);
        }

        corsBuilder.AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials();
    });
});

// Build the app
var app = builder.Build();

// Get the logger service
var logger = app.Services.GetRequiredService<ISimpleLoggerService>();

// Startup health guard: warn if SQLite unavailable
logger.LogInfo("Performing startup health checks...");
using (var scope = app.Services.CreateScope())
{
    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    if (!await dbInitializer.CanConnectAsync())
    {
        logger.LogWarning("SQLite database connection check failed. Some features may not be available.");
    }
    else
    {
        logger.LogInfo("SQLite database connection check passed.");
    }    // Skip Neo4j health check completely
    logger.LogInfo("Startup health checks completed.");

    // Memory sync will be performed after Neo4j initialization
    logger.LogInfo("Memory synchronization will be performed after Neo4j initialization");
}

// Initialize directories
try
{
    logger.LogInfo("Initializing application directories...");
    var directoryInitializer = app.Services.GetRequiredService<DirectoryInitializerService>();
    directoryInitializer.InitializeDirectories();
    logger.LogInfo("Directory initialization completed successfully");
}
catch (Exception ex)
{
    logger.LogCritical("Failed to initialize directories", ex);
}

// Initialize database
try
{
    logger.LogInfo("Initializing database...");
    using (var scope = app.Services.CreateScope())
    {
        var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        await dbInitializer.InitializeAsync();
        logger.LogInfo("Database initialization completed successfully");

        // Create Users table and default user if needed
        logger.LogInfo("Ensuring Users table and default user exist...");
        var directDatabaseService = scope.ServiceProvider.GetRequiredService<IDirectDatabaseService>();
        await directDatabaseService.CreateUsersTableAndDefaultUserAsync();
        logger.LogInfo("Users table and default user initialization completed successfully");
    }
}
catch (Exception ex)
{
    logger.LogCritical("Failed to initialize database", ex);
    // Don't exit - continue with startup even if database initialization fails
    logger.LogWarning("Continuing startup despite database initialization failure");
}

logger.LogInfo("Skipping Neo4j database schema initialization - will be done after Neo4j runtime starts");

// Initialize Weaviate vector store schema
try
{
    logger.LogInfo("Initializing Weaviate vector store schema...");
    using (var scope = app.Services.CreateScope())
    {
        var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
        await vectorStore.InitializeAsync();
        logger.LogInfo("Weaviate vector store schema initialization completed successfully");
    }
}
catch (Exception ex)
{
    logger.LogWarning($"Failed to initialize Weaviate vector store schema - this is expected if Weaviate is not available. Error: {ex.Message}");
}

// --- Seed default user and AI profile on first run ---
try
{
    using (var scope = app.Services.CreateScope())    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure there is exactly one default user for this single-user application
        Guid defaultUserId;
        var existingUsers = db.Users.ToList();

        if (existingUsers.Count == 0)
        {
            // No users exist, create the default user
            logger.LogInfo("No users found. Creating default user for single-user application...");

            var defaultUser = new SwAIvyn.Data.Entities.AppUser
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Username = "Default User",
                PasswordHash = "", // No password needed for single-user app
                PINCode = "",
                RecoveryPhrase = "",
                CreatedAt = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow
            };

            db.Users.Add(defaultUser);
            db.SaveChanges();
            defaultUserId = defaultUser.Id;
            logger.LogInfo($"Created default user: {defaultUserId}");
        }
        else if (existingUsers.Count == 1)
        {
            // Exactly one user exists, use it
            defaultUserId = existingUsers[0].Id;
            logger.LogInfo($"Using existing single user: {defaultUserId}");
        }
        else
        {
            // Multiple users exist, consolidate to first one and log warning
            logger.LogWarning($"Multiple users found ({existingUsers.Count}). This is a single-user application. Using first user: {existingUsers[0].Id}");
            defaultUserId = existingUsers[0].Id;

            // Optionally, you could migrate data from other users to the first one here
            // For now, just use the first user
        }

        // COMMENTED OUT: Hardcoded character creation - only load from database
        // If no AI character profiles exist, create a default one linked to our user
        // if (!db.Avatars.Any())
        // {
        //     logger.LogInfo("No AI profiles found. Creating default AI profile...");
        //     db.Avatars.Add(new SwAIvyn.Data.Entities.AvatarInfo
        //     {
        //         Id = Guid.NewGuid(),
        //         UserId = defaultUserId, // Use our confirmed user ID
        //         Name = "Default AI",
        //         ImagePath = "",
        //         Personality = "Friendly and helpful AI assistant.",
        //         VoiceSettings = "default",
        //         Description = "A helpful AI assistant ready to chat with you.",
        //         Scenario = "General conversation",
        //         FirstMessage = "Hello! I'm your AI assistant. How can I help you today?",
        //         MessageExample = "",
        //         SystemPrompt = "You are a helpful, harmless, and honest AI assistant.",
        //         PostHistoryInstructions = "",
        //         AlternateGreetings = "[]",
        //         Tags = "[]",
        //         Creator = "SwAIvyn",
        //         CreatorNotes = "Default AI assistant character",
        //         CharacterVersion = "1.0",
        //         Talkativeness = 0.5f,
        //         IsFavorite = false,
        //         Extensions = "{}",
        //         YamlProfile = "",
        //         CreatedAt = DateTime.UtcNow,
        //         LastModified = DateTime.UtcNow
        //     });
        //
        //     db.SaveChanges();
        //     logger.LogInfo("Seeded default AI profile.");
        // }

        // Initialize default settings for the user
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settingsService.InitializeDefaultSettingsAsync(defaultUserId);

        // Load character cards from filesystem
        logger.LogInfo("Loading character cards from filesystem...");
        var characterCardLoader = scope.ServiceProvider.GetRequiredService<CharacterCardLoaderService>();
        await characterCardLoader.LoadCharacterCardsAsync();
        logger.LogInfo("Character card loading completed");

        // Ensure GLaDOS default character is loaded
        logger.LogInfo("Ensuring GLaDOS default character is loaded...");
        var defaultCharacterService = scope.ServiceProvider.GetRequiredService<IDefaultCharacterService>();
        await defaultCharacterService.EnsureDefaultCharacterAsync();
        logger.LogInfo("GLaDOS default character loaded successfully");

        // Create a welcome conversation if no conversations exist
        if (!db.Conversations.Any())
        {
            logger.LogInfo("No conversations found. Creating welcome conversation...");

            var welcomeConversation = new SwAIvyn.Data.Entities.Conversation
            {
                Id = Guid.NewGuid(),
                UserId = defaultUserId,
                Title = "Welcome to SwAIvyn! 🎉",
                Summary = "Getting started guide and tutorial",
                Status = "active",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                LastOpenUtc = DateTime.UtcNow,
                Tags = "welcome,tutorial,getting-started"
            };

            db.Conversations.Add(welcomeConversation);
            db.SaveChanges();

            // Add welcome messages to the conversation
            var welcomeMessages = new[]
            {
                new { Role = "assistant", Content = "# Welcome to SwAIvyn! 🎉\n\nHello! I'm your AI assistant, and I'm excited to help you get started with SwAIvyn. This is a powerful AI chat application that lets you:\n\n✨ **Chat with multiple AI engines** (Ollama, LM Studio)\n📁 **Organize conversations** in folders\n🧠 **Store memories** for context\n🎭 **Create AI personas** with different personalities\n📊 **Search through your chat history**\n\nLet me show you around!" },
                new { Role = "assistant", Content = "## 🔧 Getting Started\n\n**1. Choose your AI Engine:**\n- Go to Settings to configure Ollama or LM Studio\n- Default is Ollama (http://localhost:11434)\n- You can switch between engines anytime\n\n**2. Your settings are automatically saved:**\n- When you change the AI engine, it persists across sessions\n- All your preferences are stored locally\n- Your conversations are saved automatically\n\n**3. Try these features:**\n- Create folders to organize conversations\n- Ask me anything - I'll remember our context\n- Use the search to find old conversations" },
                new { Role = "assistant", Content = "## 🎯 Quick Tips\n\n**Settings Persistence:**\n- ✅ All settings save automatically\n- ✅ Your AI engine choice is remembered\n- ✅ Conversations persist between sessions\n- ✅ No data is lost when you refresh or restart\n\n**Current Configuration:**\n- 🤖 Default AI Engine: Ollama\n- 💾 Database: SQLite with WAL mode\n- 📍 Data Location: `../data/swai-vyn.db`\n- 🔍 Vector Search: Available (when SQLite-VSS loads)\n\n**Ready to start?** Try asking me a question, or feel free to delete this conversation once you're comfortable with the app!" }
            };

            foreach (var message in welcomeMessages)
            {
                var chatIndex = new SwAIvyn.Data.Entities.ChatIndex
                {
                    Id = Guid.NewGuid(),
                    ConversationId = welcomeConversation.Id,
                    Role = message.Role,
                    FilePath = $"welcome_{Guid.NewGuid()}.json",
                    CreatedUtc = DateTime.UtcNow
                };

                db.ChatIndices.Add(chatIndex);
            }

            db.SaveChanges();
            logger.LogInfo("Created welcome conversation with tutorial messages.");
        }
    }
}
catch (Exception ex)
{
    logger.LogError("Failed to seed default user and AI profile", ex);
    logger.LogError($"Error details: {ex.Message}");

    if (ex.InnerException != null)
    {
        logger.LogError($"Inner exception: {ex.InnerException.Message}");
    }
}

// Initialize vector stores
try
{
    logger.LogInfo("Initializing Weaviate vector store...");
    var weaviateStore = app.Services.GetRequiredService<WeaviateVectorStore>();
    await weaviateStore.InitializeAsync();
    logger.LogInfo("Weaviate vector store initialization completed successfully");
}
catch (Exception ex)
{
    logger.LogError($"Failed to initialize Weaviate vector store. Upload search will not be available. Error: {ex.Message}");
}

// Initialize Neo4j runtime and service
var neo4jEmbedded = builder.Configuration.GetValue<bool>("AppSettings:Neo4jEmbedded", false);
var requireNeo4j = builder.Configuration.GetValue<bool>("AppSettings:RequireNeo4j", false);
logger.LogInfo($"Neo4j embedded mode is {(neo4jEmbedded ? "enabled" : "disabled")}");
logger.LogInfo($"Neo4j required: {requireNeo4j}");

try
{
    // Get Neo4j services
    // var neo4jRuntime = app.Services.GetRequiredService<Neo4jRuntimeService>(); // Temporarily commented out

    // Initialize Neo4j runtime (extract and start Neo4j)
    logger.LogInfo("Skipping Neo4j runtime initialization for now due to build issues.");
    // await neo4jRuntime.InitializeAsync(); // Temporarily commented out

    // Give Neo4j time to fully start up before connecting
    if (neo4jEmbedded)
    {
        logger.LogInfo("Waiting 30 seconds for Neo4j to fully start up...");
        await Task.Delay(TimeSpan.FromSeconds(30));
        logger.LogInfo("Neo4j startup delay completed, proceeding with service initialization...");
    }

    using (var scope = app.Services.CreateScope())
    {
        var neo4jService = scope.ServiceProvider.GetRequiredService<INeo4jService>();

        // Initialize Neo4j service
        logger.LogInfo("Initializing Neo4j service...");
        await neo4jService.InitializeAsync();
        logger.LogInfo("Neo4j service initialization completed successfully");


        // Check if Neo4j is available
        var graphOk = await neo4jService.PingAsync();
        if (!graphOk && requireNeo4j)
        {
            logger.LogCritical("Startup aborted: Neo4j service unavailable.");
            Environment.Exit(1);
        }

        if (!graphOk)
        {
            logger.LogWarning("Neo4j offline - graph features disabled until reconnection.");
        }
    }
}
catch (Exception ex)
{
    if (requireNeo4j)
    {
        logger.LogCritical("Startup aborted: Neo4j service unavailable.", ex);
        Environment.Exit(1);
    }
    else
    {
        logger.LogWarning($"Failed to initialize Neo4j service. Graph functionality will not be available. Error: {ex.Message}");
    }
}

// Initialize Neo4j vector store after Neo4j runtime is ready
try
{
    logger.LogInfo("Initializing Neo4j vector store...");
    var neo4jStore = app.Services.GetRequiredService<Neo4jVectorStore>();
    await neo4jStore.InitializeAsync();
    logger.LogInfo("Neo4j vector store initialization completed successfully");
}
catch (Exception ex)
{
    logger.LogError($"Failed to initialize Neo4j vector store. Memory search will not be available. Error: {ex.Message}");
}

// Perform memory synchronization after Neo4j is fully initialized
try
{
    logger.LogInfo("Performing memory synchronization after Neo4j initialization...");
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brainGraphService = scope.ServiceProvider.GetRequiredService<IBrainGraphService>();

        const string defaultUserId = "00000000-0000-0000-0000-000000000001";
        var userId = Guid.Parse(defaultUserId);

        // Check sync status first
        var sqliteMemories = await dbContext.Memories
            .Where(m => m.UserId == userId)
            .ToListAsync();

        var neo4jMemoryIds = new List<Guid>();
        try
        {
            neo4jMemoryIds = await brainGraphService.GetAllMemoryIdsAsync(userId);
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Failed to get Neo4j memories during startup sync: {ex.Message}");
        }

        var sqliteIds = sqliteMemories.Select(m => m.Id).ToHashSet();
        var neo4jIds = neo4jMemoryIds.ToHashSet();
        var missingInNeo4j = sqliteIds.Except(neo4jIds).ToList();

        if (missingInNeo4j.Count > 0)
        {
            logger.LogInfo($"Found {missingInNeo4j.Count} memories missing from Neo4j. Performing repair...");

            int successCount = 0;
            int failureCount = 0;

            foreach (var memoryId in missingInNeo4j)
            {
                var memory = sqliteMemories.First(m => m.Id == memoryId);

                try
                {
                    var metadata = new Dictionary<string, string>
                    {
                        { "category", memory.Category ?? "general" },
                        { "userId", memory.UserId.ToString() },
                        { "isShared", memory.IsShared.ToString() },
                        { "createdAt", memory.CreatedAt.ToString("O") },
                        { "source", "startup-sync" }
                    };

                    var success = await brainGraphService.AddMemoryAsync(memory.Id, memory.Content, metadata);

                    if (success)
                    {
                        successCount++;
                    }
                    else
                    {
                        failureCount++;
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    logger.LogWarning($"Failed to sync memory {memory.Id} during startup: {ex.Message}");
                }
            }

            logger.LogInfo($"Startup memory repair completed: {successCount} successful, {failureCount} failed");
        }
        else
        {
            logger.LogInfo("Memory databases are already in sync.");
        }
    }
}
catch (Exception ex)
{
    logger.LogError("Failed to perform startup memory sync", ex);
    // Don't fail startup for sync issues
}

// Set up global exception handler
AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
{
    var exception = args.ExceptionObject as Exception;
    var isTerminating = args.IsTerminating;

    logger.LogCritical(
        $"Unhandled exception: {(isTerminating ? "Application is terminating" : "Application will continue")}",
        exception);

    // If the application is terminating, ensure logs are flushed
    if (isTerminating)
    {
        try
        {
            // Give some time for logs to be written
            Thread.Sleep(1000);
        }
        catch
        {
            // Last resort - if we can't even sleep
            Console.WriteLine("Failed to wait for logs to be written. Application is terminating.");
        }
    }
};

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add request logging middleware for debugging
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    var method = context.Request.Method;
    logger.LogInfo($"[REQUEST] {method} {path}");

    await next();

    logger.LogInfo($"[RESPONSE] {method} {path} -> {context.Response.StatusCode}");
});

// Add global exception handler middleware
app.UseGlobalExceptionHandler();

app.UseHttpsRedirection();
app.UseDefaultFiles(); // Add this to serve index.html by default

// Configure static files with cache-busting headers for development
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Add cache-busting headers to prevent browser caching during development
        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
        ctx.Context.Response.Headers.Append("Expires", "0");
    }
});

app.UseRouting();
app.UseCors("CorsPolicy");
app.UseAuthorization();

// Map API controllers first
logger.LogInfo("[ROUTING] Mapping controllers...");
app.MapControllers();
logger.LogInfo("[ROUTING] Controllers mapped successfully");

app.MapHub<ChatHub>("/hubs/chat").RequireCors("CorsPolicy");
app.MapHub<VoiceHub>("/hubs/voice").RequireCors("CorsPolicy");
app.MapHub<NotificationHub>("/hubs/notification").RequireCors("CorsPolicy");

// Add health endpoint for Neo4j
app.MapGet("/api/health/neo4j", async (INeo4jService neo4jService) =>
    Results.Ok(await neo4jService.GetStatusAsync()))
    .RequireCors("CorsPolicy");

// Add memory debug endpoints
app.MapPost("/api/debug/memory-search", async (
    HttpContext context,
    [FromBody] MemorySearchRequest request,
    IBrainService brainService,
    IVectorRouter vectorRouter,
    Neo4jVectorStore neo4jStore,
    IEmbeddingService embeddingService,
    ISimpleLoggerService logger) =>
{
    try
    {
        logger.LogInfo($"[MEMORY DEBUG] Testing memory search for: {request.Query}");
        
        // Use a test user ID for debug purposes
        var testUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        
        // Test BrainService search
        logger.LogInfo("[MEMORY DEBUG] Testing BrainService.SearchAsync...");
        var brainResults = await brainService.SearchAsync(request.Query, request.Limit ?? 5);
        logger.LogInfo($"[MEMORY DEBUG] BrainService returned {brainResults.Count()} results");
        
        // Test Neo4j vector store directly (need to generate embedding first)
        logger.LogInfo("[MEMORY DEBUG] Testing Neo4jVectorStore.SearchAsync...");
        var queryEmbedding = await embeddingService.EmbedTextAsync(request.Query);
        var neo4jResults = await neo4jStore.SearchAsync(queryEmbedding, request.Limit ?? 5);
        logger.LogInfo($"[MEMORY DEBUG] Neo4jVectorStore returned {neo4jResults.Count()} results");
        
        // Test VectorRouter
        logger.LogInfo("[MEMORY DEBUG] Testing VectorRouter.SearchMemoryAsync...");
        var routerResults = await vectorRouter.SearchMemoryAsync(testUserId, request.Query, request.Limit ?? 5);
        logger.LogInfo($"[MEMORY DEBUG] VectorRouter returned {routerResults.Count()} results");        
        return Results.Ok(new
        {
            query = request.Query,
            brainService = new
            {
                count = brainResults.Count(),
                results = brainResults.Select(r => new
                {
                    id = r.Id,
                    score = r.Score,
                    content = r.Metadata?.GetValueOrDefault("content", ""),
                    metadata = r.Metadata
                }).ToList()
            },
            neo4jVectorStore = new
            {
                count = neo4jResults.Count(),
                results = neo4jResults.Select(r => new
                {
                    id = r.Id,
                    score = r.Score,
                    content = r.Metadata?.GetValueOrDefault("content", ""),
                    metadata = r.Metadata
                }).ToList()
            },
            vectorRouter = new
            {
                count = routerResults.Count(),
                results = routerResults.Select(r => new
                {
                    id = r.Id,
                    score = r.Score,
                    content = r.Metadata?.GetValueOrDefault("content", ""),
                    metadata = r.Metadata
                }).ToList()
            }
        });
    }
    catch (Exception ex)
    {
        logger.LogError($"[MEMORY DEBUG] Error testing memory search: {ex.Message}", ex);
        return Results.BadRequest(new { error = ex.Message, stackTrace = ex.StackTrace });
    }
})
.RequireCors("CorsPolicy");

app.MapGet("/api/debug/memory-count", async (
    IBrainService brainService,
    Neo4jVectorStore neo4jStore,
    IEmbeddingService embeddingService,
    ISimpleLoggerService logger) =>
{
    try
    {
        logger.LogInfo("[MEMORY DEBUG] Getting memory counts...");
        
        // Get all memories from BrainService
        var allBrainMemories = await brainService.SearchAsync("", 1000); // Large limit to get all
        logger.LogInfo($"[MEMORY DEBUG] BrainService has {allBrainMemories.Count()} memories");
        
        // Try to get count from Neo4j directly
        var neo4jCount = 0;
        try
        {
            var emptyEmbedding = await embeddingService.EmbedTextAsync("");
            var allNeo4jResults = await neo4jStore.SearchAsync(emptyEmbedding, 1000);
            neo4jCount = allNeo4jResults.Count();
            logger.LogInfo($"[MEMORY DEBUG] Neo4jVectorStore has {neo4jCount} memories");
        }
        catch (Exception ex)
        {
            logger.LogError($"[MEMORY DEBUG] Failed to query Neo4j: {ex.Message}");
        }
        
        return Results.Ok(new
        {
            brainService = allBrainMemories.Count(),
            neo4jVectorStore = neo4jCount,
            brainMemories = allBrainMemories.Select(r => new
            {
                id = r.Id,
                score = r.Score,
                content = r.Metadata?.GetValueOrDefault("content", ""),
                metadata = r.Metadata
            }).Take(10).ToList() // Show first 10
        });
    }
    catch (Exception ex)
    {
        logger.LogError($"[MEMORY DEBUG] Error getting memory counts: {ex.Message}", ex);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.RequireCors("CorsPolicy");

// Fallback to index.html for SPA routes - CRITICAL for React Router to work
// Use a pattern that excludes API routes
logger.LogInfo("[ROUTING] Setting up SPA fallback...");
app.MapFallback(context =>
{
    var path = context.Request.Path.Value;
    logger.LogInfo($"[FALLBACK] Processing path: {path}");

    // Don't fallback for API routes, hubs, or static files
    if (path != null && (path.StartsWith("/api/") || path.StartsWith("/hubs/") || path.Contains('.')))
    {
        logger.LogInfo($"[FALLBACK] Rejecting path (API/hub/static): {path}");
        context.Response.StatusCode = 404;
        return Task.CompletedTask;
    }

    logger.LogInfo($"[FALLBACK] Serving index.html for SPA route: {path}");
    // Serve index.html for SPA routes
    context.Response.ContentType = "text/html";
    return context.Response.SendFileAsync("wwwroot/index.html");
});
logger.LogInfo("[ROUTING] SPA fallback configured");

// Set URLs explicitly
if (app.Urls.Count == 0)
{
    app.Urls.Add("http://localhost:5000");
}

// Log application startup
logger.LogInfo($"Application starting on URLs: {string.Join(", ", app.Urls)}");

// Register shutdown handler
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInfo("Application is shutting down...");

    // Gracefully shut down Neo4j
    // try
    // {
    //     var neo4jRuntime = app.Services.GetRequiredService<Neo4jRuntimeService>();
    //     neo4jRuntime.Dispose();
    //     logger.LogInfo("Neo4j runtime shut down successfully");
    // }
    // catch (Exception ex)
    // {
    //     logger.LogError("Failed to shut down Neo4j runtime", ex);
    // }
});

// Create logs directory if it doesn't exist
var logDir = builder.Configuration["AppSettings:LogDirectory"] ?? "logs";
if (!Directory.Exists(logDir))
{
    Directory.CreateDirectory(logDir);
}

// Open browser window when application starts (only in production)
if (!app.Environment.IsDevelopment())
{
    var hostUrl = app.Configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";

    // Start the application in the background
    var hostTask = app.RunAsync();

    // Wait a moment for the server to start
    Thread.Sleep(2000);

    // Open the browser (Windows only)
    try
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            logger.LogInfo($"Opening browser at URL: {hostUrl}");
            var psi = new ProcessStartInfo
            {
                FileName = hostUrl,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        else
        {
            logger.LogInfo($"Application started at URL: {hostUrl} (browser auto-open disabled on non-Windows platforms)");
        }
    }
    catch (Exception ex)
    {
        logger.LogError("Failed to open browser", ex);
    }

    // Wait for the host to stop
    await hostTask;

    // Log application shutdown
    logger.LogInfo("Application has stopped");
}
else
{
    logger.LogInfo("Running in development mode");
    logger.LogInfo("Application URLs: " + string.Join(", ", app.Urls));
    app.Run();
    logger.LogInfo("Application has stopped");
}

/// <summary>
/// Request model for memory search debugging
/// </summary>
public class MemorySearchRequest
{
    /// <summary>
    /// The search query to test
    /// </summary>
    public required string Query { get; set; }
    
    /// <summary>
    /// Maximum number of results to return (optional, defaults to 5)
    /// </summary>
    public int? Limit { get; set; }
}
