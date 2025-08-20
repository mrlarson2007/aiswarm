using System.Collections.Concurrent;
using AISwarm.DataLayer.Contracts;

namespace AgentLauncher.Services;

public class AgentManagementService : IAgentManagementService
{
    private readonly ConcurrentDictionary<string, AgentInstance> _agents = new();
    private readonly AgentHealthConfiguration _healthConfig;
    private readonly ITimeService _timeService;

    public AgentManagementService() : this(new AgentHealthConfiguration(), new SystemTimeService())
    {
    }

    public AgentManagementService(AgentHealthConfiguration healthConfig) : this(healthConfig, new SystemTimeService())
    {
    }

    public AgentManagementService(AgentHealthConfiguration healthConfig, ITimeService timeService)
    {
        _healthConfig = healthConfig;
        _timeService = timeService;
    }

    public Task<AgentInstance> StartAgentAsync(string agentType, string workingDirectory, string? model = null, string? worktreeName = null)
    {
        var agentInstance = new AgentInstance
        {
            Id = Guid.NewGuid().ToString(),
            AgentType = agentType,
            WorkingDirectory = workingDirectory,
            Status = AgentStatus.Running,
            StartedAt = _timeService.UtcNow,
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
            agent.StoppedAt = _timeService.UtcNow;
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
                CheckTime = _timeService.UtcNow
            });
        }

        var timeSinceHeartbeat = _timeService.UtcNow - lastHeartbeat;
        var isHealthy = timeSinceHeartbeat <= _healthConfig.HeartbeatTimeout;

        return Task.FromResult(new AgentHealthStatus
        {
            IsHealthy = isHealthy,
            Reason = isHealthy ? "Healthy" : "heartbeat timeout exceeded",
            TimeSinceLastHeartbeat = timeSinceHeartbeat,
            LastHeartbeat = lastHeartbeat,
            CheckTime = _timeService.UtcNow
        });
    }
}