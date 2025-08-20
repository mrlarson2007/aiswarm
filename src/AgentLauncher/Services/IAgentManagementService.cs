namespace AgentLauncher.Services;

public interface IAgentManagementService
{
    Task<AgentInstance> StartAgentAsync(string agentType, string workingDirectory, string? model = null, string? worktreeName = null);
    Task<bool> StopAgentAsync(string agentId);
    Task<AgentInstance?> GetAgentStatusAsync(string agentId);
    Task<IEnumerable<AgentInstance>> GetAllAgentsAsync();
}