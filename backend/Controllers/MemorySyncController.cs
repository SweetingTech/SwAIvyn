using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using SwAIvyn.Enums;
using SwAIvyn.Services;
using SwAIvyn.Services.Graph;
using SwAIvyn.Services.Interfaces;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Simplified memory sync controller for single-user application
    /// Handles synchronization between SQLite and Neo4j without user ID filtering
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MemorySyncController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IBrainGraphService _brainGraphService;
        private readonly IMemoryService _memoryService;
        private readonly ISimpleLoggerService _logger;

        public MemorySyncController(
            ApplicationDbContext dbContext,
            IBrainGraphService brainGraphService,
            IMemoryService memoryService,
            ISimpleLoggerService logger)
        {
            _dbContext = dbContext;
            _brainGraphService = brainGraphService;
            _memoryService = memoryService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the sync status between SQLite and Neo4j for all memories
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetSyncStatus()
        {
            try
            {
                _logger.LogInfo("🔍 Getting memory sync status for single-user app");

                // Get all memories from SQLite
                var sqliteMemories = await _dbContext.Memories.ToListAsync();
                _logger.LogInfo($"📊 Found {sqliteMemories.Count} memories in SQLite");

                // Get all memory IDs from Neo4j
                var neo4jMemoryIds = new List<Guid>();
                try
                {
                    neo4jMemoryIds = await _brainGraphService.GetAllMemoryIdsAsync(Guid.Empty);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"⚠️ Failed to get Neo4j memories: {ex.Message}");
                }
                _logger.LogInfo($"📊 Found {neo4jMemoryIds.Count} memories in Neo4j");

                // Calculate sync differences
                var sqliteIds = sqliteMemories.Select(m => m.Id).ToHashSet();
                var neo4jIds = neo4jMemoryIds.ToHashSet();

                var missingInNeo4j = sqliteIds.Except(neo4jIds).ToList();
                var missingInSqlite = neo4jIds.Except(sqliteIds).ToList();

                var inSync = missingInNeo4j.Count == 0 && missingInSqlite.Count == 0;

                _logger.LogInfo($"📊 Sync Status - SQLite: {sqliteMemories.Count}, Neo4j: {neo4jMemoryIds.Count}, Missing in Neo4j: {missingInNeo4j.Count}, Missing in SQLite: {missingInSqlite.Count}");

                return Ok(new
                {
                    sqliteCount = sqliteMemories.Count,
                    neo4jCount = neo4jMemoryIds.Count,
                    inSync = inSync,
                    missingInNeo4j = new
                    {
                        count = missingInNeo4j.Count,
                        memoryIds = missingInNeo4j,
                        details = missingInNeo4j.Select(id => 
                        {
                            var memory = sqliteMemories.FirstOrDefault(m => m.Id == id);
                            return new
                            {
                                id = id.ToString(),
                                content = memory?.Content ?? "Unknown",
                                category = memory?.Category ?? "Unknown",
                                createdAt = memory?.CreatedAt.ToString("O") ?? "",
                                preview = memory?.Content?.Length > 50 ? memory.Content.Substring(0, 50) + "..." : memory?.Content ?? ""
                            };
                        }).ToList()
                    },
                    missingInSqlite = new
                    {
                        count = missingInSqlite.Count,
                        memoryIds = missingInSqlite
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error getting sync status: {ex.Message}");
                return StatusCode(500, new { error = "Failed to get sync status", message = ex.Message });
            }
        }

        /// <summary>
        /// Repairs memory sync issues by performing bidirectional sync
        /// </summary>
        [HttpPost("repair")]
        public async Task<IActionResult> RepairMemories()
        {
            try
            {
                _logger.LogInfo("🔧 Starting memory repair for single-user app");

                var successCount = 0;
                var failureCount = 0;
                var repairDetails = new List<object>();

                // Get current sync status
                var sqliteMemories = await _dbContext.Memories.ToListAsync();
                var neo4jMemoryIds = await _brainGraphService.GetAllMemoryIdsAsync(Guid.Empty);

                var sqliteIds = sqliteMemories.Select(m => m.Id).ToHashSet();
                var neo4jIds = neo4jMemoryIds.ToHashSet();

                // Forward sync: SQLite → Neo4j
                var missingInNeo4j = sqliteMemories.Where(m => !neo4jIds.Contains(m.Id)).ToList();
                foreach (var memory in missingInNeo4j)
                {
                    try
                    {
                        var metadata = new Dictionary<string, string>
                        {
                            { "category", memory.Category ?? "general" },
                            { "isShared", memory.IsShared.ToString() },
                            { "createdAt", memory.CreatedAt.ToString("O") },
                            { "source", "repair-sync" }
                        };

                        var success = await _brainGraphService.AddMemoryAsync(memory.Id, memory.Content, metadata);
                        if (success)
                        {
                            successCount++;
                            repairDetails.Add(new
                            {
                                memoryId = memory.Id,
                                direction = "SQLite → Neo4j",
                                status = "Success",
                                preview = memory.Content.Length > 50 ? memory.Content.Substring(0, 50) + "..." : memory.Content
                            });
                        }
                        else
                        {
                            failureCount++;
                            repairDetails.Add(new
                            {
                                memoryId = memory.Id,
                                direction = "SQLite → Neo4j",
                                status = "Failed",
                                error = "Unknown error",
                                preview = memory.Content.Length > 50 ? memory.Content.Substring(0, 50) + "..." : memory.Content
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        repairDetails.Add(new
                        {
                            memoryId = memory.Id,
                            direction = "SQLite → Neo4j",
                            status = "Error",
                            error = ex.Message,
                            preview = memory.Content.Length > 50 ? memory.Content.Substring(0, 50) + "..." : memory.Content
                        });
                    }
                }

                // Reverse sync: Neo4j → SQLite
                var missingInSqlite = neo4jIds.Except(sqliteIds).ToList();
                foreach (var memoryId in missingInSqlite)
                {
                    try
                    {
                        var newMemory = new MemoryItem
                        {
                            Id = memoryId,
                            Content = "Content recovered from Neo4j",
                            Category = "recovered",
                            UserId = Guid.Empty, // Single-user app
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            LastAccessed = DateTime.UtcNow,
                            IsShared = false,
                            TargetStore = VectorTarget.Neo4j
                        };

                        _dbContext.Memories.Add(newMemory);
                        await _dbContext.SaveChangesAsync();
                        
                        successCount++;
                        repairDetails.Add(new
                        {
                            memoryId = memoryId,
                            direction = "Neo4j → SQLite",
                            status = "Success",
                            preview = "Content recovered from Neo4j"
                        });
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        repairDetails.Add(new
                        {
                            memoryId = memoryId,
                            direction = "Neo4j → SQLite",
                            status = "Error",
                            error = ex.Message,
                            preview = "Content recovery failed"
                        });
                    }
                }

                _logger.LogInfo($"🔧 Memory repair completed - Success: {successCount}, Failed: {failureCount}");

                return Ok(new
                {
                    message = "Memory repair completed",
                    totalRepairs = successCount + failureCount,
                    successfulRepairs = successCount,
                    failedRepairs = failureCount,
                    repairDetails = repairDetails,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error during memory repair: {ex.Message}");
                return StatusCode(500, new { error = "Memory repair failed", message = ex.Message });
            }
        }

        /// <summary>
        /// Performs a full sync operation with detailed reporting
        /// </summary>
        [HttpPost("full")]
        public async Task<IActionResult> FullSync()
        {
            try
            {
                _logger.LogInfo("🚀 Starting full memory sync for single-user app");

                // Get initial status
                var initialStatusResult = await GetSyncStatus() as OkObjectResult;
                dynamic initialStatus = initialStatusResult?.Value;

                // Perform repair
                var repairResult = await RepairMemories() as OkObjectResult;
                dynamic repairData = repairResult?.Value;

                // Get final status
                var finalStatusResult = await GetSyncStatus() as OkObjectResult;
                dynamic finalStatus = finalStatusResult?.Value;

                return Ok(new
                {
                    message = "Full sync completed",
                    initialStatus = initialStatus,
                    repairResults = repairData,
                    finalStatus = finalStatus,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error during full sync: {ex.Message}");
                return StatusCode(500, new { error = "Full sync failed", message = ex.Message });
            }
        }
    }
}
