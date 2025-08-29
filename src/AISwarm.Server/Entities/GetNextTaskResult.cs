namespace AISwarm.Server.Entities;

/// <summary>
///     Result of getting the next task via MCP tool
/// </summary>
public class GetNextTaskResult
{
    /// <summary>
    ///     Whether the operation was successful
    /// </summary>
    public bool Success
    {
        get;
        set;
    }

    /// <summary>
    ///     ID of the retrieved task (null if no tasks available)
    /// </summary>
    public string? TaskId
    {
        get;
        set;
    }

    /// <summary>
    ///     PersonaId for the task (null if no tasks available)
    /// </summary>
    public string? PersonaId
    {
        get;
        set;
    }

    /// <summary>
    ///     Description of the task (null if no tasks available)
    /// </summary>
    public string? Description
    {
        get;
        set;
    }

    /// <summary>
    ///     Message with reinforcing prompt to call the tool again
    /// </summary>
    public string? Message
    {
        get;
        set;
    }

    /// <summary>
    ///     Error message if operation failed
    /// </summary>
    public string? ErrorMessage
    {
        get;
        set;
    }


    /// <summary>
    ///     Creates a failure result with error message
    /// </summary>
    public static GetNextTaskResult Failure(string errorMessage)
    {
        return new GetNextTaskResult { Success = false, ErrorMessage = errorMessage };
    }

    /// <summary>
    ///     Creates a successful result indicating no tasks are available
    /// </summary>
    public static GetNextTaskResult NoTasksAvailable()
    {
        return new GetNextTaskResult
        {
            Success = true,
            TaskId = $"system:requery:{Guid.NewGuid():N}",
            PersonaId =
                "cooperative-agent",
            Description =
                "No tasks available right now. Immediately call the get_next_task tool again to check for new tasks.",
            Message = "No tasks available. Please call this tool again to check for new tasks."
        };
    }

    /// <summary>
    ///     Creates a successful result with task information
    /// </summary>
    public static GetNextTaskResult SuccessWithTask(
        string taskId,
        string personaId,
        string description)
    {
        return new GetNextTaskResult
        {
            Success = true,
            TaskId = taskId,
            PersonaId = personaId,
            Description = description,
            Message =
                "Task retrieved successfully. After completing this task, call this tool again to get the next task."
        };
    }
}
