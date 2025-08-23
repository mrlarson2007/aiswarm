namespace AISwarm.Server.McpTools;

/// <summary>
/// Result of creating a task via MCP tool
/// </summary>
public class CreateTaskResult
{
    /// <summary>
    /// Whether the task was successfully created
    /// </summary>
    public bool Success
    {
        get; set;
    }

    /// <summary>
    /// ID of the created task (only populated on success)
    /// </summary>
    public string? TaskId
    {
        get; set;
    }

    /// <summary>
    /// Error message if task creation failed
    /// </summary>
    public string? ErrorMessage
    {
        get; set;
    }

    /// <summary>
    /// Creates a successful result with task ID
    /// </summary>
    public static CreateTaskResult SuccessWithTaskId(string taskId) => new()
    {
        Success = true,
        TaskId = taskId
    };

    /// <summary>
    /// Creates a failure result with error message
    /// </summary>
    public static CreateTaskResult Failure(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
