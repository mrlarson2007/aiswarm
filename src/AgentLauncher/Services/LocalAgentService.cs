using AISwarm.Shared.Contracts;
using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Database;
using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentLauncher.Services;

/// <summary>
/// Local agent service focused on launcher's specific needs:
/// - Agent registration and tracking
/// - Local health monitoring  
/// - Process lifecycle management
/// </summary>
public class LocalAgentService : ILocalAgentService
{
    private readonly ITimeService _timeService;
    private readonly IDatabaseScopeService _scopeService;
    private readonly IProcessTerminationService? _processTerminationService;

    public LocalAgentService(
        ITimeService timeService, 
        IDatabaseScopeService scopeService, 
        IProcessTerminationService? processTerminationService = null)
    {
        _timeService = timeService;
        _scopeService = scopeService;
        _processTerminationService = processTerminationService;
    }

    /// <summary>
    /// Register a new agent with the launcher
    /// </summary>
    public async Task<string> RegisterAgentAsync(AgentRegistrationRequest request)
    {
        using var scope = _scopeService.CreateWriteScope();
        
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

        scope.Agents.Add(agent);
        await scope.SaveChangesAsync();
        scope.Complete();
        
        return agentId;
    }

    /// <summary>
    /// Get agent information by ID
    /// </summary>
    public async Task<Agent?> GetAgentAsync(string agentId)
    {
        using var scope = _scopeService.CreateReadScope();
        return await scope.Agents.FindAsync(agentId);
    }

    /// <summary>
    /// Update agent heartbeat
    /// </summary>
    public async Task<bool> UpdateHeartbeatAsync(string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        
        var agent = await scope.Agents.FindAsync(agentId);
        if (agent != null)
        {
            agent.UpdateHeartbeat(_timeService.UtcNow);
            await scope.SaveChangesAsync();
            scope.Complete();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Mark agent as running with process ID
    /// </summary>
    public async Task MarkAgentRunningAsync(
        string agentId, 
        string processId)
    {
        using var scope = _scopeService.CreateWriteScope();
        
        var agent = await scope.Agents.FindAsync(agentId);
        if (agent != null)
        {
            agent.Status = AISwarm.DataLayer.Entities.AgentStatus.Running;
            agent.ProcessId = processId;
            agent.StartedAt = _timeService.UtcNow;
            await scope.SaveChangesAsync();
            scope.Complete();
        }
    }

    /// <summary>
    /// Stop agent and update status
    /// </summary>
    public async Task StopAgentAsync(string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        
        var agent = await scope.Agents.FindAsync(agentId);
        if (agent != null)
        {
            agent.Stop(_timeService.UtcNow);
            await scope.SaveChangesAsync();
            scope.Complete();
        }
    }

    /// <summary>
    /// Forcibly kill an agent and update status
    /// </summary>
    public async Task KillAgentAsync(string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        
        var agent = await scope.Agents.FindAsync(agentId);
        if (agent != null)
        {
            // Attempt to kill the actual process if we have a process ID and termination service
            if (!string.IsNullOrEmpty(agent.ProcessId) && _processTerminationService != null)
            {
                await _processTerminationService.KillProcessAsync(agent.ProcessId);
            }
            
            agent.Kill(_timeService.UtcNow);
            
            // TODO: Update any pending tasks assigned to this agent once task infrastructure is implemented
            // - Mark tasks as failed/cancelled due to agent termination
            // - Potentially reassign tasks to other available agents
            // - Update task status and add termination reason
            
            await scope.SaveChangesAsync();
            scope.Complete();
        }
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