namespace AISwarm.Server.Entities;

public class LaunchAgentResult
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

    public string? ProcessId
    {
        get;
        init;
    }

    public static LaunchAgentResult Failure(string message)
    {
        return new LaunchAgentResult { Success = false, ErrorMessage = message };
    }

    public static LaunchAgentResult SuccessWith(string agentId)
    {
        return new LaunchAgentResult { Success = true, AgentId = agentId };
    }

    public static LaunchAgentResult SuccessWith(string agentId, string? processId)
    {
        return new LaunchAgentResult { Success = true, AgentId = agentId, ProcessId = processId };
    }
}
