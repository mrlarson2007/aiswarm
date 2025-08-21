namespace AISwarm.DataLayer.Entities;

/// <summary>
/// Status of a task in the system
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// Task has been created but not yet started
    /// </summary>
    Pending,
    
    /// <summary>
    /// Task is currently being worked on by an agent
    /// </summary>
    InProgress,
    
    /// <summary>
    /// Task has been successfully completed
    /// </summary>
    Completed,
    
    /// <summary>
    /// Task was cancelled or failed
    /// </summary>
    Failed
}