namespace AgentLauncher.Services;

/// <summary>
/// Interface for managing agent lifecycle with database persistence
/// </summary>
public interface ILocalAgentService
{
    Task<string> RegisterAgentAsync(AgentRegistrationRequest request);
    Task<AISwarm.DataLayer.Entities.Agent?> GetAgentAsync(string agentId);
    Task<bool> UpdateHeartbeatAsync(string agentId);
    Task MarkAgentRunningAsync(string agentId, string processId);
    Task StopAgentAsync(string agentId);
    Task KillAgentAsync(string agentId);
}
