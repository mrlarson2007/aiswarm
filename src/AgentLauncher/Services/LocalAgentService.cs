using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Entities;
using System.Collections.Concurrent;

namespace AgentLauncher.Services;

/// <summary>
/// Local agent service focused on launcher's specific needs:
/// - Agent registration and tracking
/// - Local health monitoring  
/// - Process lifecycle management
/// </summary>
public class LocalAgentService
{
    private readonly ITimeService _timeService;
    private readonly ConcurrentDictionary<string, Agent> _agents = new();

    public LocalAgentService(ITimeService timeService)
    {
        _timeService = timeService;
    }

    /// <summary>
    /// Register a new agent with the launcher
    /// </summary>
    public Task<string> RegisterAgentAsync(AgentRegistrationRequest request)
    {
        var agentId = Guid.NewGuid().ToString();
        var currentTime = _timeService.UtcNow;
        
        var agent = new Agent
        {
            Id = agentId,
            PersonaId = request.PersonaId,
            AgentType = request.AgentType,
            WorkingDirectory = request.WorkingDirectory,
            Status = AISwarm.DataLayer.Entities.AgentStatus.Starting,
            RegisteredAt = currentTime,
            LastHeartbeat = currentTime,
            Model = request.Model,
            WorktreeName = request.WorktreeName
        };

        _agents[agentId] = agent;
        return Task.FromResult(agentId);
    }

    /// <summary>
    /// Get agent information by ID
    /// </summary>
    public Task<Agent?> GetAgentAsync(string agentId)
    {
        _agents.TryGetValue(agentId, out var agent);
        return Task.FromResult(agent);
    }

    /// <summary>
    /// Update agent heartbeat
    /// </summary>
    public Task<bool> UpdateHeartbeatAsync(string agentId)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.UpdateHeartbeat(_timeService.UtcNow);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <summary>
    /// Mark agent as running with process ID
    /// </summary>
    public Task MarkAgentRunningAsync(string agentId, string processId)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.Status = AISwarm.DataLayer.Entities.AgentStatus.Running;
            agent.ProcessId = processId;
            agent.StartedAt = _timeService.UtcNow;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop agent and update status
    /// </summary>
    public Task StopAgentAsync(string agentId)
    {
        if (_agents.TryGetValue(agentId, out var agent))
        {
            agent.Stop(_timeService.UtcNow);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Request model for registering an agent in the launcher
/// </summary>
public record AgentRegistrationRequest
{
    public string PersonaId { get; init; } = string.Empty;
    public string AgentType { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
    public string? Model { get; init; }
    public string? WorktreeName { get; init; }
}