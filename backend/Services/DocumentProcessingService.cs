using SwAIvyn.Data;
using SwAIvyn.Data.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SwAIvyn.Services
{
    /// <summary>
    /// Service for processing uploaded documents, extracting text, and creating chunks
    /// </summary>
    public interface IDocumentProcessingService
    {
        Task<string> ExtractTextFromFileAsync(string filePath, string mimeType);
        Task<List<DocumentChunk>> CreateTextChunksAsync(Guid documentId, string text, int maxChunkSize = 1000, int overlapSize = 100);
        Task<string> CalculateFileHashAsync(string filePath);
        Task<bool> ProcessDocumentAsync(Guid documentId);
        bool IsSupportedFileType(string mimeType, string extension);
    }

    /// <summary>
    /// Implementation of document processing service
    /// </summary>
    public class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ISimpleLoggerService _logger;

        // Supported file types for text extraction
        private readonly HashSet<string> _supportedMimeTypes = new()
        {
            "text/plain",
            "text/markdown",
            "text/csv",
            "application/json",
            "application/xml",
            "text/xml",
            "text/html",
            "application/rtf",
            "application/pdf",
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/gif",
            "image/bmp",
            "image/tiff",
            "image/webp"
        };

        private readonly HashSet<string> _supportedExtensions = new()
        {
            ".txt", ".md", ".csv", ".json", ".xml", ".html", ".htm", ".rtf",
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp"
        };

        public DocumentProcessingService(
            ApplicationDbContext dbContext,
            ISimpleLoggerService logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <inheritdoc/>
        public bool IsSupportedFileType(string mimeType, string extension)
        {
            return _supportedMimeTypes.Contains(mimeType?.ToLowerInvariant()) ||
                   _supportedExtensions.Contains(extension?.ToLowerInvariant());
        }

        /// <inheritdoc/>
        public async Task<string> ExtractTextFromFileAsync(string filePath, string mimeType)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File not found: {filePath}");
                }

                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                if (!IsSupportedFileType(mimeType, extension))
                {
                    throw new NotSupportedException($"File type not supported: {mimeType} ({extension})");
                }

                // Handle different file types
                string text;
                if (mimeType == "application/pdf" || extension == ".pdf")
                {
                    text = await ExtractTextFromPdfAsync(filePath);
                }
                else if (mimeType.StartsWith("image/") || _supportedExtensions.Contains(extension) && extension.StartsWith("."))
                {
                    text = await ExtractTextFromImageAsync(filePath);
                }
                else
                {
                    // Handle basic text files
                    text = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                }

                // Clean up the text
                text = CleanExtractedText(text);
                
                _logger.LogInfo($"Extracted {text.Length} characters from {filePath}");
                return text;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to extract text from {filePath}", ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<DocumentChunk>> CreateTextChunksAsync(Guid documentId, string text, int maxChunkSize = 1000, int overlapSize = 100)
        {
            var chunks = new List<DocumentChunk>();
            
            if (string.IsNullOrWhiteSpace(text))
            {
                return chunks;
            }

            // Split text into sentences for better chunk boundaries
            var sentences = SplitIntoSentences(text);
            var currentChunk = new StringBuilder();
            var currentPosition = 0;
            var chunkIndex = 0;

            foreach (var sentence in sentences)
            {
                // Check if adding this sentence would exceed the chunk size
                if (currentChunk.Length + sentence.Length > maxChunkSize && currentChunk.Length > 0)
                {
                    // Create a chunk from current content
                    var chunkContent = currentChunk.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(chunkContent))
                    {
                        var chunk = new DocumentChunk
                        {
                            Id = Guid.NewGuid(),
                            UploadDocumentId = documentId,
                            Content = chunkContent,
                            ChunkIndex = chunkIndex++,
                            StartPosition = currentPosition - chunkContent.Length,
                            EndPosition = currentPosition,
                            Length = chunkContent.Length,
                            CreatedAt = DateTime.UtcNow,
                            IsStoredInWeaviate = false
                        };
                        chunks.Add(chunk);
                    }

                    // Start new chunk with overlap
                    currentChunk.Clear();
                    if (overlapSize > 0 && chunkContent.Length > overlapSize)
                    {
                        var overlapText = chunkContent.Substring(chunkContent.Length - overlapSize);
                        currentChunk.Append(overlapText);
                    }
                }

                currentChunk.Append(sentence);
                currentPosition += sentence.Length;
            }

            // Add the final chunk if there's remaining content
            if (currentChunk.Length > 0)
            {
                var finalContent = currentChunk.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(finalContent))
                {
                    var finalChunk = new DocumentChunk
                    {
                        Id = Guid.NewGuid(),
                        UploadDocumentId = documentId,
                        Content = finalContent,
                        ChunkIndex = chunkIndex,
                        StartPosition = currentPosition - finalContent.Length,
                        EndPosition = currentPosition,
                        Length = finalContent.Length,
                        CreatedAt = DateTime.UtcNow,
                        IsStoredInWeaviate = false
                    };
                    chunks.Add(finalChunk);
                }
            }

            _logger.LogInfo($"Created {chunks.Count} chunks from document {documentId}");
            return chunks;
        }

        /// <inheritdoc/>
        public async Task<string> CalculateFileHashAsync(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = await Task.Run(() => sha256.ComputeHash(stream));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <inheritdoc/>
        public async Task<bool> ProcessDocumentAsync(Guid documentId)
        {
            try
            {
                var document = await _dbContext.UploadDocuments
                    .FirstOrDefaultAsync(d => d.Id == documentId);

                if (document == null)
                {
                    _logger.LogError($"Document not found: {documentId}");
                    return false;
                }

                // Update status to processing
                document.Status = DocumentProcessingStatus.Processing;
                await _dbContext.SaveChangesAsync();

                // Extract text from the file
                var extractedText = await ExtractTextFromFileAsync(document.FilePath, document.MimeType);
                
                // Create chunks
                var chunks = await CreateTextChunksAsync(documentId, extractedText);

                // Save extracted text and chunks to database
                document.ExtractedText = extractedText;
                document.ChunkCount = chunks.Count;
                document.Status = DocumentProcessingStatus.Completed;
                document.ProcessedAt = DateTime.UtcNow;

                _dbContext.DocumentChunks.AddRange(chunks);
                await _dbContext.SaveChangesAsync();

                _logger.LogInfo($"Successfully processed document {documentId} with {chunks.Count} chunks");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to process document {documentId}", ex);
                
                // Update document status to failed
                var document = await _dbContext.UploadDocuments
                    .FirstOrDefaultAsync(d => d.Id == documentId);
                if (document != null)
                {
                    document.Status = DocumentProcessingStatus.Failed;
                    document.ErrorMessage = ex.Message;
                    await _dbContext.SaveChangesAsync();
                }
                
                return false;
            }
        }

        private string CleanExtractedText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remove excessive whitespace
            text = Regex.Replace(text, @"\s+", " ");
            
            // Remove control characters except newlines and tabs
            text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");
            
            return text.Trim();
        }

        private List<string> SplitIntoSentences(string text)
        {
            var sentences = new List<string>();
            
            // Simple sentence splitting - could be improved with more sophisticated NLP
            var sentenceEnders = new[] { '.', '!', '?' };
            var currentSentence = new StringBuilder();
            
            for (int i = 0; i < text.Length; i++)
            {
                currentSentence.Append(text[i]);
                
                if (Array.IndexOf(sentenceEnders, text[i]) >= 0)
                {
                    // Check if this is likely the end of a sentence
                    if (i == text.Length - 1 || char.IsWhiteSpace(text[i + 1]))
                    {
                        sentences.Add(currentSentence.ToString());
                        currentSentence.Clear();
                    }
                }
            }
            
            // Add any remaining text as the final sentence
            if (currentSentence.Length > 0)
            {
                sentences.Add(currentSentence.ToString());
            }
            
            return sentences;
        }

        /// <summary>
        /// Extract text from PDF files
        /// </summary>
        private async Task<string> ExtractTextFromPdfAsync(string filePath)
        {
            try
            {
                // TODO: Implement PDF text extraction using a library like iTextSharp or PdfPig
                // For now, return a placeholder that indicates PDF processing is needed
                var fileInfo = new FileInfo(filePath);
                var placeholder = $"[PDF Document: {fileInfo.Name}]\n" +
                                $"File size: {fileInfo.Length} bytes\n" +
                                $"Note: PDF text extraction requires additional library implementation.\n" +
                                $"This document has been uploaded and can be processed when PDF extraction is implemented.";

                _logger.LogWarning($"PDF text extraction not yet implemented for {filePath}");
                return placeholder;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to extract text from PDF {filePath}", ex);
                throw new InvalidOperationException($"PDF text extraction failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Extract text from image files using OCR
        /// </summary>
        private async Task<string> ExtractTextFromImageAsync(string filePath)
        {
            try
            {
                // TODO: Implement OCR text extraction using a library like Tesseract.NET
                // For now, return a placeholder that indicates image processing is needed
                var fileInfo = new FileInfo(filePath);
                var placeholder = $"[Image Document: {fileInfo.Name}]\n" +
                                $"File size: {fileInfo.Length} bytes\n" +
                                $"Image type: {Path.GetExtension(filePath)}\n" +
                                $"Note: OCR text extraction requires additional library implementation.\n" +
                                $"This image has been uploaded and can be processed when OCR extraction is implemented.";

                _logger.LogWarning($"Image OCR text extraction not yet implemented for {filePath}");
                return placeholder;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to extract text from image {filePath}", ex);
                throw new InvalidOperationException($"Image text extraction failed: {ex.Message}", ex);
            }
        }
    }
}
