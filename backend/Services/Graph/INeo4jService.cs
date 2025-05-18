using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SwAIvyn.Services.Graph
{
    /// <summary>
    /// Represents a node in the Neo4j graph database
    /// </summary>
    public class GraphNode
    {
        /// <summary>
        /// Gets or sets the unique identifier for the node
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the labels for the node
        /// </summary>
        public List<string> Labels { get; set; }

        /// <summary>
        /// Gets or sets the properties for the node
        /// </summary>
        public Dictionary<string, object> Properties { get; set; }
    }

    /// <summary>
    /// Represents a relationship in the Neo4j graph database
    /// </summary>
    public class GraphRelationship
    {
        /// <summary>
        /// Gets or sets the unique identifier for the relationship
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the type of the relationship
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the start node ID
        /// </summary>
        public string StartNodeId { get; set; }

        /// <summary>
        /// Gets or sets the end node ID
        /// </summary>
        public string EndNodeId { get; set; }

        /// <summary>
        /// Gets or sets the properties for the relationship
        /// </summary>
        public Dictionary<string, object> Properties { get; set; }
    }

    /// <summary>
    /// Interface for Neo4j graph database service
    /// </summary>
    public interface INeo4jService
    {
        /// <summary>
        /// Initializes the Neo4j service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Creates a node in the graph database
        /// </summary>
        /// <param name="labels">Node labels</param>
        /// <param name="properties">Node properties</param>
        /// <returns>The created node</returns>
        Task<GraphNode> CreateNodeAsync(List<string> labels, Dictionary<string, object> properties);

        /// <summary>
        /// Creates a relationship between two nodes
        /// </summary>
        /// <param name="startNodeId">Start node ID</param>
        /// <param name="endNodeId">End node ID</param>
        /// <param name="type">Relationship type</param>
        /// <param name="properties">Relationship properties</param>
        /// <returns>The created relationship</returns>
        Task<GraphRelationship> CreateRelationshipAsync(string startNodeId, string endNodeId, string type, Dictionary<string, object> properties = null);

        /// <summary>
        /// Gets a node by ID
        /// </summary>
        /// <param name="id">Node ID</param>
        /// <returns>The node or null if not found</returns>
        Task<GraphNode> GetNodeAsync(string id);

        /// <summary>
        /// Gets nodes by label
        /// </summary>
        /// <param name="label">Node label</param>
        /// <param name="limit">Maximum number of nodes to return</param>
        /// <returns>List of nodes</returns>
        Task<List<GraphNode>> GetNodesByLabelAsync(string label, int limit = 100);

        /// <summary>
        /// Gets nodes by property
        /// </summary>
        /// <param name="propertyName">Property name</param>
        /// <param name="propertyValue">Property value</param>
        /// <param name="limit">Maximum number of nodes to return</param>
        /// <returns>List of nodes</returns>
        Task<List<GraphNode>> GetNodesByPropertyAsync(string propertyName, object propertyValue, int limit = 100);

        /// <summary>
        /// Gets relationships by type
        /// </summary>
        /// <param name="type">Relationship type</param>
        /// <param name="limit">Maximum number of relationships to return</param>
        /// <returns>List of relationships</returns>
        Task<List<GraphRelationship>> GetRelationshipsByTypeAsync(string type, int limit = 100);

        /// <summary>
        /// Executes a Cypher query
        /// </summary>
        /// <param name="query">Cypher query</param>
        /// <param name="parameters">Query parameters</param>
        /// <returns>Query result as a list of dictionaries</returns>
        Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string query, Dictionary<string, object> parameters = null);

        /// <summary>
        /// Deletes a node by ID
        /// </summary>
        /// <param name="id">Node ID</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteNodeAsync(string id);

        /// <summary>
        /// Deletes a relationship by ID
        /// </summary>
        /// <param name="id">Relationship ID</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteRelationshipAsync(string id);

        /// <summary>
        /// Gets the status of the Neo4j service
        /// </summary>
        /// <returns>Status information</returns>
        Task<Dictionary<string, object>> GetStatusAsync();
    }
}
