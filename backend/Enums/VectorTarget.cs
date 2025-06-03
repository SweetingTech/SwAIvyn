namespace SwAIvyn.Enums
{
    /// <summary>
    /// Defines the target vector store for memory items.
    /// Determines which vector database should be used for storage and retrieval.
    /// </summary>
    public enum VectorTarget
    {
        /// <summary>
        /// Store in Neo4j vector database (for long-term brain memories)
        /// </summary>
        Neo4j = 0,

        /// <summary>
        /// Store in Weaviate vector database (for file/document uploads)
        /// </summary>
        Weaviate = 1
    }
}
