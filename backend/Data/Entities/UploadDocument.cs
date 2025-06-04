using System;
using System.ComponentModel.DataAnnotations;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents an uploaded document for general knowledge storage
    /// </summary>
    public class UploadDocument
    {
        /// <summary>
        /// Unique identifier for the uploaded document
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Original filename of the uploaded document
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string FileName { get; set; }

        /// <summary>
        /// File extension (e.g., .pdf, .txt, .docx)
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string FileExtension { get; set; }

        /// <summary>
        /// MIME type of the uploaded file
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string MimeType { get; set; }

        /// <summary>
        /// Size of the file in bytes
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Path where the file is stored on disk
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; }

        /// <summary>
        /// Processing status of the document
        /// </summary>
        [Required]
        public DocumentProcessingStatus Status { get; set; }

        /// <summary>
        /// Extracted text content from the document
        /// </summary>
        public string? ExtractedText { get; set; }

        /// <summary>
        /// Number of chunks created from this document
        /// </summary>
        public int ChunkCount { get; set; }

        /// <summary>
        /// Error message if processing failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// When the document was uploaded
        /// </summary>
        public DateTime UploadedAt { get; set; }

        /// <summary>
        /// When the document processing was completed
        /// </summary>
        public DateTime? ProcessedAt { get; set; }

        /// <summary>
        /// Category or tag for organizing documents
        /// </summary>
        [MaxLength(100)]
        public string? Category { get; set; }

        /// <summary>
        /// Optional description or notes about the document
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Whether this document is available for general knowledge search
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Hash of the file content for duplicate detection
        /// </summary>
        [MaxLength(64)]
        public string? ContentHash { get; set; }
    }

    /// <summary>
    /// Processing status for uploaded documents
    /// </summary>
    public enum DocumentProcessingStatus
    {
        /// <summary>
        /// Document has been uploaded but not yet processed
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Document is currently being processed
        /// </summary>
        Processing = 1,

        /// <summary>
        /// Document has been successfully processed and stored in Weaviate
        /// </summary>
        Completed = 2,

        /// <summary>
        /// Document processing failed
        /// </summary>
        Failed = 3,

        /// <summary>
        /// Document processing was cancelled
        /// </summary>
        Cancelled = 4
    }
}
