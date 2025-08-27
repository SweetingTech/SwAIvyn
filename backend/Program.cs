using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using SQLitePCL;
using YamlDotNet.Serialization;

using SwAIvyn.Data;
using SwAIvyn.Hubs;
using SwAIvyn.Middleware;
using SwAIvyn.Configuration;
using SwAIvyn.Services;
using SwAIvyn.Services.VectorStore;
using SwAIvyn.Services.Graph;
using SwAIvyn.HostedServices;

// Initialize SQLitePCL.raw for extension loading
Batteries_V2.Init();

// ─── Helpers for process cleanup ──────────────────────────────────────────────

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
            if (process == null) continue;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains($":{port}") && line.Contains("LISTENING"))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && int.TryParse(parts[^1], out var pid))
                    {
                        try
                        {
                            var p = Process.GetProcessById(pid);
                            Console.WriteLine($"[CLEANUP] Killing process {pid} using port {port}: {p.ProcessName}");
                            p.Kill();
                            p.WaitForExit(3000);
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
    catch (Exception ex)
    {
        Console.WriteLine($"[CLEANUP] Error checking ports: {ex.Message}");
    }
}

static void CleanupAllRequiredPorts()
{
    try
    {
        Console.WriteLine("[CLEANUP] Starting targeted Neo4j and Fish Speech port cleanup...");
        KillProcessesUsingPorts(new[] { 7474, 7687, 8081 });

        var neo4jProcesses = Process.GetProcesses()
            .Where(p => p.ProcessName.Contains("neo4j", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (neo4jProcesses.Any())
        {
            Console.WriteLine($"[CLEANUP] Found {neo4jProcesses.Count} Neo4j-named processes to terminate");
            foreach (var process in neo4jProcesses)
            {
                try
                {
                    Console.WriteLine($"[CLEANUP] Killing Neo4j process {process.Id}: {process.ProcessName}");
                    process.Kill();
                    process.WaitForExit(3000);
                    Console.WriteLine($"[CLEANUP] Process {process.Id} terminated");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CLEANUP] Failed to kill process {process.Id}: {ex.Message}");
                }
            }
        }
        else
        {
            Console.WriteLine("[CLEANUP] No Neo4j-named processes found");
        }

        Console.WriteLine("[CLEANUP] Port cleanup completed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[CLEANUP] Error during port cleanup: {ex.Message}");
    }
}

// ─── Pre-startup: kill any existing SwAIvyn and Neo4j processes ───────────────
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    try
    {
        var me = Process.GetCurrentProcess();
        var others = Process.GetProcessesByName("SwAIvyn")
                            .Where(p => p.Id != me.Id)
                            .ToList();

        if (others.Any())
        {
            Console.WriteLine($"[STARTUP] Found {others.Count} existing SwAIvyn process(es). Terminating...");
            foreach (var p in others)
            {
                try
                {
                    Console.WriteLine($"[STARTUP] Killing process {p.Id}...");
                    p.Kill();
                    p.WaitForExit(5000);
                    Console.WriteLine($"[STARTUP] Process {p.Id} terminated successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[STARTUP] Failed to kill process {p.Id}: {ex.Message}");
                }
            }
            Console.WriteLine("[STARTUP] Waiting 3 seconds for cleanup...");
            Thread.Sleep(3000);
        }
        else
        {
            Console.WriteLine("[STARTUP] No existing SwAIvyn processes found");
        }

        Console.WriteLine("[STARTUP] Performing aggressive port cleanup (Neo4j, Fish Speech)...");
        CleanupAllRequiredPorts();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[STARTUP] Error during Windows-specific startup cleanup: {ex.Message}");
    }
}
else
{
    Console.WriteLine("[STARTUP] Skipping Windows-specific port/process cleanup inside container.");
}

// ─── Build host ────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Configure API behavior to show model validation errors
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
        new BadRequestObjectResult(context.ModelState);
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
    var cfg = sp.GetRequiredService<IConfiguration>();
    var conn = cfg.GetConnectionString("DefaultConnection");
    var dataDir = cfg["AppSettings:DataDirectory"] ?? "../data";

    var resolvedDir = Path.IsPathRooted(dataDir)
        ? dataDir
        : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, dataDir));

    var csb = new SqliteConnectionStringBuilder(conn);
    if (!string.IsNullOrEmpty(csb.DataSource) && !Path.IsPathRooted(csb.DataSource))
    {
        csb.DataSource = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, csb.DataSource));
    }
    conn = csb.ToString();

    var logDb = sp.GetRequiredService<ISimpleLoggerService>();
    logDb.LogInfo($"Using resolved connection string for ApplicationDbContext: {conn}");

    options.UseSqlite(conn);
    // .AddInterceptors(sp.GetRequiredService<SqliteVssExtensionInterceptor>());
}, ServiceLifetime.Scoped);

