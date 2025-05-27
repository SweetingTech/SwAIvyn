using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SwAIvyn.Data;
using SwAIvyn.Services;
using SwAIvyn.Services.VectorStore;
using SwAIvyn.Services.Graph;
using SwAIvyn.Hubs;
using SwAIvyn.Middleware;
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

// Initialize SQLitePCL.raw for extension loading
Batteries_V2.Init();

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
builder.Services.AddSingleton<SqliteVssExtensionInterceptor>(sp =>
    new SqliteVssExtensionInterceptor(
        sp.GetRequiredService<IConfiguration>()
          .GetValue<string>("AppSettings:VssExtensionPath", "sqlite-vss.dll"),
        sp.GetRequiredService<ISimpleLoggerService>()
    )
);

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
        // Ensure the DataSource path is made absolute, relative to the resolvedDataDirectory
        // This handles cases like "Data Source=swai-vyn.db" or "Data Source=../data/swai-vyn.db"
        // by ensuring the final path is within the intended data directory structure.
        string dbFileName = Path.GetFileName(csBuilder.DataSource); // Extracts "swai-vyn.db"
        csBuilder.DataSource = Path.GetFullPath(Path.Combine(resolvedDataDirectory, dbFileName));
    }
    connectionString = csBuilder.ToString();
    var loggerForDb = sp.GetRequiredService<ISimpleLoggerService>(); // Assuming ISimpleLoggerService is registered as Singleton or Scoped
    loggerForDb.LogInfo($"Using resolved connection string for ApplicationDbContext: {connectionString}");

    options
        .UseSqlite(connectionString + ";Pooling=true;Cache=Shared")
        .AddInterceptors(sp.GetRequiredService<SqliteVssExtensionInterceptor>());
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
        string dbFileName = Path.GetFileName(csBuilder.DataSource);
        csBuilder.DataSource = Path.GetFullPath(Path.Combine(resolvedDataDirectory, dbFileName));
    }
    connectionString = csBuilder.ToString();
    var loggerForDbFactory = sp.GetRequiredService<ISimpleLoggerService>();  // Assuming ISimpleLoggerService is registered as Singleton or Scoped
    loggerForDbFactory.LogInfo($"Using resolved connection string for ApplicationDbContext (Factory): {connectionString}");

    options
        .UseSqlite(connectionString + ";Pooling=true;Cache=Shared")
        .AddInterceptors(sp.GetRequiredService<SqliteVssExtensionInterceptor>());
}, ServiceLifetime.Scoped);

// Add database initializer service
builder.Services.AddScoped<IDatabaseInitializer, DatabaseInitializerService>();

// Add direct database service for Users table creation
builder.Services.AddScoped<IDirectDatabaseService, DirectDatabaseService>();

// Add directory initializer service
builder.Services.AddSingleton<DirectoryInitializerService>();

// Add backup service
builder.Services.AddHostedService<BackupService>();

// Add conversation and folder services
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IFolderService, FolderService>();

// Register the simple logger service first (no dependencies)
builder.Services.AddSingleton<ISimpleLoggerService, SimpleLoggerService>();

// Register the settings provider (configuration-based, no database dependency)
builder.Services.AddSingleton<ISettingsProvider, SettingsProvider>();

// Register the settings service (database-based, for user settings)
builder.Services.AddScoped<ISettingsService, SettingsService>();

// Register the configuration service
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();

// Add vector store and brain services
builder.Services.AddSingleton<IEmbeddingService, SimpleEmbeddingService>();
builder.Services.AddSingleton<IVectorStore, SqliteVectorStore>();
builder.Services.AddScoped<IBrainService, BrainService>();

// Add Neo4j and BrainGraph services
builder.Services.AddScoped<INeo4jService, Neo4jService>();
builder.Services.AddSingleton<Neo4jRuntimeService>();
builder.Services.AddScoped<IBrainGraphService, BrainGraphService>();

// Add LLM and AI chat services
builder.Services.AddScoped<ILlmConnectorService, LlmConnectorService>();
builder.Services.AddScoped<IAiChatService, AiChatService>();

// Add character card loader service
builder.Services.AddScoped<CharacterCardLoaderService>();

