namespace AISwarm.DataLayer.Entities;

/// <summary>
///     Represents a work item that can be assigned to and executed by an agent
/// </summary>
public class WorkItem
{
    /// <summary>
    ///     Unique identifier for the work item
    /// </summary>
    public string Id
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    ///     ID of the agent this work item is assigned to
    /// </summary>
    public string? AgentId
    {
        get;
        set;
    }

    /// <summary>
    ///     Current status of the work item
    /// </summary>
    public TaskStatus Status
    {
        get;
        set;
    }

    /// <summary>
    ///     Routing tag indicating which persona should execute this work item
    /// </summary>
    public string? PersonaId
    {
        get;
        set;
    }

    /// <summary>
    ///     Description of what the agent should accomplish
    /// </summary>
    public string Description
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    ///     Priority of the task (higher values = higher priority)
    /// </summary>
    public TaskPriority Priority
    {
        get;
        set;
    } = TaskPriority.Normal;

    /// <summary>
    ///     When the work item was created
    /// </summary>
    public DateTime CreatedAt
    {
        get;
        set;
    }

    /// <summary>
    ///     When the agent started working on this item
    /// </summary>
    public DateTime? StartedAt
    {
        get;
        set;
    }

    /// <summary>
    ///     When the work item was completed or failed
    /// </summary>
    public DateTime? CompletedAt
    {
        get;
        set;
    }

    /// <summary>
    ///     Optional result or output from work item execution
    /// </summary>
    public string? Result
    {
        get;
        set;
    }
}
