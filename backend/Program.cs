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

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Register DbContext with WAL mode and connection pooling
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlite(connectionString + ";Pooling=true;Cache=Shared");
});

// Add DbContextFactory for background services
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlite(connectionString + ";Pooling=true;Cache=Shared");
}, ServiceLifetime.Scoped);

// Add database initializer service
builder.Services.AddScoped<IDatabaseInitializer, DatabaseInitializerService>();

// Add directory initializer service
builder.Services.AddSingleton<DirectoryInitializerService>();

// Add backup service
builder.Services.AddHostedService<BackupService>();

// Add conversation and folder services
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IFolderService, FolderService>();

// Add vector store and brain services
builder.Services.AddSingleton<IEmbeddingService, SimpleEmbeddingService>();
builder.Services.AddSingleton<IVectorStore, SqliteVectorStore>();
builder.Services.AddScoped<IBrainService, BrainService>();

// Add Neo4j and BrainGraph services
builder.Services.AddScoped<INeo4jService, Neo4jService>();
builder.Services.AddSingleton<Neo4jRuntimeService>();
builder.Services.AddScoped<IBrainGraphService, BrainGraphService>();

builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register the configuration service
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();

// Register the simple logger service
builder.Services.AddSingleton<ISimpleLoggerService, SimpleLoggerService>();

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

// Startup health guard: abort if SQLite unavailable
logger.LogInfo("Performing startup health checks...");
using (var scope = app.Services.CreateScope())
{
    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    if (!await dbInitializer.CanConnectAsync())
    {
        logger.LogCritical("Startup aborted: SQLite database unavailable.");
        Environment.Exit(1);
    }

    // Skip Neo4j health check completely
    logger.LogInfo("Startup health checks passed.");
}

// --- Seed default AI profile on first run ---
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // If no AI character profiles exist, create a default
        if (!db.Avatars.Any())
        {
            db.Avatars.Add(new SwAIvyn.Data.Entities.AvatarInfo
            {
                Id = Guid.NewGuid(),
                UserId = db.Users.FirstOrDefault()?.Id ?? Guid.NewGuid(), // Assign to first user or dummy
                Name = "Default AI",
                ImagePath = "",
                Personality = "Friendly and helpful AI assistant.",
                VoiceSettings = "default",
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            });
            db.SaveChanges();
            logger.LogInfo("Seeded default AI profile.");
        }
    }
}
catch (Exception ex)
{
    logger.LogError("Failed to seed default AI profile", ex);
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
    }
}
catch (Exception ex)
{
    logger.LogCritical("Failed to initialize database", ex);
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

// Fallback to index.html for SPA routes
app.MapFallbackToFile("index.html");

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<VoiceHub>("/hubs/voice");
app.MapHub<NotificationHub>("/hubs/notification");

// Add health endpoint for Neo4j
app.MapGet("/api/health/neo4j", async (Neo4jRuntimeService neo4jRuntime) =>
    Results.Ok(await neo4jRuntime.GetStatusAsync()));

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
    var hostTask = app.StartAsync();

    // Wait a moment for the server to start
    Thread.Sleep(1000);

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
