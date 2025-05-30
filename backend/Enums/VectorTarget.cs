using System;

namespace SwAIvyn.Enums
{
    /// <summary>
    /// Enum defining the target vector store for memory items.
    /// Determines which vector database should be used for storage and retrieval.
    /// </summary>
    public enum VectorTarget
    {
        /// <summary>
        /// Neo4j vector store for brain memories with graph relationships.
        /// Used for personal knowledge, facts, events that benefit from graph connections.
        /// </summary>
        Neo4j = 0,

        /// <summary>
        /// Weaviate vector store for document knowledge and uploads.
        /// Used for external documents, file uploads, and structured knowledge base.
        /// </summary>
        Weaviate = 1
    }
}
