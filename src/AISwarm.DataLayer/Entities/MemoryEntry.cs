using System.ComponentModel.DataAnnotations;

namespace AISwarm.DataLayer.Entities;

/// <summary>
/// Represents a memory entry in the coordination system for agent communication and state persistence.
/// </summary>
public class MemoryEntry
{
    /// <summary>
    /// Unique identifier for the memory entry
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Namespace for memory isolation (e.g., "planning", "agent-{id}", "shared", "task-results")
    /// </summary>
    [Required]
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Key for the memory entry within the namespace
    /// </summary>
    [Required]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The stored value as JSON or plain text
    /// </summary>
    [Required]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Content type (json, text, binary, etc.)
    /// </summary>
    public string Type { get; set; } = "json";

    /// <summary>
    /// JSON metadata for extensibility and rich queries
    /// </summary>
    public string? Metadata
    {
        get; set;
    }

    /// <summary>
    /// Whether the content is compressed
    /// </summary>
    public bool IsCompressed
    {
        get; set;
    }

    /// <summary>
    /// Size of the content in bytes
    /// </summary>
    public int Size
    {
        get; set;
    }

    /// <summary>
    /// When the entry was created
    /// </summary>
    public DateTime CreatedAt
    {
        get; set;
    }

    /// <summary>
    /// When the entry was last updated
    /// </summary>
    public DateTime LastUpdatedAt
    {
        get; set;
    }

    /// <summary>
    /// When the entry was last accessed (for analytics)
    /// </summary>
    public DateTime? AccessedAt
    {
        get; set;
    }

    /// <summary>
    /// Number of times the entry has been accessed
    /// </summary>
    public int AccessCount
    {
        get; set;
    }
}