// Add default character service
builder.Services.AddScoped<IDefaultCharacterService, DefaultCharacterService>();

builder.Services.AddSignalR();
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
    options.AddPolicy("CorsPolicy", builder =>
        builder.WithOrigins("http://localhost:3000")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials());
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
    }

    // Skip Neo4j health check completely
    logger.LogInfo("Startup health checks completed.");
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
                Id = Guid.NewGuid(),
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

        // If no AI character profiles exist, create a default one linked to our user
        if (!db.Avatars.Any())
        {
            logger.LogInfo("No AI profiles found. Creating default AI profile...");            db.Avatars.Add(new SwAIvyn.Data.Entities.AvatarInfo
            {
                Id = Guid.NewGuid(),
                UserId = defaultUserId, // Use our confirmed user ID
                Name = "Default AI",
                ImagePath = "",
                Personality = "Friendly and helpful AI assistant.",
                VoiceSettings = "default",
                Description = "A helpful AI assistant ready to chat with you.",
                Scenario = "General conversation",
                FirstMessage = "Hello! I'm your AI assistant. How can I help you today?",
                MessageExample = "",
                SystemPrompt = "You are a helpful, harmless, and honest AI assistant.",
                PostHistoryInstructions = "",
                AlternateGreetings = "[]",
                Tags = "[]",
                Creator = "SwAIvyn",
                CreatorNotes = "Default AI assistant character",
                CharacterVersion = "1.0",
                Talkativeness = 0.5f,
                IsFavorite = false,
                Extensions = "{}",
                YamlProfile = "",
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            });

            db.SaveChanges();
            logger.LogInfo("Seeded default AI profile.");
        }

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

// Initialize vector store
try
{
    logger.LogInfo("Initializing vector store...");
    var vectorStore = app.Services.GetRequiredService<IVectorStore>();
    await vectorStore.InitializeAsync();
    logger.LogInfo("Vector store initialization completed successfully");
}
catch (Exception ex)
{
    logger.LogError($"Failed to initialize vector store. Vector search will not be available. Error: {ex.Message}");
}

// Initialize Neo4j runtime and service
var neo4jEmbedded = builder.Configuration.GetValue<bool>("AppSettings:Neo4jEmbedded", false);
var requireNeo4j = builder.Configuration.GetValue<bool>("AppSettings:RequireNeo4j", false);
logger.LogInfo($"Neo4j embedded mode is {(neo4jEmbedded ? "enabled" : "disabled")}");
logger.LogInfo($"Neo4j required: {requireNeo4j}");

try
{
    // Get Neo4j services
    var neo4jRuntime = app.Services.GetRequiredService<Neo4jRuntimeService>();

    // Initialize Neo4j runtime (extract and start Neo4j)
    logger.LogInfo("Initializing Neo4j runtime...");
    await neo4jRuntime.InitializeAsync();

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

// Add global exception handler middleware
app.UseGlobalExceptionHandler();

app.UseHttpsRedirection();
app.UseDefaultFiles(); // Add this to serve index.html by default
app.UseStaticFiles();
app.UseRouting();
app.UseCors("CorsPolicy");
app.UseAuthorization();

// Map API controllers first
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<VoiceHub>("/hubs/voice");
app.MapHub<NotificationHub>("/hubs/notification");

// Fallback to index.html for SPA routes - CRITICAL for React Router to work
app.MapFallbackToFile("index.html");

// Add health endpoint for Neo4j
app.MapGet("/api/health/neo4j", async (INeo4jService neo4jService) =>
    Results.Ok(await neo4jService.GetStatusAsync()));

// Set URLs explicitly
if (!app.Urls.Any())
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
    try
    {
        var neo4jRuntime = app.Services.GetRequiredService<Neo4jRuntimeService>();
        neo4jRuntime.Dispose();
        logger.LogInfo("Neo4j runtime shut down successfully");
    }
    catch (Exception ex)
    {
        logger.LogError("Failed to shut down Neo4j runtime", ex);
    }
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

    // Open the browser
    try
    {
        logger.LogInfo($"Opening browser at URL: {hostUrl}");
        var psi = new ProcessStartInfo
        {
            FileName = hostUrl,
            UseShellExecute = true
        };
        Process.Start(psi);
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
