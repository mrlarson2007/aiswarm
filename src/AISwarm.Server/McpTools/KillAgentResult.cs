namespace AISwarm.Server.McpTools;

public class KillAgentResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? AgentId { get; init; }

    public static KillAgentResult Failure(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    public static KillAgentResult SuccessWith(string agentId) => new()
    {
        Success = true,
        AgentId = agentId
    };
}