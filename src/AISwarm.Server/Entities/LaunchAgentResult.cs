using AISwarm.Shared.Models;

namespace AISwarm.Server.Entities;

public class LaunchAgentResult : Result<LaunchAgentResult>
{
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

    public static LaunchAgentResult SuccessWith(string agentId)
    {
        return new LaunchAgentResult { Success = true, AgentId = agentId };
    }
}
