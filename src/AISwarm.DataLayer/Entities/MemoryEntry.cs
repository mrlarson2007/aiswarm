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

    public DateTimeOffset LastUpdatedAt
    {
        get;
        set;
    } = default;
}