// Add DbContextFactory for background services
builder.Services.AddDbContextFactory<ApplicationDbContext>((sp, options) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var conn = cfg.GetConnectionString("DefaultConnection");
    var dataDir = cfg["AppSettings:DataDirectory"] ?? "../data";

    var resolvedDir = Path.IsPathRooted(dataDir)
        ? dataDir
        : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, dataDir));

    var csb = new SqliteConnectionStringBuilder(conn);
    if (!string.IsNullOrEmpty(csb.DataSource) && !Path.IsPathRooted(csb.DataSource))
    {
        csb.DataSource = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, csb.DataSource));
    }
    conn = csb.ToString();

    var logFactory = sp.GetRequiredService<ISimpleLoggerService>();
    logFactory.LogInfo($"Using resolved connection string for ApplicationDbContext (Factory): {conn}");

    options.UseSqlite(conn);
    // .AddInterceptors(sp.GetRequiredService<SqliteVssExtensionInterceptor>());
}, ServiceLifetime.Scoped);

// Secondary DbContext (if needed)
builder.Services.AddDbContext<SwAIvynDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Core services
builder.Services.AddScoped<IDatabaseInitializer, DatabaseInitializerService>();
builder.Services.AddScoped<IDirectDatabaseService, DirectDatabaseService>();
builder.Services.AddSingleton<DirectoryInitializerService>();
builder.Services.AddSingleton<ISimpleLoggerService, SimpleLoggerService>();
builder.Services.AddSingleton<ITerminalLoggerService, TerminalLoggerService>();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

// Hosted services
builder.Services.AddHostedService<SwAIvyn.HostedServices.BackupService>(); // Explicitly specify namespace

// Optional hosted services controlled by AppSettings flags
var enableSearchService = builder.Configuration.GetValue<bool>("AppSettings:EnableSearchService", false);
if (enableSearchService)
{
    builder.Services.AddHostedService<SearchServiceHostedService>();
}

// Register FishSpeechHostedService as singleton first, then as hosted service
builder.Services.AddSingleton<FishSpeechHostedService>();
builder.Services.AddHostedService<FishSpeechHostedService>(provider => provider.GetRequiredService<FishSpeechHostedService>());

// Google Workspace optional
var enableGoogleWorkspace = builder.Configuration.GetValue<bool>("AppSettings:EnableGoogleWorkspace", false);
if (enableGoogleWorkspace)
{
    builder.Services.AddHostedService<GoogleWorkspaceHostedService>();
}

builder.Services.AddHostedService<ApplicationMonitorService>();

// App services
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IFolderService, FolderService>();

// Settings
builder.Services.Configure<FishSpeechOptions>(builder.Configuration.GetSection(FishSpeechOptions.SectionName));
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();

// Embedding + Vector stores
builder.Services.AddScoped<IEmbeddingService, SimpleEmbeddingService>();
builder.Services.AddScoped<Neo4jVectorStore>();
builder.Services.AddHttpClient<WeaviateVectorStore>();
builder.Services.AddSingleton<WeaviateVectorStore>();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("WorkerApi");
builder.Services.AddScoped<IVectorRouter, VectorRouter>();
builder.Services.AddScoped<IBrainService, BrainService>();

// Hybrid search
builder.Services.AddHttpClient<IHybridSearchService, HybridSearchService>();

// TTS
builder.Services.AddHttpClient<ElevenLabsTtsService>();
builder.Services.AddScoped<FishSpeechTtsService>();
builder.Services.AddHttpClient<FishSpeechTtsService>();

