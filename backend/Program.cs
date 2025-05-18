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
});

// Add database initializer service
builder.Services.AddSingleton<IDatabaseInitializer, DatabaseInitializerService>();

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
builder.Services.AddSingleton<INeo4jService, Neo4jService>();
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
    var dbInitializer = app.Services.GetRequiredService<IDatabaseInitializer>();
    await dbInitializer.InitializeAsync();
    logger.LogInfo("Database initialization completed successfully");
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

// Initialize Neo4j service
try
{
    logger.LogInfo("Initializing Neo4j service...");
    var neo4jService = app.Services.GetRequiredService<INeo4jService>();
    await neo4jService.InitializeAsync();
    logger.LogInfo("Neo4j service initialization completed successfully");
}
catch (Exception ex)
{
    logger.LogError($"Failed to initialize Neo4j service. Graph functionality will not be available. Error: {ex.Message}");
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

// Log application startup
logger.LogInfo($"Application starting on URLs: {string.Join(", ", app.Urls)}");

// Register shutdown handler
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    logger.LogInfo("Application is shutting down...");
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
    app.Run();
    logger.LogInfo("Application has stopped");
}
