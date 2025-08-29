using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;

namespace AISwarm.Tests.TestDoubles;

public class FakeLocalAgentService : ILocalAgentService
{
    public string FailureMessage
    {
        get;
        set;
    } = string.Empty;

    public bool ShouldFail => !string.IsNullOrEmpty(FailureMessage);

    public string RegisteredAgentId
    {
        get;
        set;
    } = "test-agent-123";

    public Agent? RetrievedAgent
    {
        get;
        set;
    }

    public string? KilledAgentId
    {
        get;
        private set;
    }

    public Task<string> RegisterAgentAsync(AgentRegistrationRequest request)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.FromResult(RegisteredAgentId);
    }

    public Task<bool> UpdateHeartbeatAsync(string agentId)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.FromResult(true);
    }

    public Task KillAgentAsync(string agentId)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);
        KilledAgentId = agentId;
        return Task.CompletedTask;
    }

    public Task<Agent?> GetAgentAsync(string agentId)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.FromResult(RetrievedAgent);
    }
}
