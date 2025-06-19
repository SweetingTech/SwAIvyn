using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using SwAIvyn.Services;
using SwAIvyn.Services.Graph;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for debugging and managing memory synchronization between SQLite and Neo4j
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MemorySyncStatusController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IBrainGraphService _brainGraphService;
        private readonly ISimpleLoggerService _logger;

        public MemorySyncStatusController(
            ApplicationDbContext dbContext, 
            IBrainGraphService brainGraphService,
            ISimpleLoggerService logger)
        {
            _dbContext = dbContext;
            _brainGraphService = brainGraphService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the sync status between SQLite and Neo4j for memories.
        /// Since this is a single-user application, we get ALL memories regardless of userId.
        /// </summary>
        /// <param name="userId">User ID (ignored for single-user app)</param>
        /// <returns>Memory sync status information</returns>
        [HttpGet("status/{userId}")]
        public async Task<IActionResult> GetSyncStatus(Guid userId)
        {
            try
            {
                // Get ALL memories from SQLite (single-user app)
                var sqliteMemories = await _dbContext.Memories
                    .Select(m => new { m.Id, m.Content, m.Category, m.CreatedAt })
                    .ToListAsync();

                _logger.LogInfo($"📊 Found {sqliteMemories.Count} memories in SQLite (all users)");

                // Get ALL memories from Neo4j (use default user ID for Neo4j operations)
                var defaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                var neo4jMemoryIds = new List<Guid>();
                try
                {
                    neo4jMemoryIds = await _brainGraphService.GetAllMemoryIdsAsync(defaultUserId);
                    _logger.LogInfo($"📊 Found {neo4jMemoryIds.Count} memories in Neo4j");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠️ Failed to get Neo4j memories: {ex.Message}");
                }

                // Find missing memories (in SQLite but not in Neo4j)
                var sqliteIds = sqliteMemories.Select(m => m.Id).ToHashSet();
                var neo4jIds = neo4jMemoryIds.ToHashSet();
                
                var missingInNeo4j = sqliteIds.Except(neo4jIds).ToList();
                var missingInSqlite = neo4jIds.Except(sqliteIds).ToList();

                // Get details of missing memories
                var missingMemoryDetails = sqliteMemories
                    .Where(m => missingInNeo4j.Contains(m.Id))
                    .Select(m => new
                    {
                        m.Id,
                        m.Content,
                        m.Category,
                        m.CreatedAt,
                        Preview = m.Content.Length > 100 ? m.Content.Substring(0, 100) + "..." : m.Content
                    })
                    .ToList();

                var syncStatus = new
                {
                    UserId = userId,
                    SqliteCount = sqliteMemories.Count,
                    Neo4jCount = neo4jMemoryIds.Count,
                    InSync = missingInNeo4j.Count == 0 && missingInSqlite.Count == 0,
                    MissingInNeo4j = new
                    {
                        Count = missingInNeo4j.Count,
                        MemoryIds = missingInNeo4j,
                        Details = missingMemoryDetails
                    },
                    MissingInSqlite = new
                    {
                        Count = missingInSqlite.Count,
                        MemoryIds = missingInSqlite
                    },
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInfo($"🔍 Sync Status - SQLite: {sqliteMemories.Count}, Neo4j: {neo4jMemoryIds.Count}, Missing in Neo4j: {missingInNeo4j.Count}, Missing in SQLite: {missingInSqlite.Count}");

                return Ok(syncStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError($"🚨 Error getting sync status: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Repairs missing memories by syncing them from SQLite to Neo4j.
        /// Since this is a single-user application, we repair ALL memories regardless of userId.
        /// </summary>
        /// <param name="userId">User ID (ignored for single-user app)</param>
        /// <returns>Repair results</returns>
        [HttpPost("repair/{userId}")]
        public async Task<IActionResult> RepairMemories(Guid userId)
        {
            try
            {
                _logger.LogInfo($"🔧 Starting memory repair (single-user app)");

                // Get ALL memories from SQLite (single-user app)
                var sqliteMemories = await _dbContext.Memories.ToListAsync();

                // Use default user ID for Neo4j operations
                var defaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                var neo4jMemoryIds = new List<Guid>();
                try
                {
                    neo4jMemoryIds = await _brainGraphService.GetAllMemoryIdsAsync(defaultUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠️ Failed to get Neo4j memories during repair: {ex.Message}");
                }

                var sqliteIds = sqliteMemories.Select(m => m.Id).ToHashSet();
                var neo4jIds = neo4jMemoryIds.ToHashSet();
                var missingInNeo4j = sqliteIds.Except(neo4jIds).ToList();

                var repairResults = new List<object>();
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
                            { "source", "repair-sync" }
                        };

                        var success = await _brainGraphService.AddMemoryAsync(memory.Id, memory.Content, metadata);
                        
                        if (success)
                        {
                            successCount++;
                            repairResults.Add(new
                            {
                                MemoryId = memory.Id,
                                Status = "Success",
                                Preview = memory.Content.Length > 50 ? memory.Content.Substring(0, 50) + "..." : memory.Content
                            });
                            _logger.LogInfo($"✅ Repaired memory {memory.Id}");
                        }
                        else
                        {
                            failureCount++;
                            repairResults.Add(new
                            {
                                MemoryId = memory.Id,
                                Status = "Failed",
                                Error = "AddMemoryAsync returned false",
                                Preview = memory.Content.Length > 50 ? memory.Content.Substring(0, 50) + "..." : memory.Content
                            });
                            _logger.LogWarning($"❌ Failed to repair memory {memory.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        repairResults.Add(new
                        {
                            MemoryId = memory.Id,
                            Status = "Error",
                            Error = ex.Message,
                            Preview = memory.Content.Length > 50 ? memory.Content.Substring(0, 50) + "..." : memory.Content
                        });
                        _logger.LogError($"🚨 Error repairing memory {memory.Id}: {ex.Message}");
                    }
                }

                var result = new
                {
                    UserId = userId,
                    TotalMissingMemories = missingInNeo4j.Count,
                    SuccessfulRepairs = successCount,
                    FailedRepairs = failureCount,
                    RepairDetails = repairResults,
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInfo($"🔧 Memory repair completed - Success: {successCount}, Failed: {failureCount}");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"🚨 Error during memory repair: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Syncs a specific memory from SQLite to Neo4j
        /// </summary>
        /// <param name="memoryId">Memory ID to sync</param>
        /// <returns>Sync result</returns>
        [HttpPost("sync/{memoryId}")]
        public async Task<IActionResult> SyncSpecificMemory(Guid memoryId)
        {
            try
            {
                var memory = await _dbContext.Memories.FindAsync(memoryId);
                if (memory == null)
                {
                    return NotFound(new { error = "Memory not found in SQLite" });
                }

                var metadata = new Dictionary<string, string>
                {
                    { "category", memory.Category ?? "general" },
                    { "userId", memory.UserId.ToString() },
                    { "isShared", memory.IsShared.ToString() },
                    { "createdAt", memory.CreatedAt.ToString("O") },
                    { "source", "manual-sync" }
                };

                var success = await _brainGraphService.AddMemoryAsync(memory.Id, memory.Content, metadata);
                
                if (success)
                {
                    _logger.LogInfo($"✅ Manually synced memory {memoryId}");
                    return Ok(new
                    {
                        MemoryId = memoryId,
                        Status = "Success",
                        Message = "Memory successfully synced to Neo4j"
                    });
                }
                else
                {
                    _logger.LogWarning($"❌ Failed to manually sync memory {memoryId}");
                    return BadRequest(new
                    {
                        MemoryId = memoryId,
                        Status = "Failed",
                        Message = "Failed to add memory to Neo4j"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"🚨 Error syncing memory {memoryId}: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets overall sync statistics for the single-user application
        /// </summary>
        /// <returns>Global sync statistics</returns>
        [HttpGet("global-stats")]
        public async Task<IActionResult> GetGlobalSyncStats()
        {
            try
            {
                // Get ALL memories from SQLite (single-user app)
                var totalSqliteMemories = await _dbContext.Memories.CountAsync();

                // Get ALL memories from Neo4j (use default user ID)
                var defaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                var totalNeo4jMemories = 0;
                try
                {
                    var neo4jIds = await _brainGraphService.GetAllMemoryIdsAsync(defaultUserId);
                    totalNeo4jMemories = neo4jIds.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠️ Failed to get Neo4j count: {ex.Message}");
                }

                var totalMissingInNeo4j = Math.Max(0, totalSqliteMemories - totalNeo4jMemories);

                // Single user stats
                var userSyncStats = new List<object>
                {
                    new
                    {
                        UserId = defaultUserId,
                        SqliteCount = totalSqliteMemories,
                        Neo4jCount = totalNeo4jMemories,
                        MissingInNeo4j = totalMissingInNeo4j,
                        SyncPercentage = totalSqliteMemories > 0 ? Math.Round((double)totalNeo4jMemories / totalSqliteMemories * 100, 2) : 100
                    }
                };

                var globalStats = new
                {
                    TotalUsers = 1, // Single-user application
                    TotalSqliteMemories = totalSqliteMemories,
                    TotalNeo4jMemories = totalNeo4jMemories,
                    TotalMissingInNeo4j = totalMissingInNeo4j,
                    GlobalSyncPercentage = totalSqliteMemories > 0 ? Math.Round((double)totalNeo4jMemories / totalSqliteMemories * 100, 2) : 100,
                    UserStats = userSyncStats,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(globalStats);
            }
            catch (Exception ex)
            {
                _logger.LogError($"🚨 Error getting global sync stats: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Performs a full sync operation: checks status, repairs missing memories, and returns final status
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Complete sync operation results</returns>
        [HttpPost("full/{userId}")]
        public async Task<IActionResult> FullSync(Guid userId)
        {
            try
            {
                _logger.LogInfo($"🚀 Starting full sync for user {userId}");

                // 1. Get current status
                var initialStatusResult = await GetSyncStatus(userId) as OkObjectResult;
                if (initialStatusResult?.Value == null)
                {
                    return StatusCode(500, "Failed to compute initial sync status");
                }

                dynamic initialStatus = initialStatusResult.Value;
                int missingCount = initialStatus.MissingInNeo4j.Count;

                _logger.LogInfo($"📊 Initial status - Missing in Neo4j: {missingCount}");                if (missingCount == 0)
                {
                    return Ok(new
                    {
                        message = "Already in sync - no repairs needed",
                        userId = userId,
                        summary = new
                        {
                            wasInSync = true,
                            isNowInSync = true,
                            improvementMade = false,
                            totalInitialIssues = 0,
                            totalRemainingIssues = 0,
                            issuesResolved = 0
                        },
                        initialStatus,
                        repairResult = new { message = "No repairs performed" },
                        finalStatus = initialStatus,
                        timestamp = DateTime.UtcNow
                    });
                }

                // 2. Perform repair
                var repairResult = await RepairMemories(userId) as OkObjectResult;
                if (repairResult?.Value == null)
                {
                    return StatusCode(500, "Repair operation failed");
                }

                dynamic repairData = repairResult.Value;
                _logger.LogInfo($"🔧 Repair completed - Success: {repairData.SuccessfulRepairs}, Failed: {repairData.FailedRepairs}");

                // 3. Get final status to verify sync
                var finalStatusResult = await GetSyncStatus(userId) as OkObjectResult;
                if (finalStatusResult?.Value == null)
                {
                    return StatusCode(500, "Failed to compute final sync status");
                }

                dynamic finalStatus = finalStatusResult.Value;
                int remainingMissing = finalStatus.MissingInNeo4j.Count;                var result = new
                {
                    message = $"Full sync completed: {missingCount} → {remainingMissing} missing rows",
                    userId = userId,
                    summary = new
                    {
                        wasInSync = missingCount == 0,
                        isNowInSync = remainingMissing == 0,
                        improvementMade = missingCount > remainingMissing,
                        totalInitialIssues = missingCount,
                        totalRemainingIssues = remainingMissing,
                        issuesResolved = missingCount - remainingMissing
                    },
                    initialStatus,
                    repairResult = repairData,
                    finalStatus,
                    timestamp = DateTime.UtcNow
                };

                _logger.LogInfo($"🎉 Full sync completed for user {userId} - Improved by {missingCount - remainingMissing} items");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"🚨 Error during full sync for user {userId}: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Repairs missing memories by syncing them from Neo4j to SQLite.
        /// This is the reverse of the regular repair - it adds memories that exist in Neo4j but not in SQLite.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Reverse repair results</returns>
        [HttpPost("repair-reverse/{userId}")]
        public async Task<IActionResult> RepairMemoriesReverse(Guid userId)
        {
            try
            {
                _logger.LogInfo($"🔧 Starting reverse memory repair (Neo4j → SQLite) for user {userId}");

                // Get ALL memories from SQLite (single-user app)
                var sqliteMemories = await _dbContext.Memories.ToListAsync();

                // Get ALL memories from Neo4j
                var defaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                var neo4jMemoryIds = new List<Guid>();
                try
                {
                    neo4jMemoryIds = await _brainGraphService.GetAllMemoryIdsAsync(defaultUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠️ Failed to get Neo4j memories during reverse repair: {ex.Message}");
                    return StatusCode(500, new { error = "Failed to get memories from Neo4j", details = ex.Message });
                }

                var sqliteIds = sqliteMemories.Select(m => m.Id).ToHashSet();
                var neo4jIds = neo4jMemoryIds.ToHashSet();
                var missingInSqlite = neo4jIds.Except(sqliteIds).ToList();

                var repairResults = new List<object>();
                int successCount = 0;
                int failureCount = 0;

                _logger.LogInfo($"📊 Found {missingInSqlite.Count} memories to sync from Neo4j to SQLite");

                foreach (var memoryId in missingInSqlite)
                {
                    try
                    {
                        // Get memory content from Neo4j
                        var searchResults = await _brainGraphService.SearchAsync($"memory_id:{memoryId}", limit: 1);
                        
                        if (!searchResults.Any())
                        {
                            failureCount++;
                            repairResults.Add(new
                            {
                                MemoryId = memoryId,
                                Status = "Failed",
                                Error = "Memory not found in Neo4j search results"
                            });
                            _logger.LogWarning($"❌ Memory {memoryId} not found in Neo4j search");
                            continue;
                        }

                        var result = searchResults.First();
                        var hit = result.Hit;

                        // Extract content and metadata from Neo4j
                        var content = hit.Metadata?.TryGetValue("content", out var contentValue) == true ? contentValue : "Memory content not available";
                        var category = hit.Metadata?.TryGetValue("category", out var cat) == true ? cat : "Chat Memory";
                        var createdAtStr = hit.Metadata?.TryGetValue("createdAt", out var created) == true ? created : DateTime.UtcNow.ToString("O");
                        var isSharedStr = hit.Metadata?.TryGetValue("isShared", out var shared) == true ? shared : "false";

                        DateTime.TryParse(createdAtStr, out var createdAt);
                        bool.TryParse(isSharedStr, out var isShared);

                        // Create new memory item for SQLite
                        var memory = new MemoryItem
                        {
                            Id = memoryId,
                            UserId = defaultUserId, // Use default user ID for single-user app
                            Content = content,
                            Category = category,
                            IsShared = isShared,
                            CreatedAt = createdAt == default ? DateTime.UtcNow : createdAt,
                            LastAccessed = DateTime.UtcNow
                        };

                        // Save to SQLite
                        _dbContext.Memories.Add(memory);
                        await _dbContext.SaveChangesAsync();

                        successCount++;
                        repairResults.Add(new
                        {
                            MemoryId = memory.Id,
                            Status = "Success",
                            Preview = memory.Content.Length > 50 ? memory.Content.Substring(0, 50) + "..." : memory.Content,
                            Category = memory.Category
                        });
                        _logger.LogInfo($"✅ Synced memory from Neo4j to SQLite: {memory.Id} - {memory.Content.Substring(0, Math.Min(30, memory.Content.Length))}...");
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        repairResults.Add(new
                        {
                            MemoryId = memoryId,
                            Status = "Error",
                            Error = ex.Message
                        });
                        _logger.LogError($"❌ Failed to sync memory {memoryId} from Neo4j to SQLite: {ex.Message}");
                    }
                }                var repairResult = new
                {
                    UserId = userId,
                    TotalMissingMemories = missingInSqlite.Count,
                    SuccessfulRepairs = successCount,
                    FailedRepairs = failureCount,
                    RepairDetails = repairResults,
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInfo($"🎉 Reverse repair completed - Success: {successCount}, Failed: {failureCount}");

                return Ok(repairResult);
            }
            catch (Exception ex)
            {
                _logger.LogError($"🚨 Error during reverse memory repair: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

    }
}
