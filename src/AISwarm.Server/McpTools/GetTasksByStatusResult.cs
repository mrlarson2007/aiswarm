namespace AISwarm.Server.McpTools;

public class GetTasksByStatusResult
{
    public bool Success
    {
        get; init;
    }
    public string? ErrorMessage
    {
        get; init;
    }
    public TaskInfo[]? Tasks
    {
        get; init;
    }

    public static GetTasksByStatusResult Failure(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    public static GetTasksByStatusResult SuccessWith(TaskInfo[] tasks) => new()
    {
        Success = true,
        Tasks = tasks
    };
}

public class TaskInfo
{
    public string TaskId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? AgentId
    {
        get; init;
    }
    public DateTime? StartedAt
    {
        get; init;
    }
    public DateTime? CompletedAt
    {
        get; init;
    }
}
