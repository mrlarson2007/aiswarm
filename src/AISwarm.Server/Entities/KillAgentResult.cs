using AISwarm.Shared.Models;

namespace AISwarm.Server.Entities;

public class KillAgentResult : Result<KillAgentResult>
{
    public string? AgentId
    {
        get;
        init;
    }

    public static KillAgentResult SuccessWith(string agentId)
    {
        return new KillAgentResult { Success = true, AgentId = agentId };
    }
}
