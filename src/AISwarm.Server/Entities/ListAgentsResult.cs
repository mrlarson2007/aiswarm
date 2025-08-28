using AISwarm.Shared.Models;

namespace AISwarm.Server.Entities;

public class ListAgentsResult : Result<ListAgentsResult>
{
    public AgentInfo[]? Agents
    {
        get;
        init;
    }

    public static ListAgentsResult SuccessWith(AgentInfo[] agents)
    {
        return new ListAgentsResult { Success = true, Agents = agents };
    }
}

public class AgentInfo
{
    public string AgentId
    {
        get;
        init;
    } = string.Empty;

    public string PersonaId
    {
        get;
        init;
    } = string.Empty;

    public string Status
    {
        get;
        init;
    } = string.Empty;

    public string? ProcessId
    {
        get;
        init;
    }

    public DateTime RegisteredAt
    {
        get;
        init;
    }

    public DateTime LastHeartbeat
    {
        get;
        init;
    }

    public string? WorkingDirectory
    {
        get;
        init;
    }

    public string? Model
    {
        get;
        init;
    }

    public string? WorktreeName
    {
        get;
        init;
    }
}
