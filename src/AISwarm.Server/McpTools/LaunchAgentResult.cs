namespace AISwarm.Server.McpTools;

public class LaunchAgentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? AgentId { get; init; }
    public string? ProcessId { get; init; }

    public static LaunchAgentResult Failure(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    public static LaunchAgentResult SuccessWith(string agentId, string? processId = null) => new()
    {
        Success = true,
        AgentId = agentId,
        ProcessId = processId
    };
}