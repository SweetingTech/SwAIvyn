using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using SwAIvyn.Services.VectorStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Service for managing document uploads and integration with Weaviate
    /// </summary>
    public interface IDocumentUploadService
    {
        Task<UploadDocument> SaveUploadedFileAsync(Stream fileStream, string fileName, string mimeType, string? category = null, string? description = null);
        Task<bool> ProcessAndStoreInWeaviateAsync(Guid documentId);
        Task<List<UploadDocument>> GetUploadedDocumentsAsync(int skip = 0, int take = 50);
        Task<UploadDocument?> GetDocumentByIdAsync(Guid documentId);
        Task<bool> DeleteDocumentAsync(Guid documentId);
        Task<List<DocumentChunk>> GetDocumentChunksAsync(Guid documentId);
        Task<bool> ReprocessDocumentAsync(Guid documentId);
    }

    /// <summary>
    /// Implementation of document upload service
    /// </summary>
    public class DocumentUploadService : IDocumentUploadService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IDocumentProcessingService _documentProcessor;
        private readonly IVectorRouter _vectorRouter;
        private readonly IEmbeddingService _embeddingService;
        private readonly ISimpleLoggerService _logger;
        private readonly IConfiguration _configuration;
        private readonly string _uploadsDirectory;

        public DocumentUploadService(
            ApplicationDbContext dbContext,
            IDocumentProcessingService documentProcessor,
            IVectorRouter vectorRouter,
            IEmbeddingService embeddingService,
            ISimpleLoggerService logger,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _documentProcessor = documentProcessor;
            _vectorRouter = vectorRouter;
            _embeddingService = embeddingService;
            _logger = logger;
            _configuration = configuration;
            _uploadsDirectory = _configuration["AppSettings:UploadsDirectory"] ?? "../data/uploads";
            
            // Ensure uploads directory exists
            Directory.CreateDirectory(_uploadsDirectory);
        }

        /// <inheritdoc/>
        public async Task<UploadDocument> SaveUploadedFileAsync(Stream fileStream, string fileName, string mimeType, string? category = null, string? description = null)
        {
            try
            {
                var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
                
                // Check if file type is supported
                if (!_documentProcessor.IsSupportedFileType(mimeType, fileExtension))
                {
                    throw new NotSupportedException($"File type not supported: {mimeType} ({fileExtension})");
                }

                // Generate unique file path
                var documentId = Guid.NewGuid();
                var safeFileName = $"{documentId}{fileExtension}";
                var filePath = Path.Combine(_uploadsDirectory, safeFileName);

                // Save file to disk
                using (var fileStreamOut = File.Create(filePath))
                {
                    await fileStream.CopyToAsync(fileStreamOut);
                }

                var fileInfo = new FileInfo(filePath);
                var contentHash = await _documentProcessor.CalculateFileHashAsync(filePath);

                // Check for duplicate files
                var existingDocument = await _dbContext.UploadDocuments
                    .FirstOrDefaultAsync(d => d.ContentHash == contentHash && d.IsActive);

                if (existingDocument != null)
                {
                    // Delete the newly uploaded file since it's a duplicate
                    File.Delete(filePath);
                    _logger.LogInfo($"Duplicate file detected: {fileName} matches existing document {existingDocument.Id}");
                    return existingDocument;
                }

                // Create database record
                var uploadDocument = new UploadDocument
                {
                    Id = documentId,
                    FileName = fileName,
                    FileExtension = fileExtension,
                    MimeType = mimeType,
                    FileSizeBytes = fileInfo.Length,
                    FilePath = filePath,
                    Status = DocumentProcessingStatus.Pending,
                    UploadedAt = DateTime.UtcNow,
                    Category = category,
                    Description = description,
                    IsActive = true,
                    ContentHash = contentHash,
                    ChunkCount = 0
                };

                _dbContext.UploadDocuments.Add(uploadDocument);
                await _dbContext.SaveChangesAsync();

                _logger.LogInfo($"Saved uploaded file: {fileName} ({fileInfo.Length} bytes) as {documentId}");
                return uploadDocument;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save uploaded file: {fileName}", ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ProcessAndStoreInWeaviateAsync(Guid documentId)
        {
            try
            {
                _logger.LogInfo($"Starting processing and Weaviate storage for document {documentId}");

                // First, process the document to extract text and create chunks
                var processingSuccess = await _documentProcessor.ProcessDocumentAsync(documentId);
                if (!processingSuccess)
                {
                    _logger.LogError($"Document processing failed for {documentId}");
                    return false;
                }

                // Get the document and its chunks
                var document = await _dbContext.UploadDocuments
                    .FirstOrDefaultAsync(d => d.Id == documentId);

                var chunks = await _dbContext.DocumentChunks
                    .Where(c => c.UploadDocumentId == documentId)
                    .OrderBy(c => c.ChunkIndex)
                    .ToListAsync();

                if (document == null || !chunks.Any())
                {
                    _logger.LogError($"No document or chunks found for {documentId}");
                    return false;
                }

                // Store each chunk in Weaviate
                var successCount = 0;
                foreach (var chunk in chunks)
                {
                    try
                    {
                        // Generate embedding for the chunk
                        var embedding = await _embeddingService.EmbedTextAsync(chunk.Content);

                        // Prepare metadata for Weaviate
                        var metadata = new Dictionary<string, string>
                        {
                            ["_type"] = "document", // This tells VectorRouter to use Document class
                            ["content"] = chunk.Content, // The actual text content
                            ["title"] = document.FileName,
                            ["source"] = $"upload:{document.Id}",
                            ["tags"] = document.Category ?? "general",
                            ["userId"] = "00000000-0000-0000-0000-000000000001", // Default user for single-user app
                            ["documentId"] = document.Id.ToString(),
                            ["fileName"] = document.FileName,
                            ["category"] = document.Category ?? "general",
                            ["chunkIndex"] = chunk.ChunkIndex.ToString(),
                            ["uploadedAt"] = document.UploadedAt.ToString("O"),
                            ["scope"] = "knowledge" // Mark as general knowledge, not chat-specific
                        };

                        // Store in Weaviate via VectorRouter
                        var storeSuccess = await _vectorRouter.StoreUploadChunkAsync(chunk.Id, embedding, metadata);

                        if (storeSuccess)
                        {
                            chunk.IsStoredInWeaviate = true;
                            chunk.StoredAt = DateTime.UtcNow;
                            successCount++;
                        }
                        else
                        {
                            chunk.ErrorMessage = "Failed to store in Weaviate";
                            _logger.LogWarning($"Failed to store chunk {chunk.Id} in Weaviate");
                        }
                    }
                    catch (Exception ex)
                    {
                        chunk.ErrorMessage = ex.Message;
                        _logger.LogError($"Error storing chunk {chunk.Id} in Weaviate", ex);
                    }
                }

                // Update database with results
                await _dbContext.SaveChangesAsync();

                var allStored = successCount == chunks.Count;
                _logger.LogInfo($"Stored {successCount}/{chunks.Count} chunks in Weaviate for document {documentId}");

                return allStored;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to process and store document {documentId} in Weaviate", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<List<UploadDocument>> GetUploadedDocumentsAsync(int skip = 0, int take = 50)
        {
            return await _dbContext.UploadDocuments
                .Where(d => d.IsActive)
                .OrderByDescending(d => d.UploadedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<UploadDocument?> GetDocumentByIdAsync(Guid documentId)
        {
            return await _dbContext.UploadDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && d.IsActive);
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteDocumentAsync(Guid documentId)
        {
            try
            {
                var document = await _dbContext.UploadDocuments
                    .FirstOrDefaultAsync(d => d.Id == documentId);

                if (document == null)
                {
                    return false;
                }

                // Get all chunks for this document
                var chunks = await _dbContext.DocumentChunks
                    .Where(c => c.UploadDocumentId == documentId)
                    .ToListAsync();

                // Delete chunks from Weaviate first
                var weaviateDeletionTasks = chunks
                    .Where(c => c.IsStoredInWeaviate)
                    .Select(async chunk =>
                    {
                        try
                        {
                            var success = await _vectorRouter.DeleteUploadChunkAsync(chunk.Id);
                            if (!success)
                            {
                                _logger.LogWarning($"Failed to delete chunk {chunk.Id} from Weaviate");
                            }
                            return success;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error deleting chunk {chunk.Id} from Weaviate", ex);
                            return false;
                        }
                    });

                var weaviateResults = await Task.WhenAll(weaviateDeletionTasks);
                var weaviateSuccessCount = weaviateResults.Count(r => r);

                _logger.LogInfo($"Deleted {weaviateSuccessCount}/{chunks.Count(c => c.IsStoredInWeaviate)} chunks from Weaviate");

                // Delete chunks from SQL database
                _dbContext.DocumentChunks.RemoveRange(chunks);

                // Mark document as inactive
                document.IsActive = false;
                await _dbContext.SaveChangesAsync();

                // Delete the physical file
                if (File.Exists(document.FilePath))
                {
                    File.Delete(document.FilePath);
                }

                _logger.LogInfo($"Deleted document {documentId} from SQL database and file system");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete document {documentId}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<List<DocumentChunk>> GetDocumentChunksAsync(Guid documentId)
        {
            return await _dbContext.DocumentChunks
                .Where(c => c.UploadDocumentId == documentId)
                .OrderBy(c => c.ChunkIndex)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<bool> ReprocessDocumentAsync(Guid documentId)
        {
            try
            {
                var document = await _dbContext.UploadDocuments
                    .FirstOrDefaultAsync(d => d.Id == documentId);

                if (document == null)
                {
                    return false;
                }

                // Remove existing chunks
                var existingChunks = await _dbContext.DocumentChunks
                    .Where(c => c.UploadDocumentId == documentId)
                    .ToListAsync();

                _dbContext.DocumentChunks.RemoveRange(existingChunks);

                // Reset document status
                document.Status = DocumentProcessingStatus.Pending;
                document.ExtractedText = null;
                document.ChunkCount = 0;
                document.ProcessedAt = null;
                document.ErrorMessage = null;

                await _dbContext.SaveChangesAsync();

                // Reprocess the document
                return await ProcessAndStoreInWeaviateAsync(documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to reprocess document {documentId}", ex);
                return false;
            }
        }
    }
}
