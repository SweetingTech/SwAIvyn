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
        /// Gets the sync status between SQLite and Neo4j for a user's memories
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Memory sync status information</returns>
        [HttpGet("status/{userId}")]
        public async Task<IActionResult> GetSyncStatus(Guid userId)
        {
            try
            {
                // Get all memories from SQLite
                var sqliteMemories = await _dbContext.Memories
                    .Where(m => m.UserId == userId)
                    .Select(m => new { m.Id, m.Content, m.Category, m.CreatedAt })
                    .ToListAsync();

                _logger.LogInfo($"📊 Found {sqliteMemories.Count} memories in SQLite for user {userId}");

                // Get all memories from Neo4j
                var neo4jMemoryIds = new List<Guid>();
                try
                {
                    neo4jMemoryIds = await _brainGraphService.GetAllMemoryIdsAsync(userId);
                    _logger.LogInfo($"📊 Found {neo4jMemoryIds.Count} memories in Neo4j for user {userId}");
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
        /// Repairs missing memories by syncing them from SQLite to Neo4j
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Repair results</returns>
        [HttpPost("repair/{userId}")]
        public async Task<IActionResult> RepairMemories(Guid userId)
        {
            try
            {
                _logger.LogInfo($"🔧 Starting memory repair for user {userId}");

                // Get sync status first
                var sqliteMemories = await _dbContext.Memories
                    .Where(m => m.UserId == userId)
                    .ToListAsync();

                var neo4jMemoryIds = new List<Guid>();
                try
                {
                    neo4jMemoryIds = await _brainGraphService.GetAllMemoryIdsAsync(userId);
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
        /// Gets overall sync statistics across all users
        /// </summary>
        /// <returns>Global sync statistics</returns>
        [HttpGet("global-stats")]
        public async Task<IActionResult> GetGlobalSyncStats()
        {
            try
            {
                var totalSqliteMemories = await _dbContext.Memories.CountAsync();
                
                // Get user list to check individual sync status
                var userIds = await _dbContext.Memories
                    .Select(m => m.UserId)
                    .Distinct()
                    .ToListAsync();

                var userSyncStats = new List<object>();
                int totalNeo4jMemories = 0;
                int totalMissingInNeo4j = 0;

                foreach (var userId in userIds)
                {
                    var userSqliteCount = await _dbContext.Memories.CountAsync(m => m.UserId == userId);
                    
                    var userNeo4jCount = 0;
                    try
                    {
                        var neo4jIds = await _brainGraphService.GetAllMemoryIdsAsync(userId);
                        userNeo4jCount = neo4jIds.Count;
                        totalNeo4jMemories += userNeo4jCount;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"⚠️ Failed to get Neo4j count for user {userId}: {ex.Message}");
                    }

                    var missingCount = Math.Max(0, userSqliteCount - userNeo4jCount);
                    totalMissingInNeo4j += missingCount;

                    userSyncStats.Add(new
                    {
                        UserId = userId,
                        SqliteCount = userSqliteCount,
                        Neo4jCount = userNeo4jCount,
                        MissingInNeo4j = missingCount,
                        SyncPercentage = userSqliteCount > 0 ? Math.Round((double)userNeo4jCount / userSqliteCount * 100, 2) : 100
                    });
                }

                var globalStats = new
                {
                    TotalUsers = userIds.Count,
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

                _logger.LogInfo($"📊 Initial status - Missing in Neo4j: {missingCount}");

                if (missingCount == 0)
                {
                    return Ok(new
                    {
                        message = "Already in sync - no repairs needed",
                        initialStatus,
                        repairResults = new { message = "No repairs performed" },
                        finalStatus = initialStatus
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
                int remainingMissing = finalStatus.MissingInNeo4j.Count;

                var result = new
                {
                    message = $"Full sync completed: {missingCount} → {remainingMissing} missing rows",
                    userId = userId,
                    summary = new
                    {
                        initialMissingCount = missingCount,
                        repairedCount = repairData.SuccessfulRepairs,
                        failedRepairCount = repairData.FailedRepairs,
                        finalMissingCount = remainingMissing,
                        syncImprovement = missingCount - remainingMissing,
                        isFullySynced = remainingMissing == 0
                    },
                    initialStatus,
                    repairResults = repairData,
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
    }
}
