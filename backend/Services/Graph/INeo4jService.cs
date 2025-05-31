using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neo4j.Driver; // Added for IResultSummary

namespace SwAIvyn.Services.Graph
{    /// <summary>
    /// Represents a node in the Neo4j graph database
    /// </summary>
    public class GraphNode
    {
        /// <summary>
        /// Gets or sets the unique identifier for the node
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the labels for the node
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the properties for the node
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Represents a relationship in the Neo4j graph database
    /// </summary>
    public class GraphRelationship
    {
        /// <summary>
        /// Gets or sets the unique identifier for the relationship
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the relationship
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the start node ID
        /// </summary>
        public string StartNodeId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the end node ID
        /// </summary>
        public string EndNodeId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the properties for the relationship
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
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
        /// Initializes the Neo4j database schema with constraints and vector indexes
        /// </summary>
        Task InitializeDatabaseSchemaAsync();

        /// <summary>
        /// Creates a node in the graph database
        /// </summary>
        /// <param name="labels">Node labels</param>
        /// <param name="properties">Node properties</param>
        /// <returns>The created node</returns>
        Task<GraphNode?> CreateNodeAsync(List<string> labels, Dictionary<string, object> properties);

        /// <summary>
        /// Creates a relationship between two nodes
        /// </summary>
        /// <param name="startNodeId">Start node ID</param>
        /// <param name="endNodeId">End node ID</param>
        /// <param name="type">Relationship type</param>
        /// <param name="properties">Relationship properties</param>
        /// <returns>The created relationship</returns>
        Task<GraphRelationship?> CreateRelationshipAsync(string startNodeId, string endNodeId, string type, Dictionary<string, object>? properties = null);

        /// <summary>
        /// Gets a node by ID
        /// </summary>
        /// <param name="id">Node ID</param>
        /// <returns>The node or null if not found</returns>
        Task<GraphNode?> GetNodeAsync(string id);

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
        /// <returns>A list of relationships</returns>
        Task<List<GraphRelationship>> GetRelationshipsByTypeAsync(string type, int limit = 100);        /// <summary>
        /// Executes a Cypher query and returns the results.
        /// Primarily uses the HTTP endpoint.
        /// </summary>
        /// <param name="query">The Cypher query to execute.</param>
        /// <param name="parameters">Parameters for the Cypher query.</param>
        /// <returns>A list of dictionaries, where each dictionary represents a row in the result set.</returns>
        Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string query, Dictionary<string, object>? parameters = null);

        /// <summary>
        /// Executes a write query against Neo4j with parameters using the Bolt driver.
        /// </summary>
        /// <param name="query">The Cypher query to execute.</param>
        /// <param name="parameters">Parameters for the query.</param>
        /// <returns>A summary of the query execution.</returns>
        Task<List<Dictionary<string, object>>> ExecuteWriteQueryAsync(string query, Dictionary<string, object> parameters);

        /// <summary>
        /// Deletes a node from Neo4j by its internal ID.
        /// </summary>
        /// <param name="nodeId">The internal Neo4j ID of the node to delete.</param>
        /// <returns>True if deletion was successful, false otherwise.</returns>
        Task<bool> DeleteNodeAsync(string nodeId);

        /// <summary>
        /// Deletes a relationship from Neo4j by its internal ID.
        /// </summary>
        /// <param name="relationshipId">The internal Neo4j ID of the relationship to delete.</param>
        /// <returns>True if deletion was successful, false otherwise.</returns>
        Task<bool> DeleteRelationshipAsync(string relationshipId);

        /// <summary>
        /// Pings the Neo4j server to check its availability.
        /// </summary>
        /// <returns>True if the server is available, false otherwise.</returns>
        Task<bool> PingAsync();

        /// <summary>
        /// Checks the connection status to Neo4j.
        /// </summary>
        /// <returns>True if connected, false otherwise.</returns>
        Task<bool> CheckConnectionAsync();        /// <summary>
        /// Gets the current status of the Neo4j service.
        /// </summary>
        /// <returns>A dictionary containing status information.</returns>
        Dictionary<string, object> GetStatus();

        /// <summary>
        /// Gets the current status of the Neo4j service asynchronously.
        /// </summary>
        /// <returns>A task that returns a dictionary containing status information.</returns>
        Task<Dictionary<string, object>> GetStatusAsync();
    }
}
