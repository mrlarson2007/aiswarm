using System.Collections.Concurrent;

namespace AgentLauncher.Services;

public class AgentManagementService : IAgentManagementService
{
    private readonly ConcurrentDictionary<string, AgentInstance> _agents = new();
    private readonly AgentHealthConfiguration _healthConfig;

    public AgentManagementService() : this(new AgentHealthConfiguration())
    {
    }

    public AgentManagementService(AgentHealthConfiguration healthConfig)
    {
        _healthConfig = healthConfig;
    }

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

    public Task<AgentHealthStatus> CheckAgentHealthAsync(string agentId, DateTime lastHeartbeat)
    {
        if (!_agents.TryGetValue(agentId, out var agent))
        {
            return Task.FromResult(new AgentHealthStatus
            {
                IsHealthy = false,
                Reason = "Agent not found",
                CheckTime = DateTime.UtcNow
            });
        }

        var timeSinceHeartbeat = DateTime.UtcNow - lastHeartbeat;
        var isHealthy = timeSinceHeartbeat <= _healthConfig.HeartbeatTimeout;

        return Task.FromResult(new AgentHealthStatus
        {
            IsHealthy = isHealthy,
            Reason = isHealthy ? "Healthy" : "heartbeat timeout exceeded",
            TimeSinceLastHeartbeat = timeSinceHeartbeat,
            LastHeartbeat = lastHeartbeat,
            CheckTime = DateTime.UtcNow
        });
    }
}