using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SwAIvyn.Data.Entities;
using SwAIvyn.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace SwAIvyn.Controllers
{
    /// <summary>
    /// Controller for handling document uploads and knowledge base management
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly IDocumentUploadService _uploadService;
        private readonly ISimpleLoggerService _logger;

        public UploadController(
            IDocumentUploadService uploadService,
            ISimpleLoggerService logger)
        {
            _uploadService = uploadService;
            _logger = logger;
        }

        /// <summary>
        /// Upload a single file for general knowledge
        /// </summary>
        /// <param name="file">The file to upload</param>
        /// <param name="category">Optional category for organizing the document</param>
        /// <param name="description">Optional description of the document</param>
        /// <returns>The uploaded document information</returns>
        [HttpPost("file")]
        public async Task<IActionResult> UploadFile(
            IFormFile file,
            [FromForm] string? category = null,
            [FromForm] string? description = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest("No file provided");
                }

                if (file.Length > 50 * 1024 * 1024) // 50MB limit
                {
                    return BadRequest("File size exceeds 50MB limit");
                }

                _logger.LogInfo($"Uploading file: {file.FileName} ({file.Length} bytes)");

                using var stream = file.OpenReadStream();
                var document = await _uploadService.SaveUploadedFileAsync(
                    stream, 
                    file.FileName, 
                    file.ContentType, 
                    category, 
                    description);

                // Start processing in the background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _uploadService.ProcessAndStoreInWeaviateAsync(document.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Background processing failed for document {document.Id}", ex);
                    }
                });

                return Ok(new UploadResponse
                {
                    Id = document.Id,
                    FileName = document.FileName,
                    Status = document.Status,
                    UploadedAt = document.UploadedAt,
                    Category = document.Category,
                    Description = document.Description,
                    FileSizeBytes = document.FileSizeBytes
                });
            }
            catch (NotSupportedException ex)
            {
                return BadRequest($"Unsupported file type: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to upload file", ex);
                return StatusCode(500, "An error occurred while uploading the file");
            }
        }

        /// <summary>
        /// Upload multiple files at once
        /// </summary>
        /// <param name="files">The files to upload</param>
        /// <param name="category">Optional category for organizing the documents</param>
        /// <param name="description">Optional description for the documents</param>
        /// <returns>List of uploaded document information</returns>
        [HttpPost("files")]
        public async Task<IActionResult> UploadFiles(
            List<IFormFile> files,
            [FromForm] string? category = null,
            [FromForm] string? description = null)
        {
            try
            {
                if (files == null || !files.Any())
                {
                    return BadRequest("No files provided");
                }

                if (files.Count > 20) // Limit to 20 files at once
                {
                    return BadRequest("Too many files. Maximum 20 files per upload");
                }

                var totalSize = files.Sum(f => f.Length);
                if (totalSize > 200 * 1024 * 1024) // 200MB total limit
                {
                    return BadRequest("Total file size exceeds 200MB limit");
                }

                var results = new List<UploadResponse>();
                var errors = new List<string>();

                foreach (var file in files)
                {
                    try
                    {
                        if (file.Length == 0) continue;

                        using var stream = file.OpenReadStream();
                        var document = await _uploadService.SaveUploadedFileAsync(
                            stream, 
                            file.FileName, 
                            file.ContentType, 
                            category, 
                            description);

                        results.Add(new UploadResponse
                        {
                            Id = document.Id,
                            FileName = document.FileName,
                            Status = document.Status,
                            UploadedAt = document.UploadedAt,
                            Category = document.Category,
                            Description = document.Description,
                            FileSizeBytes = document.FileSizeBytes
                        });

                        // Start processing in the background
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _uploadService.ProcessAndStoreInWeaviateAsync(document.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Background processing failed for document {document.Id}", ex);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{file.FileName}: {ex.Message}");
                        _logger.LogError($"Failed to upload file {file.FileName}", ex);
                    }
                }

                return Ok(new
                {
                    SuccessfulUploads = results,
                    Errors = errors,
                    TotalFiles = files.Count,
                    SuccessCount = results.Count,
                    ErrorCount = errors.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to upload files", ex);
                return StatusCode(500, "An error occurred while uploading files");
            }
        }

        /// <summary>
        /// Get list of uploaded documents
        /// </summary>
        /// <param name="skip">Number of documents to skip</param>
        /// <param name="take">Number of documents to take</param>
        /// <returns>List of uploaded documents</returns>
        [HttpGet("documents")]
        public async Task<IActionResult> GetDocuments([FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            try
            {
                if (take > 100) take = 100; // Limit to 100 documents per request

                var documents = await _uploadService.GetUploadedDocumentsAsync(skip, take);
                
                var response = documents.Select(d => new UploadResponse
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    Status = d.Status,
                    UploadedAt = d.UploadedAt,
                    ProcessedAt = d.ProcessedAt,
                    Category = d.Category,
                    Description = d.Description,
                    FileSizeBytes = d.FileSizeBytes,
                    ChunkCount = d.ChunkCount,
                    ErrorMessage = d.ErrorMessage
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get documents", ex);
                return StatusCode(500, "An error occurred while retrieving documents");
            }
        }

        /// <summary>
        /// Get details of a specific document
        /// </summary>
        /// <param name="id">Document ID</param>
        /// <returns>Document details</returns>
        [HttpGet("documents/{id}")]
        public async Task<IActionResult> GetDocument(Guid id)
        {
            try
            {
                var document = await _uploadService.GetDocumentByIdAsync(id);
                if (document == null)
                {
                    return NotFound("Document not found");
                }

                var chunks = await _uploadService.GetDocumentChunksAsync(id);

                return Ok(new
                {
                    Document = new UploadResponse
                    {
                        Id = document.Id,
                        FileName = document.FileName,
                        Status = document.Status,
                        UploadedAt = document.UploadedAt,
                        ProcessedAt = document.ProcessedAt,
                        Category = document.Category,
                        Description = document.Description,
                        FileSizeBytes = document.FileSizeBytes,
                        ChunkCount = document.ChunkCount,
                        ErrorMessage = document.ErrorMessage
                    },
                    Chunks = chunks.Select(c => new
                    {
                        c.Id,
                        c.ChunkIndex,
                        c.Length,
                        c.IsStoredInWeaviate,
                        c.StoredAt,
                        c.ErrorMessage,
                        ContentPreview = c.Content.Length > 200 ? c.Content.Substring(0, 200) + "..." : c.Content
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get document {id}", ex);
                return StatusCode(500, "An error occurred while retrieving the document");
            }
        }

        /// <summary>
        /// Delete a document
        /// </summary>
        /// <param name="id">Document ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("documents/{id}")]
        public async Task<IActionResult> DeleteDocument(Guid id)
        {
            try
            {
                var success = await _uploadService.DeleteDocumentAsync(id);
                if (!success)
                {
                    return NotFound("Document not found");
                }

                return Ok(new { Message = "Document deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete document {id}", ex);
                return StatusCode(500, "An error occurred while deleting the document");
            }
        }

        /// <summary>
        /// Reprocess a document (re-extract text and re-store in Weaviate)
        /// </summary>
        /// <param name="id">Document ID</param>
        /// <returns>Success status</returns>
        [HttpPost("documents/{id}/reprocess")]
        public async Task<IActionResult> ReprocessDocument(Guid id)
        {
            try
            {
                var success = await _uploadService.ReprocessDocumentAsync(id);
                if (!success)
                {
                    return NotFound("Document not found or reprocessing failed");
                }

                return Ok(new { Message = "Document reprocessing started" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to reprocess document {id}", ex);
                return StatusCode(500, "An error occurred while reprocessing the document");
            }
        }

        /// <summary>
        /// Search through uploaded knowledge documents
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="limit">Maximum number of results</param>
        /// <returns>Search results</returns>
        [HttpGet("search")]
        public async Task<IActionResult> SearchKnowledge([FromQuery] string query, [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Query parameter is required");
                }

                // This endpoint would integrate with the search.py service
                // For now, return a placeholder response
                var results = new
                {
                    Query = query,
                    Results = new object[] { },
                    Message = "Knowledge search integration with search.py service is ready for implementation"
                };

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to search knowledge documents", ex);
                return StatusCode(500, "An error occurred while searching knowledge documents");
            }
        }
    }

    /// <summary>
    /// Response model for uploaded documents
    /// </summary>
    public class UploadResponse
    {
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public DocumentProcessingStatus Status { get; set; }
        public DateTime UploadedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public long FileSizeBytes { get; set; }
        public int ChunkCount { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
