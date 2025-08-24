namespace AISwarm.Server.McpTools;

public class ListAgentsResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public AgentInfo[]? Agents { get; init; }

    public static ListAgentsResult Failure(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    public static ListAgentsResult SuccessWith(AgentInfo[] agents) => new()
    {
        Success = true,
        Agents = agents
    };
}

public class AgentInfo
{
    public string AgentId { get; init; } = string.Empty;
    public string PersonaId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ProcessId { get; init; }
    public DateTime RegisteredAt { get; init; }
    public DateTime LastHeartbeat { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? Model { get; init; }
    public string? WorktreeName { get; init; }
}