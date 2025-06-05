using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwAIvyn.Data.Entities
{
    /// <summary>
    /// Represents a chunk of text extracted from an uploaded document
    /// </summary>
    public class DocumentChunk
    {
        /// <summary>
        /// Unique identifier for the document chunk
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Reference to the parent uploaded document
        /// </summary>
        [Required]
        public Guid UploadDocumentId { get; set; }

        /// <summary>
        /// Navigation property to the parent document
        /// </summary>
        [ForeignKey(nameof(UploadDocumentId))]
        public UploadDocument UploadDocument { get; set; }

        /// <summary>
        /// The text content of this chunk
        /// </summary>
        [Required]
        public string Content { get; set; }

        /// <summary>
        /// Sequential number of this chunk within the document (0-based)
        /// </summary>
        public int ChunkIndex { get; set; }

        /// <summary>
        /// Character position where this chunk starts in the original document
        /// </summary>
        public int StartPosition { get; set; }

        /// <summary>
        /// Character position where this chunk ends in the original document
        /// </summary>
        public int EndPosition { get; set; }

        /// <summary>
        /// Length of the chunk in characters
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Whether this chunk has been successfully stored in Weaviate
        /// </summary>
        public bool IsStoredInWeaviate { get; set; }

        /// <summary>
        /// When this chunk was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When this chunk was stored in Weaviate
        /// </summary>
        public DateTime? StoredAt { get; set; }

        /// <summary>
        /// Optional metadata about this chunk (JSON format)
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// Error message if storing in Weaviate failed
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