// Google Workspace
builder.Services.AddHttpClient<IGoogleWorkspaceService, GoogleWorkspaceService>();

// Graph & brain
builder.Services.AddScoped<INeo4jService, Neo4jService>();
builder.Services.AddSingleton<Neo4jRuntimeService>();
builder.Services.AddScoped<IBrainGraphService, BrainGraphService>();

// AI chat + agents
builder.Services.AddHttpClient<ILlmConnectorService, LlmConnectorService>();
builder.Services.AddScoped<IAiChatService, AiChatService>();
builder.Services.AddScoped<SwAIvyn.Services.Memory.MemoryReindexService>();
builder.Services.AddScoped<ICharacterService, CharacterService>();
builder.Services.AddScoped<CharacterCardLoaderService>();
builder.Services.AddScoped<IDefaultCharacterService, DefaultCharacterService>();
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddSingleton<IModuleService, ModuleService>();

// SignalR, Swagger, CORS
builder.Services.AddSignalR().AddJsonProtocol();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransient<ILogger>(sp => sp.GetRequiredService<ILogger<Program>>());

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddCors(opts =>
{
    opts.AddPolicy("CorsPolicy", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5000",
                "http://localhost:5173",
                "http://localhost:5174",
                "https://localhost:5001");
        }
        else
        {
            var baseUrl = builder.Configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000";
            policy.WithOrigins(baseUrl);
        }
        policy.AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

var app = builder.Build();

var logger = app.Services.GetRequiredService<ISimpleLoggerService>();

// Raise ThreadPool min threads early to avoid starvation during cold start
ThreadPool.GetMinThreads(out var curWorkers, out var curIo);
var desiredMin = Math.Max(256, Environment.ProcessorCount * 8);
ThreadPool.SetMinThreads(Math.Max(curWorkers, desiredMin), Math.Max(curIo, desiredMin));
logger.LogInfo($"ThreadPool min threads set to at least {desiredMin} (workers/io: {curWorkers}/{curIo})");

// Start terminal logging to mirror console output to log files
var terminalLogger = app.Services.GetRequiredService<ITerminalLoggerService>();
terminalLogger.StartLogging();
logger.LogInfo($"Terminal logging started. Log file: {terminalLogger.GetLogFilePath()}");

// FishSpeechHostedService will start automatically as a hosted service
logger.LogInfo("FishSpeechHostedService registered and will start automatically.");

logger.LogInfo("Starting HTTP server immediately - deferring heavy initialization to background tasks");

// Track initialization status for health checks
var initializationStatus = new Dictionary<string, bool>
{
    { "directories", false },
    { "database", false },
    { "migrations", false },
    { "users", false },
    { "characters", false },
    { "neo4j", false },
    { "weaviate", false }
};

// Initialize directories (fast operation - do synchronously)
try
{
    logger.LogInfo("Initializing application directories...");
    app.Services.GetRequiredService<DirectoryInitializerService>().InitializeDirectories();
    initializationStatus["directories"] = true;
    logger.LogInfo("Directory initialization completed successfully");
}
catch (Exception ex)
{
    logger.LogCritical("Failed to initialize directories", ex);
}

// Defer critical initialization to background tasks; Kestrel will bind immediately.
logger.LogInfo("[INIT] Deferring critical initialization to background tasks. /healthz will return 503 until ready.");

// Defer all heavy initialization to background tasks
_ = Task.Factory.StartNew(async () =>
{
    try
    {
        // Database migrations (can be slow)
        logger.LogInfo("[BG] Applying Entity Framework Core migrations...");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await Task.Run(() => db.Database.Migrate());
            initializationStatus["migrations"] = true;
            logger.LogInfo("[BG] Database migrations completed successfully");
        }

        // Database initialization
        logger.LogInfo("[BG] Initializing database...");
        using (var scope = app.Services.CreateScope())
        {
            var dbInit = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            await dbInit.InitializeAsync();
            initializationStatus["database"] = true;
            logger.LogInfo("[BG] Database initialization completed successfully");

            logger.LogInfo("[BG] Ensuring Users table and default user exist...");
            await scope.ServiceProvider.GetRequiredService<IDirectDatabaseService>()
                       .CreateUsersTableAndDefaultUserAsync();
            initializationStatus["users"] = true;
            logger.LogInfo("[BG] Users table and default user initialization completed successfully");
        }

        // Character and user setup (can be slow with filesystem operations)
        logger.LogInfo("[BG] Setting up default user and AI profile...");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Guid defaultUserId;
            var existingUsers = await db.Users.ToListAsync();

            if (existingUsers.Count == 0)
            {
                logger.LogInfo("[BG] No users found. Creating default user for single-user application...");
                var defaultUser = new SwAIvyn.Data.Entities.AppUser
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Username = "Default User",
                    PasswordHash = "",
                    PINCode = "",
                    RecoveryPhrase = "",
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                };
                db.Users.Add(defaultUser);
                await db.SaveChangesAsync();
                defaultUserId = defaultUser.Id;
                logger.LogInfo($"[BG] Created default user: {defaultUserId}");
            }
            else if (existingUsers.Count == 1)
            {
                defaultUserId = existingUsers[0].Id;
                logger.LogInfo($"[BG] Using existing single user: {defaultUserId}");
            }
            else
            {
                defaultUserId = existingUsers[0].Id;
                logger.LogWarning($"[BG] Multiple users found ({existingUsers.Count}). Using first user: {defaultUserId}");
            }

            // Initialize default settings
            await scope.ServiceProvider.GetRequiredService<ISettingsService>()
                       .InitializeDefaultSettingsAsync(defaultUserId);

            // Load character cards (can be slow with filesystem I/O)
            logger.LogInfo("[BG] Loading character cards from filesystem...");
            await scope.ServiceProvider.GetRequiredService<CharacterCardLoaderService>()
                       .LoadCharacterCardsAsync();
            logger.LogInfo("[BG] Character card loading completed");

            // Ensure default character
            logger.LogInfo("[BG] Ensuring GLaDOS default character is loaded...");
            await scope.ServiceProvider.GetRequiredService<IDefaultCharacterService>()
                       .EnsureDefaultCharacterAsync();
            initializationStatus["characters"] = true;
            logger.LogInfo("[BG] GLaDOS default character loaded successfully");
            logger.LogInfo("[READY] All init tasks completed — healthz will return 200.");

            // Create welcome conversation
            if (!await db.Conversations.AnyAsync())
            {
                logger.LogInfo("[BG] No conversations found. Creating welcome conversation...");
                var convo = new SwAIvyn.Data.Entities.Conversation
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
                db.Conversations.Add(convo);
                await db.SaveChangesAsync();

                var messages = new[]
                {
                    new { Role="assistant", Content=@"# Welcome to SwAIvyn! 🎉

Hello! I'm your AI assistant, and I'm excited to help you get started with SwAIvyn. This is a powerful AI chat application that lets you:

✨ **Chat with multiple AI engines** (Ollama, LM Studio)
📁 **Organize conversations** in folders
🧠 **Store memories** for context
🎭 **Create AI personas** with different personalities
📊 **Search through your chat history**

Let me show you around!" },
                    new { Role="assistant", Content=@"## 🔧 Getting Started

**1. Choose your AI Engine:**
- Go to Settings to configure Ollama or LM Studio
- Default is Ollama (http://localhost:11434)
- You can switch between engines anytime

**2. Your settings are automatically saved:**
- When you change the AI engine, it persists across sessions
- All your preferences are stored locally
- Your conversations are saved automatically

**3. Try these features:**
- Create folders to organize conversations
- Ask me anything - I'll remember our context
- Use the search to find old conversations" },
                    new { Role="assistant", Content=@"## 🎯 Quick Tips

**Settings Persistence:**
- ✅ All settings save automatically
- ✅ Your AI engine choice is remembered
- ✅ Conversations persist between sessions
- ✅ No data is lost when you refresh or restart

**Current Configuration:**
- 🤖 Default AI Engine: Ollama
- 💾 Database: SQLite with WAL mode
- 📍 Data Location: `../data/swai-vyn.db`
- 🔍 Vector Search: Available (when SQLite-VSS loads)

**Ready to start?** Try asking me a question, or feel free to delete this conversation once you're comfortable with the app!" }
                };

                foreach (var msg in messages)
                {
                    db.ChatIndices.Add(new SwAIvyn.Data.Entities.ChatIndex
                    {
                        Id = Guid.NewGuid(),
                        ConversationId = convo.Id,
                        Content = msg.Content,
                        Embedding = "", // Assuming Embedding might also be non-nullable
                        Role = msg.Role,
                        FilePath = $"welcome_{Guid.NewGuid()}.json",
                        CreatedUtc = DateTime.UtcNow
                    });
                }
                await db.SaveChangesAsync();
                logger.LogInfo("[BG] Created welcome conversation with tutorial messages.");
            }
        }

        logger.LogInfo("[BG] Default user and AI profile setup completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError("[BG] Failed during background database/user initialization", ex);
        if (ex.InnerException != null)
            logger.LogError($"[BG] Inner exception: {ex.InnerException.Message}");
    }
}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

logger.LogInfo("Skipping Neo4j database schema initialization - will be done after Neo4j runtime starts");

// Initialize Weaviate vector store (deferred)
_ = Task.Factory.StartNew(async () =>
{
    try
    {
        logger.LogInfo("[BG] Initializing Weaviate vector store...");
        await app.Services.GetRequiredService<WeaviateVectorStore>().InitializeAsync();
        logger.LogInfo("[BG] Weaviate vector store initialization completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogWarning($"[BG] Failed to initialize Weaviate vector store - this is expected if Weaviate is not available. Error: {ex.Message}");
    }
}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

// Initialize Neo4j runtime + service
var neo4jEmbedded = builder.Configuration.GetValue<bool>("AppSettings:Neo4jEmbedded", false);
var requireNeo4j  = builder.Configuration.GetValue<bool>("AppSettings:RequireNeo4j", false);
logger.LogInfo($"Neo4j embedded mode is {(neo4jEmbedded ? "enabled" : "disabled")}");
logger.LogInfo($"Neo4j required: {requireNeo4j}");

// Initialize Neo4j runtime + service (deferred) — only when embedded mode is enabled
_ = Task.Factory.StartNew(async () =>
{
    try
    {
        if (neo4jEmbedded)
        {
            var rt = app.Services.GetRequiredService<Neo4jRuntimeService>();
            logger.LogInfo("[BG] Initializing Neo4j runtime (embedded mode)...");
            await rt.InitializeAsync();

            logger.LogInfo("[BG] Waiting 30 seconds for Neo4j to fully start up...");
            await Task.Delay(TimeSpan.FromSeconds(30));
            logger.LogInfo("[BG] Neo4j startup delay completed");
        }

        using var scope = app.Services.CreateScope();
        var nx = scope.ServiceProvider.GetRequiredService<INeo4jService>();

        logger.LogInfo("[BG] Initializing Neo4j service...");
        await nx.InitializeAsync();
        logger.LogInfo("[BG] Neo4j service initialization completed successfully");

        var ok = await nx.PingAsync();
        if (!ok && requireNeo4j)
        {
            logger.LogCritical("[BG] Startup aborted: Neo4j service unavailable.");
            Environment.Exit(1);
        }
        if (!ok)
            logger.LogWarning("[BG] Neo4j offline - graph features disabled until reconnection.");
    }
    catch (Exception ex)
    {
        if (requireNeo4j)
        {
            logger.LogCritical("[BG] Startup aborted: Neo4j service unavailable.", ex);
            Environment.Exit(1);
        }
        else
            logger.LogWarning($"[BG] Failed to initialize Neo4j service. Graph functionality unavailable. Error: {ex.Message}");
    }
}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

// Initialize Neo4j vector store (deferred)
_ = Task.Factory.StartNew(async () =>
{
    try
    {
        logger.LogInfo("[BG] Initializing Neo4j vector store...");
        await app.Services.GetRequiredService<Neo4jVectorStore>().InitializeAsync();
        logger.LogInfo("[BG] Neo4j vector store initialization completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError($"[BG] Failed to initialize Neo4j vector store. Memory search will not be available. Error: {ex.Message}");
    }
}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

// Fish Speech API is managed by FishSpeechHostedService
logger.LogInfo("Fish Speech TTS API will be started by FishSpeechHostedService");

// Memory synchronization after Neo4j ready (deferred)
_ = Task.Factory.StartNew(async () =>
{
    try
    {
        logger.LogInfo("[BG] Performing memory synchronization after Neo4j initialization...");
        using var scope = app.Services.CreateScope();
        var dbCtx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bg    = scope.ServiceProvider.GetRequiredService<IBrainGraphService>();

        var userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var sqliteMem = await dbCtx.Memories.Where(m => m.UserId == userId).ToListAsync();

        List<Guid> neo4jIds = new();
        try { neo4jIds = await bg.GetAllMemoryIdsAsync(userId); }
        catch (Exception ex) { logger.LogWarning($"[BG] Failed to get Neo4j memories: {ex.Message}"); }

        var missing = sqliteMem.Select(m => m.Id).Except(neo4jIds).ToList();
        if (missing.Any())
        {
            logger.LogInfo($"[BG] Found {missing.Count} memories missing in Neo4j. Repairing...");
            int success=0, failure=0;
            foreach(var id in missing)
            {
                var mem = sqliteMem.First(m => m.Id == id);
                try
                {
                    var meta = new Dictionary<string,string>
                    {
                        {"category", mem.Category ?? "general"},
                        {"userId",   mem.UserId.ToString()},
                        {"isShared", mem.IsShared.ToString()},
                        {"createdAt", mem.CreatedAt.ToString("O")},
                        {"source",  "startup-sync"}
                    };
                    if (await bg.AddMemoryAsync(mem.Id, mem.Content, meta))
                        success++;
                    else
                        failure++;
                }
                catch
                {
                    failure++;
                }
            }
            logger.LogInfo($"[BG] Startup memory repair: {success} succeeded, {failure} failed");
        }
        else
        {
            logger.LogInfo("[BG] Memory databases already in sync.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError("[BG] Failed to perform startup memory sync", ex);
    }
}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

// Global unhandled exception handler
AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
{
    var ex = args.ExceptionObject as Exception;
    var term = args.IsTerminating ? "Application is terminating" : "Application will continue";
    logger.LogCritical($"Unhandled exception: {term}", ex);
    if (args.IsTerminating)
        Thread.Sleep(1000);
};

// ─── HTTP pipeline ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (ctx, next) =>
{
    logger.LogInfo($"[REQUEST]  {ctx.Request.Method} {ctx.Request.Path}");
    await next();
    logger.LogInfo($"[RESPONSE] {ctx.Request.Method} -> {ctx.Response.StatusCode}");
});

app.UseGlobalExceptionHandler();
// Avoid HTTPS redirection inside container or when HTTPS isn't configured
var runningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
var httpsPortsConfigured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORTS"));
if (!runningInContainer && (app.Environment.IsDevelopment() || httpsPortsConfigured))
{
    app.UseHttpsRedirection();
}
else
{
    logger.LogInfo("HTTPS redirection disabled (running in container or no HTTPS ports configured)");
}
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (app.Environment.IsDevelopment())
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            ctx.Context.Response.Headers.Append("Pragma",        "no-cache");
            ctx.Context.Response.Headers.Append("Expires",       "0");
        }
    }
});
app.UseRouting();

// Global request/response logging with correlation IDs
app.UseRequestResponseLogging();

app.UseCors("CorsPolicy");
app.UseAuthorization();

// ─── Endpoints ────────────────────────────────────────────────────────────────
logger.LogInfo("[ROUTING] Mapping controllers...");
app.MapControllers();
logger.LogInfo("[ROUTING] Controllers mapped successfully");

// Health probe reflects readiness after critical init; returns 503 while starting
app.MapGet("/healthz", () =>
{
    bool ready =
        initializationStatus.TryGetValue("directories", out var d) && d &&
        initializationStatus.TryGetValue("migrations",  out var m) && m &&
        initializationStatus.TryGetValue("database",    out var db) && db &&
        initializationStatus.TryGetValue("users",       out var u) && u &&
        initializationStatus.TryGetValue("characters",  out var c) && c;

    return ready
        ? Results.Ok(new { ok = true, ts = DateTime.UtcNow })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}).WithName("Healthz");


// Readiness probe: only returns 200 once core initialization is complete
app.MapGet("/readyz", () =>
{
    bool ready =
        initializationStatus.TryGetValue("directories", out var d) && d &&
        initializationStatus.TryGetValue("migrations",  out var m) && m &&
        initializationStatus.TryGetValue("database",    out var db) && db &&
        initializationStatus.TryGetValue("users",       out var u) && u &&
        initializationStatus.TryGetValue("characters",  out var c) && c;

    return ready
        ? Results.Ok(new { ok = true, ts = DateTime.UtcNow })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
})
.WithName("Readyz");

app.MapHub<ChatHub>("/hubs/chat").RequireCors("CorsPolicy");
app.MapHub<VoiceHub>("/hubs/voice").RequireCors("CorsPolicy");
app.MapHub<NotificationHub>("/hubs/notification").RequireCors("CorsPolicy");

app.MapGet("/api/health/neo4j", async (INeo4jService nx) =>
    Results.Ok(await nx.GetStatusAsync())
).RequireCors("CorsPolicy");

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

        var testUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        logger.LogInfo("[MEMORY DEBUG] BrainService.SearchAsync...");
        var brainResults = await brainService.SearchAsync(request.Query, request.Limit ?? 5);

        logger.LogInfo("[MEMORY DEBUG] Neo4jVectorStore.SearchAsync...");
        var embed = await embeddingService.EmbedTextAsync(request.Query);
        var neoResults = await neo4jStore.SearchAsync(embed, request.Limit ?? 5);

        logger.LogInfo("[MEMORY DEBUG] VectorRouter.SearchMemoryAsync...");
        var routerResults = await vectorRouter.SearchMemoryAsync(testUserId, request.Query, request.Limit ?? 5);

        return Results.Ok(new {
            query = request.Query,
            brainService = new {
                count = brainResults.Count(),
                results = brainResults.Select(r => new {
                    id = r.Id,
                    score = r.Score,
                    content = r.Metadata?.GetValueOrDefault("content",""),
                    metadata = r.Metadata
                }).ToList()
            },
            neo4jVectorStore = new {
                count = neoResults.Count(),
                results = neoResults.Select(r => new {
                    id = r.Id,
                    score = r.Score,
                    content = r.Metadata?.GetValueOrDefault("content",""),
                    metadata = r.Metadata
                }).ToList()
            },
            vectorRouter = new {
                count = routerResults.Count(),
                results = routerResults.Select(r => new {
                    id = r.Id,
                    score = r.Score,
                    content = r.Metadata?.GetValueOrDefault("content",""),
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
}).RequireCors("CorsPolicy");

app.MapGet("/api/debug/memory-count", async (
    IBrainService brainService,
    Neo4jVectorStore neo4jStore,
    IEmbeddingService embeddingService,
    ISimpleLoggerService logger) =>
{
    try
    {
        logger.LogInfo("[MEMORY DEBUG] Getting memory counts...");
        var allBrain = await brainService.SearchAsync("", 1000);
        logger.LogInfo($"[MEMORY DEBUG] BrainService: {allBrain.Count()} memories");

        int neoCount = 0;
        try
        {
            var emptyEmbed = await embeddingService.EmbedTextAsync("");
            neoCount = (await neo4jStore.SearchAsync(emptyEmbed, 1000)).Count();
            logger.LogInfo($"[MEMORY DEBUG] Neo4jVectorStore: {neoCount} memories");
        }
        catch (Exception ex)
        {
            logger.LogError($"[MEMORY DEBUG] Failed to query Neo4j: {ex.Message}");
        }

        return Results.Ok(new {
            brainService = allBrain.Count(),
            neo4jVectorStore = neoCount,
            brainMemories = allBrain.Take(10).Select(r => new {
                id = r.Id,
                score = r.Score,
                content = r.Metadata?.GetValueOrDefault("content",""),
                metadata = r.Metadata
            }).ToList()
        });
    }
    catch (Exception ex)
    {
        logger.LogError($"[MEMORY DEBUG] Error getting memory counts: {ex.Message}", ex);
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireCors("CorsPolicy");

// SPA Fallback
logger.LogInfo("[ROUTING] Setting up SPA fallback...");
app.MapFallback(async context =>
{
    var path = context.Request.Path.Value;
    logger.LogInfo($"[FALLBACK] Processing path: {path}");
    if (path != null && (path.StartsWith("/api/") || path.StartsWith("/hubs/") || path.Contains('.')))
    {
        logger.LogInfo($"[FALLBACK] Rejecting path (API/hub/static): {path}");
        context.Response.StatusCode = 404;
        return;
    }
    logger.LogInfo($"[FALLBACK] Serving index.html for SPA route: {path}");
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync("wwwroot/index.html");
});
logger.LogInfo("[ROUTING] SPA fallback configured");

// Explicit URLs: when running in container, bind to http://0.0.0.0:8080
if (!app.Urls.Any())
{
    var isContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
    if (isContainer)
        app.Urls.Add("http://0.0.0.0:8080");
    else
        app.Urls.Add("http://localhost:5000");
}

logger.LogInfo($"Application starting on URLs: {string.Join(", ", app.Urls)}");

// Ensure logs directory exists
var logDir = builder.Configuration["AppSettings:LogDirectory"] ?? "logs";
if (!Directory.Exists(logDir))
    Directory.CreateDirectory(logDir);

// ─── Frontend dev server (Windows only) ──────────────────────────────────────
Process? frontendProcess = null;
try
{
    var frontendPath = Path.Combine(Directory.GetCurrentDirectory(), "frontend");
    if (Directory.Exists(frontendPath))
    {
        logger.LogInfo("Starting frontend development server...");
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c npm run dev",
            WorkingDirectory = frontendPath,
            UseShellExecute = false,
            CreateNoWindow = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        frontendProcess = Process.Start(psi);
        if (frontendProcess != null)
        {
            logger.LogInfo("Frontend dev server started at http://localhost:5173");
            _ = Task.Run(async () =>
            {
                string? outLine;
                while ((outLine = await frontendProcess.StandardOutput.ReadLineAsync()) != null)
                {
                    if (!string.IsNullOrEmpty(outLine))
                        logger.LogInfo($"Frontend ⟶ {outLine}");
                }
            });
            _ = Task.Run(async () =>
            {
                string? errLine;
                while ((errLine = await frontendProcess.StandardError.ReadLineAsync()) != null)
                {
                    if (!string.IsNullOrEmpty(errLine))
                        logger.LogWarning($"Frontend ERR ⟶ {errLine}");
                }
            });
        }
        else
        {
            logger.LogWarning("Failed to start frontend dev server");
        }
    }
    else
    {
        logger.LogWarning($"Frontend directory not found: {frontendPath}");
    }
}
catch (Exception ex)
{
    logger.LogError("Failed to start frontend dev server", ex);
}

// Shutdown hook to kill frontend
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInfo("Application shutting down...");

    // Stop terminal logging
    terminalLogger.StopLogging();
    terminalLogger.Dispose();

    if (frontendProcess != null && !frontendProcess.HasExited)
    {
        try
        {
            logger.LogInfo("Stopping frontend dev server...");
            frontendProcess.Kill();
            frontendProcess.WaitForExit(5000);
            logger.LogInfo("Frontend dev server stopped");
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to stop frontend dev server", ex);
        }
    }
});

// Auto-open browser (Windows)
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    Thread.Sleep( frontendProcess != null ? 5000 : 0 );
    var target = frontendProcess != null ? "http://localhost:5173" : (builder.Configuration["AppSettings:BaseUrl"] ?? "http://localhost:5000");
    try
    {
        var psi = new ProcessStartInfo("cmd", $"/c start {target}") { CreateNoWindow = true };
        Process.Start(psi);
        logger.LogInfo($"Opened browser at {target}");
    }
    catch (Exception ex)
    {
        logger.LogError("Failed to open browser", ex);
    }
}

// Start the app
app.Run();

// ─── Request model for memory debug ───────────────────────────────────────────
public class MemorySearchRequest
{
    /// <summary>The search query to test</summary>
    public required string Query { get; set; }

    /// <summary>Maximum number of results to return (defaults to 5)</summary>
    public int? Limit { get; set; }
}
