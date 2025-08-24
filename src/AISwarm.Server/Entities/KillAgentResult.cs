namespace AISwarm.Server.Entities;

public class KillAgentResult
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

    public string? AgentId
    {
        get;
        init;
    }

    public static KillAgentResult Failure(string message)
    {
        return new KillAgentResult { Success = false, ErrorMessage = message };
    }

    public static KillAgentResult SuccessWith(string agentId)
    {
        return new KillAgentResult { Success = true, AgentId = agentId };
    }
}
