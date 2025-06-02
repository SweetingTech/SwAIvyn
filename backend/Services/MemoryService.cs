using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using SwAIvyn.Enums;
using SwAIvyn.Services.Interfaces;
using SwAIvyn.Services.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SwAIvyn.Services;

/// <summary>
/// Unified facade for managing memories across the three-database harmony architecture.
/// SQLite serves as the source of truth/ledger, Neo4j handles brain memories with vector + graph capabilities,
/// and Weaviate manages document knowledge/uploads.
/// </summary>
public class MemoryService : IMemoryService
{    private readonly ApplicationDbContext _context;
    private readonly SwAIvyn.Services.Interfaces.IVectorRouter _vectorRouter;
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(
        ApplicationDbContext context,
        SwAIvyn.Services.Interfaces.IVectorRouter vectorRouter,
        ILogger<MemoryService> logger)
    {
        _context = context;
        _vectorRouter = vectorRouter;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new memory with intelligent routing to the optimal vector store.
    /// SQLite acts as the authoritative ledger while vector stores provide semantic capabilities.
    /// </summary>
    public async Task<(bool Success, MemoryItem? CreatedMemory)> CreateMemoryAsync(MemoryItem memory, Dictionary<string, string>? metadata = null)
    {
        try
        {
            _logger.LogInformation("Creating new memory with content length: {ContentLength}", memory.Content?.Length ?? 0);

            // Step 1: Determine optimal vector store using intelligent routing
            var optimalStore = _vectorRouter.DetermineOptimalStore(memory, metadata);
            memory.TargetStore = optimalStore;

            _logger.LogInformation("Determined optimal store: {Store} for memory", optimalStore);

            // Step 2: Save to SQLite as source of truth
            _context.Memories.Add(memory);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Saved memory to SQLite with ID: {MemoryId}", memory.Id);

            // Step 3: Add to the determined vector store
            try
            {
                await _vectorRouter.AddToVectorStoreAsync(memory, optimalStore, metadata);
                _logger.LogInformation("Successfully added memory {MemoryId} to vector store {Store}", memory.Id, optimalStore);
            }
            catch (Exception vectorEx)
            {
                _logger.LogError(vectorEx, "Failed to add memory {MemoryId} to vector store {Store}. Memory saved to SQLite only.", memory.Id, optimalStore);
                // Don't fail the entire operation - SQLite has the authoritative record
            }return (true, memory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create memory");
            return (false, null);
        }
    }

    /// <summary>
    /// Retrieves all memories from SQLite (source of truth).
    /// For semantic search capabilities, use SearchMemoriesAsync instead.
    /// </summary>
    public async Task<List<MemoryItem>> GetMemoriesAsync(Guid userId, string? category = null)    {
        try
        {
            _logger.LogInformation("Retrieving memories (single-user app), category: {Category}", category);

            var baseQuery = _context.Memories.AsQueryable();
                
            if (!string.IsNullOrEmpty(category))
            {
                baseQuery = baseQuery.Where(m => m.Category == category);
            }
            
            var query = baseQuery.OrderByDescending(m => m.CreatedAt);

            var memories = await query.ToListAsync();
            _logger.LogInformation("Retrieved {Count} memories from SQLite", memories.Count);

            return memories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve memories");
            throw;
        }
    }

    /// <summary>
    /// Updates a memory across all stores (SQLite + appropriate vector store).
    /// Maintains consistency between the authoritative ledger and semantic capabilities.
    /// </summary>
    public async Task<bool> UpdateMemoryAsync(Guid memoryId, string? content = null, string? category = null, bool? isShared = null, VectorTarget? targetStore = null)    {
        try
        {
            _logger.LogInformation("Updating memory with ID: {MemoryId}", memoryId);

            var existingMemory = await _context.Memories.FindAsync(memoryId);
            if (existingMemory == null)
            {
                _logger.LogWarning("Memory with ID {MemoryId} not found", memoryId);
                return false;
            }

            var oldTargetStore = existingMemory.TargetStore;
            bool contentChanged = false;

            // Update fields if provided
            if (content != null && content != existingMemory.Content)
            {
                existingMemory.Content = content;
                contentChanged = true;
            }
            
            if (category != null)
            {
                existingMemory.Category = category;
            }
            
            if (isShared.HasValue)
            {
                existingMemory.IsShared = isShared.Value;
            }
            
            if (targetStore.HasValue)
            {
                existingMemory.TargetStore = targetStore.Value;
            }

            existingMemory.UpdatedAt = DateTime.UtcNow;            // Re-determine optimal store if content changed
            VectorTarget newOptimalStore = existingMemory.TargetStore;
            if (contentChanged)
            {
                newOptimalStore = _vectorRouter.DetermineOptimalStore(existingMemory, null);
                existingMemory.TargetStore = newOptimalStore;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated memory {MemoryId} in SQLite", memoryId);

            // Handle vector store updates
            try
            {                // If the optimal store changed, remove from old and add to new
                if (oldTargetStore != newOptimalStore)
                {
                    _logger.LogInformation("Target store changed from {OldStore} to {NewStore} for memory {MemoryId}", 
                        oldTargetStore, newOptimalStore, memoryId);

                    // Remove from old store
                    await _vectorRouter.RemoveFromVectorStoreAsync(memoryId, oldTargetStore);
                    
                    // Add to new store
                    await _vectorRouter.AddToVectorStoreAsync(existingMemory, newOptimalStore, null);
                }
                else if (contentChanged)
                {
                    // Update in the same store
                    await _vectorRouter.UpdateInVectorStoreAsync(memoryId, existingMemory.Content ?? "", newOptimalStore, null);
                }

                _logger.LogInformation("Successfully updated memory {MemoryId} in vector stores", memoryId);
            }
            catch (Exception vectorEx)
            {
                _logger.LogError(vectorEx, "Failed to update memory {MemoryId} in vector stores. SQLite updated successfully.", memoryId);
                // Don't fail the entire operation - SQLite has the authoritative record
            }

            return true;        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update memory with ID: {MemoryId}", memoryId);
            return false;
        }
    }

    /// <summary>
    /// Deletes a memory from all stores (SQLite + appropriate vector store).
    /// Ensures complete removal from the distributed memory system.
    /// </summary>
    public async Task<bool> DeleteMemoryAsync(Guid memoryId)
    {        try
        {
            _logger.LogInformation("Deleting memory with ID: {MemoryId}", memoryId);

            var memory = await _context.Memories.FindAsync(memoryId);
            if (memory == null)
            {
                _logger.LogWarning("Memory with ID {MemoryId} not found", memoryId);
                return false;
            }

            var targetStore = memory.TargetStore;

            // Remove from SQLite
            _context.Memories.Remove(memory);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted memory {MemoryId} from SQLite", memoryId);            // Remove from vector store
            try
            {
                await _vectorRouter.RemoveFromVectorStoreAsync(memoryId, targetStore);
                _logger.LogInformation("Successfully deleted memory {MemoryId} from vector store {Store}", memoryId, targetStore);
            }
            catch (Exception vectorEx)
            {
                _logger.LogError(vectorEx, "Failed to delete memory {MemoryId} from vector store {Store}. SQLite deletion successful.", memoryId, targetStore);
                // Don't fail the entire operation - SQLite deletion was successful
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete memory with ID: {MemoryId}", memoryId);
            return false;
        }
    }

    /// <summary>
    /// Performs semantic similarity search across vector stores using fan-out approach.
    /// Combines results from multiple stores for comprehensive memory retrieval.
    /// </summary>
    public async Task<List<(MemoryItem Memory, float Similarity)>> SearchMemoriesAsync(Guid userId, string query, int maxResults = 10, VectorTarget? targetStore = null)    {
        try
        {
            _logger.LogInformation("Searching global memories (single-user app) with query: '{Query}', maxResults: {MaxResults}, targetStore: {TargetStore}",
                query, maxResults, targetStore);            // Use fan-out search to query vector stores
            var searchResults = await _vectorRouter.FanOutSearchAsync(query, userId, maxResults);

            _logger.LogInformation("Fan-out search returned {Count} results", searchResults.Count);

            // Convert search results back to MemoryItem objects by querying SQLite
            // No user filtering needed for single-user app (like character cards)
            var memoryIds = searchResults.Select(r => r.MemoryId).ToList();
            var memories = await _context.Memories
                .Where(m => memoryIds.Contains(m.Id))
                .ToListAsync();

            // Check for sync issues - if vector stores found memories but SQLite doesn't have them
            if (searchResults.Count > memories.Count)
            {
                _logger.LogWarning("🔄 Sync issue detected: Vector stores found {VectorCount} memories but SQLite only has {SqliteCount}",
                    searchResults.Count, memories.Count);
                await SyncOrphanedMemoriesAsync(searchResults, memories, userId);

                // Re-query after sync
                memories = await _context.Memories
                    .Where(m => memoryIds.Contains(m.Id))
                    .ToListAsync();
            }

            // Create result list with similarity scores
            var results = new List<(MemoryItem Memory, float Similarity)>();
            foreach (var result in searchResults)
            {
                var memory = memories.FirstOrDefault(m => m.Id == result.MemoryId);
                if (memory != null)
                {
                    results.Add((memory, result.Similarity));
                }
                else
                {
                    _logger.LogWarning("⚠️ Memory {MemoryId} found in vector store but missing from SQLite after sync attempt", result.MemoryId);
                }
            }

            _logger.LogInformation("Returning {Count} memories with similarities from search", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search memories with query: '{Query}'", query);
            throw;
        }
    }    /// <summary>
    /// Retrieves memories specifically from Neo4j for graph-based relationships.
    /// Useful for exploring connections and patterns in brain memories.
    /// </summary>
    public async Task<List<MemoryItem>> GetGraphMemoriesAsync(Guid userId, string? category = null, int maxResults = 50)
    {
        try
        {
            _logger.LogInformation("Retrieving graph memories from Neo4j for user: {UserId}, category: {Category}, limit: {MaxResults}", userId, category, maxResults);            var searchResults = await _vectorRouter.SearchVectorStoreAsync(category ?? "", VectorTarget.Neo4j, userId, maxResults);
            
            // Convert search results back to MemoryItem objects by GUID
            var memoryIds = searchResults.Select(r => r.MemoryId).ToList();
            var memories = await _context.Memories
                .Where(m => memoryIds.Contains(m.Id) && m.TargetStore == VectorTarget.Neo4j)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} graph memories from Neo4j", memories.Count);
            return memories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve graph memories from Neo4j");
            throw;
        }
    }    /// <summary>
    /// Retrieves memories specifically from Weaviate for document-based knowledge.
    /// Ideal for uploaded files, documents, and structured knowledge bases.
    /// </summary>
    public async Task<List<MemoryItem>> GetDocumentMemoriesAsync(Guid userId, int maxResults = 50)
    {
        try
        {
            _logger.LogInformation("Retrieving document memories from Weaviate for user: {UserId}, limit: {MaxResults}", userId, maxResults);            var searchResults = await _vectorRouter.SearchVectorStoreAsync("", VectorTarget.Weaviate, userId, maxResults);
            
            // Convert search results back to MemoryItem objects by GUID
            var memoryIds = searchResults.Select(r => r.MemoryId).ToList();
            var memories = await _context.Memories
                .Where(m => memoryIds.Contains(m.Id) && m.TargetStore == VectorTarget.Weaviate)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} document memories from Weaviate", memories.Count);
            return memories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve document memories from Weaviate");
            throw;
        }
    }    /// <summary>
    /// Performs comprehensive reconciliation between SQLite and vector stores.
    /// Heals inconsistencies and ensures data integrity across the distributed system.
    /// </summary>
    public async Task<ReconciliationReport> ReconcileMemoriesAsync(Guid? userId = null)
    {
        var report = new ReconciliationReport
        {
            ExecutedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting memory reconciliation process for user: {UserId}", userId?.ToString() ?? "all users");

            // Get memories from SQLite (source of truth)
            var query = _context.Memories.AsQueryable();
            if (userId.HasValue)
            {
                query = query.Where(m => m.UserId == userId.Value);
            }
            
            var orderedQuery = query.OrderByDescending(m => m.CreatedAt);
            
            var allMemories = await orderedQuery.ToListAsync();
            report.TotalMemoriesChecked = allMemories.Count;

            _logger.LogInformation("Found {Count} memories in SQLite for reconciliation", allMemories.Count);

            var inconsistencies = new List<string>();
            var fixedCount = 0;
            var errors = new List<string>();

            foreach (var memory in allMemories)
            {
                try
                {                    // Check if memory exists in its designated vector store
                    var vectorResults = await _vectorRouter.SearchVectorStoreAsync(memory.Id.ToString(), memory.TargetStore, memory.UserId, 1);
                    
                    if (!vectorResults.Any(r => r.MemoryId == memory.Id))
                    {
                        // Memory missing from vector store - re-add it
                        inconsistencies.Add($"Memory {memory.Id} missing from {memory.TargetStore}");
                        _logger.LogWarning("Memory {MemoryId} missing from vector store {Store}, re-adding", memory.Id, memory.TargetStore);

                        await _vectorRouter.AddToVectorStoreAsync(memory, memory.TargetStore, null);
                        fixedCount++;
                        _logger.LogInformation("Re-added memory {MemoryId} to vector store {Store}", memory.Id, memory.TargetStore);
                    }
                }
                catch (Exception ex)
                {
                    var error = $"Error reconciling memory {memory.Id}: {ex.Message}";
                    errors.Add(error);
                    _logger.LogError(ex, "Error during reconciliation of memory {MemoryId}", memory.Id);
                }
            }

            report.InconsistenciesFound = inconsistencies.Count;
            report.InconsistenciesFixed = fixedCount;
            report.Errors = errors;
            report.Duration = DateTime.UtcNow - report.ExecutedAt;

            _logger.LogInformation("Reconciliation completed. Fixed: {Fixed}, Errors: {Errors}, Duration: {Duration}ms", 
                fixedCount, errors.Count, report.Duration.TotalMilliseconds);

            return report;
        }
        catch (Exception ex)
        {
            report.Duration = DateTime.UtcNow - report.ExecutedAt;
            report.Errors = new List<string> { ex.Message };

            _logger.LogError(ex, "Reconciliation process failed");
            throw;
        }
    }

    /// <summary>
    /// Syncs orphaned memories from vector stores back to SQLite
    /// </summary>
    private async Task SyncOrphanedMemoriesAsync(List<(Guid MemoryId, string Content, float Similarity, VectorTarget Source)> vectorResults, List<MemoryItem> sqliteMemories, Guid userId)
    {
        try
        {
            var sqliteIds = sqliteMemories.Select(m => m.Id).ToHashSet();
            var orphanedResults = vectorResults.Where(r => !sqliteIds.Contains(r.MemoryId)).ToList();

            _logger.LogInformation("🔄 Attempting to sync {Count} orphaned memories from vector stores to SQLite", orphanedResults.Count);

            foreach (var orphaned in orphanedResults)
            {
                try
                {
                    // Create a new memory record in SQLite based on vector store data
                    var newMemory = new MemoryItem
                    {
                        Id = orphaned.MemoryId,
                        Content = orphaned.Content ?? "Content recovered from vector store",
                        Category = "recovered", // Mark as recovered
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        LastAccessed = DateTime.UtcNow,
                        IsShared = false,
                        TargetStore = orphaned.Source // Use the actual source from the vector result
                    };

                    _context.Memories.Add(newMemory);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("✅ Synced orphaned memory {MemoryId} from {Source} vector store to SQLite", orphaned.MemoryId, orphaned.Source);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to sync orphaned memory {MemoryId}: {Message}", orphaned.MemoryId, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during orphaned memory sync");
        }
    }
}
