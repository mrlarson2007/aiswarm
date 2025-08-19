using AISwarm.Shared.Contracts;

namespace AISwarm.Server.Services;

/// <summary>
/// Agent registration and management service
/// Initial implementation for TDD RED phase - should fail tests
/// </summary>
public class AgentService : IAgentService
{
    public Task<string> RegisterAgentAsync(RegisterAgentRequest request)
    {
        // GREEN phase: Minimal implementation to make test pass
        var agentId = $"agent-{Guid.NewGuid():N}";
        return Task.FromResult(agentId);
    }
}