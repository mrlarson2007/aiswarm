using AISwarm.Shared.Models;

namespace AISwarm.Server.Entities;

/// <summary>
/// Result of creating a task via MCP tool
/// </summary>
public class CreateTaskResult : Result<CreateTaskResult>
{
    /// <summary>
    /// ID of the created task (only populated on success)
    /// </summary>
    public string? TaskId
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
}
