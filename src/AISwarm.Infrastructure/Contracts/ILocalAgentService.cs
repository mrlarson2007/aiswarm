namespace AISwarm.Infrastructure;

/// <summary>
///     Interface for managing agent lifecycle with database persistence
/// </summary>
public interface ILocalAgentService
{
    Task<string> RegisterAgentAsync(AgentRegistrationRequest request);
    Task<bool> UpdateHeartbeatAsync(string agentId);
    Task KillAgentAsync(string agentId);
}
