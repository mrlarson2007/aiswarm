using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Shared.Constants;

namespace AISwarm.Infrastructure;

/// <summary>
/// Local agent service focused on launcher's specific needs:
/// - Agent registration and tracking
/// - Local health monitoring
/// - Process lifecycle management
/// </summary>
public class LocalAgentService(
    ITimeService timeService,
    IDatabaseScopeService scopeService,
    IAgentStateService agentStateService)
    : ILocalAgentService
{
    /// <summary>
    /// Register a new agent with the launcher
    /// </summary>
    public async Task<string> RegisterAgentAsync(AgentRegistrationRequest request)
    {
        using var scope = scopeService.CreateWriteScope();

        var agentId = Guid.NewGuid().ToString();
        var currentTime = timeService.UtcNow;

        var agent = new Agent
        {
            Id = agentId,
            PersonaId = request.PersonaId,
            WorkingDirectory = request.WorkingDirectory,
            Status = AgentStatus.Starting,
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
    /// Update agent heartbeat and transition Starting agents to Running
    /// </summary>
    public async Task<bool> UpdateHeartbeatAsync(string agentId)
    {
        using var scope = scopeService.CreateWriteScope();

        var agent = await scope.Agents.FindAsync(agentId);
        if (agent != null)
        {
            agent.UpdateHeartbeat(timeService.UtcNow);

            // If agent is starting and actively polling for tasks, transition to running
            if (agent.Status == AgentStatus.Starting)
            {
                await agentStateService.ActivateAsync(agentId, timeService.UtcNow);
            }

            await scope.SaveChangesAsync();
            scope.Complete();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Forcibly kill an agent and update status
    /// </summary>
    public async Task KillAgentAsync(string agentId)
    {
        await agentStateService.KillAsync(agentId, timeService.UtcNow);
    }
}

/// <summary>
/// Request model for registering an agent in the launcher
/// </summary>
public record AgentRegistrationRequest
{
    public string PersonaId { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
    public string? Model
    {
        get; init;
    }
    public string? WorktreeName
    {
        get; init;
    }
}
