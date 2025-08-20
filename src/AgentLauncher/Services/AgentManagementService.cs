using System.Collections.Concurrent;

namespace AgentLauncher.Services;

public class AgentManagementService : IAgentManagementService
{
    private readonly ConcurrentDictionary<string, AgentInstance> _agents = new();

    public Task<AgentInstance> StartAgentAsync(string agentType, string workingDirectory, string? model = null, string? worktreeName = null)
    {
        var agentInstance = new AgentInstance
        {
            Id = Guid.NewGuid().ToString(),
            AgentType = agentType,
            WorkingDirectory = workingDirectory,
            Status = AgentStatus.Running,
            StartedAt = DateTime.UtcNow,
            Model = model,
            WorktreeName = worktreeName
        };

        _agents[agentInstance.Id] = agentInstance;
        return Task.FromResult(agentInstance);
    }

    public Task<bool> StopAgentAsync(string agentId)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.Status = AgentStatus.Stopped;
            agent.StoppedAt = DateTime.UtcNow;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<AgentInstance?> GetAgentStatusAsync(string agentId)
    {
        _agents.TryGetValue(agentId, out var agent);
        return Task.FromResult(agent);
    }

    public Task<IEnumerable<AgentInstance>> GetAllAgentsAsync()
    {
        return Task.FromResult(_agents.Values.AsEnumerable());
    }
}