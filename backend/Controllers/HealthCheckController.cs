// HealthCheckController.cs
// Provides API endpoints for health checks of core backend services (SQLite, Vector Store, Neo4j)

using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Services;
using SwAIvyn.Services.VectorStore;
using SwAIvyn.Services.Graph;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    // Marks this class as an API controller and sets the route to /api/healthcheck
    [ApiController]
    [Route("api/[controller]")]
    public class HealthCheckController : ControllerBase
    {
        // Dependencies for checking health of vector store, Neo4j, and SQLite
        private readonly IVectorStore _vectorStore;
        private readonly INeo4jService _neo4jService;
        private readonly IDatabaseInitializer _dbInitializer;
        private readonly WeaviateVectorStore _weaviateVectorStore;

        // Constructor injects required services for health checks
        public HealthCheckController(IVectorStore vectorStore, INeo4jService neo4jService, IDatabaseInitializer dbInitializer, WeaviateVectorStore weaviateVectorStore)
        {
            _vectorStore = vectorStore; // Used to check vector store health
            _neo4jService = neo4jService; // Used to check Neo4j health
            _dbInitializer = dbInitializer; // Used to check SQLite health
            _weaviateVectorStore = weaviateVectorStore; // Used for specific Weaviate health checks
        }

        // GET /api/healthcheck/sqlite
        // Checks if the SQLite database is available and can be connected to
        [HttpGet("sqlite")]
        public async Task<IActionResult> Sqlite()
        {
            var ok = await _dbInitializer.CanConnectAsync(); // Returns true if DB is reachable
            // Return 200 OK if healthy, 503 Service Unavailable if not
            return ok ? Ok(new { status = "ok" }) : StatusCode(503, new { status = "unavailable" });
        }

        // GET /api/healthcheck/vector
        // Checks if the vector store (embedding DB) is available
        [HttpGet("vector")]
        public async Task<IActionResult> Vector()
        {
            var ok = await _vectorStore.HealthCheckAsync(); // Returns true if vector store is healthy
            // Return 200 OK if healthy, 503 Service Unavailable if not
            return ok ? Ok(new { status = "ok" }) : StatusCode(503, new { status = "unavailable" });
        }

        // GET /api/healthcheck/neo4j
        // Checks if the Neo4j graph database is available
        [HttpGet("neo4j")]
        public async Task<IActionResult> Neo4j()
        {
            // Get detailed status information
            var status = await _neo4jService.GetStatusAsync();

            // Check if Neo4j is reachable
            var isOnline = await _neo4jService.PingAsync();

            // Return detailed status information
            string? mode = null;
            string? uri = null;

            // Use TryGetValue to avoid double lookups
            status.TryGetValue("Mode", out var modeObj);
            status.TryGetValue("Uri", out var uriObj);

            if (modeObj != null) mode = modeObj.ToString();
            if (uriObj != null) uri = uriObj.ToString();

            return Ok(new {
                status = isOnline ? "online" : "offline",
                details = status,
                isEmbedded = mode == "Embedded",
                uri
            });
        }

        // GET /api/healthcheck/weaviate
        // Checks if the Weaviate vector database is available with detailed information
        [HttpGet("weaviate")]
        public async Task<IActionResult> Weaviate()
        {
            // Get detailed status information
            var status = await _weaviateVectorStore.GetStatusAsync();

            // Check if Weaviate is reachable
            var isOnline = await _weaviateVectorStore.HealthCheckAsync();

            // Return detailed status information
            string? baseUrl = null;
            bool? initialized = null;
            string? memoryClass = null;
            string? documentClass = null;

            // Extract status details safely
            status.TryGetValue("baseUrl", out var baseUrlObj);
            status.TryGetValue("initialized", out var initializedObj);
            status.TryGetValue("memoryClass", out var memoryClassObj);
            status.TryGetValue("documentClass", out var documentClassObj);

            if (baseUrlObj != null) baseUrl = baseUrlObj.ToString();
            if (initializedObj != null) initialized = (bool?)initializedObj;
            if (memoryClassObj != null) memoryClass = memoryClassObj.ToString();
            if (documentClassObj != null) documentClass = documentClassObj.ToString();

            return Ok(new {
                status = isOnline ? "online" : "offline",
                details = status,
                baseUrl,
                initialized,
                memoryClass,
                documentClass
            });
        }
    }
}
