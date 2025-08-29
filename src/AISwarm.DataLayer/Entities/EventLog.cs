using System.ComponentModel.DataAnnotations;

namespace AISwarm.DataLayer.Entities;

/// <summary>
///     Durable audit log for important system events including task lifecycle and agent state changes.
///     Provides a historical record for debugging, monitoring, and compliance purposes.
/// </summary>
public class EventLog
{
    /// <summary>
    ///     Unique identifier for the event log entry
    /// </summary>
    [Key]
    public string Id
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    ///     Type of event (TaskCreated, TaskCompleted, TaskFailed, AgentRegistered, AgentKilled, etc.)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string EventType
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    ///     UTC timestamp when the event occurred
    /// </summary>
    [Required]
    public DateTime Timestamp
    {
        get;
        set;
    }

    /// <summary>
    ///     Actor who triggered the event (agent ID, user, system)
    /// </summary>
    [MaxLength(200)]
    public string? Actor
    {
        get;
        set;
    }

    /// <summary>
    ///     Correlation ID for tracing related events
    /// </summary>
    [MaxLength(100)]
    public string? CorrelationId
    {
        get;
        set;
    }

    /// <summary>
    ///     Serialized event payload containing event-specific data
    /// </summary>
    public string? Payload
    {
        get;
        set;
    }

    /// <summary>
    ///     Primary entity ID involved in the event (task ID, agent ID, etc.)
    /// </summary>
    [MaxLength(100)]
    public string? EntityId
    {
        get;
        set;
    }

    /// <summary>
    ///     Type of entity (Task, Agent, User, etc.)
    /// </summary>
    [MaxLength(50)]
    public string? EntityType
    {
        get;
        set;
    }

    /// <summary>
    ///     Event severity level (Information, Warning, Error, Critical)
    /// </summary>
    [MaxLength(20)]
    public string Severity
    {
        get;
        set;
    } = "Information";

    /// <summary>
    ///     Optional tags for filtering and categorization
    /// </summary>
    [MaxLength(500)]
    public string? Tags
    {
        get;
        set;
    }
}
