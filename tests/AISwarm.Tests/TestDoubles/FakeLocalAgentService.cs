using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;

namespace AISwarm.Tests.TestDoubles;

public class FakeLocalAgentService : ILocalAgentService
{
    public string FailureMessage { get; set; } = string.Empty;
    public bool ShouldFail => !string.IsNullOrEmpty(FailureMessage);
    public string RegisteredAgentId { get; set; } = "test-agent-123";
    public Agent? RetrievedAgent
    {
        get; set;
    }

    public Task<string> RegisterAgentAsync(AgentRegistrationRequest request)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.FromResult(RegisteredAgentId);
    }

    public Task<Agent?> GetAgentAsync(string agentId)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.FromResult(RetrievedAgent);
    }

    public Task<bool> UpdateHeartbeatAsync(string agentId)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.FromResult(true);
    }

    public Task MarkAgentRunningAsync(string agentId, string processId)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.CompletedTask;
    }

    public Task StopAgentAsync(string agentId)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.CompletedTask;
    }

    public Task KillAgentAsync(string agentId)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.CompletedTask;
    }
}
