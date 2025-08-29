namespace AISwarm.Server.Entities;

public class GetTaskStatusResult
{
    public bool Success
    {
        get;
        init;
    }

    public string? ErrorMessage
    {
        get;
        init;
    }

    public string? TaskId
    {
        get;
        init;
    }

    public string? Status
    {
        get;
        init;
    }

    public string? AgentId
    {
        get;
        init;
    }

    public DateTime? StartedAt
    {
        get;
        init;
    }

    public DateTime? CompletedAt
    {
        get;
        init;
    }

    public static GetTaskStatusResult Failure(string message)
    {
        return new GetTaskStatusResult { Success = false, ErrorMessage = message };
    }

    public static GetTaskStatusResult SuccessWith(
        string? taskId,
        string? status,
        string? agentId,
        DateTime? startedAt,
        DateTime? completedAt)
    {
        return new GetTaskStatusResult
        {
            Success = true,
            TaskId = taskId,
            Status = status,
            AgentId = agentId,
            StartedAt = startedAt,
            CompletedAt = completedAt
        };
    }
}
