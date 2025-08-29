using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure.Eventing;
using AISwarm.Shared.Constants;
using AISwarm.Shared.Extensions;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;

namespace AISwarm.Infrastructure;

/// <summary>
///     Domain service for managing agent state transitions with business rules and event publishing
/// </summary>
public interface IAgentStateService
{
    /// <summary>
    ///     Kills an agent, handling process termination and task cleanup
    /// </summary>
    Task<bool> KillAsync(string agentId, DateTime timestamp);

    /// <summary>
    ///     Transitions agent from Starting to Running with heartbeat update
    /// </summary>
    Task<bool> ActivateAsync(string agentId, DateTime timestamp);
}

/// <summary>
///     Implementation of agent state service with centralized business logic
/// </summary>
public class AgentStateService(
    IDatabaseScopeService scopeService,
    IAgentNotificationService notificationService,
    IProcessTerminationService processTerminationService) : IAgentStateService
{
    /// <inheritdoc />
    public async Task<bool> KillAsync(string agentId, DateTime timestamp)
    {
        using var scope = scopeService.GetWriteScope();
        var agent = await scope.Agents.FindAsync(agentId);

        if (agent == null || !agent.Status.CanBeKilled())
            return false;

        // Terminate process if available
        if (!string.IsNullOrEmpty(agent.ProcessId)) await processTerminationService.KillProcessAsync(agent.ProcessId);

        var oldStatus = agent.Status;
        agent.Kill(timestamp);

        // Fail all in-progress tasks
        var inProgressTasks = scope.Tasks
            .Where(t => t.AgentId == agentId)
            .Where(t => t.Status == TaskStatus.InProgress)
            .ToList();

        foreach (var task in inProgressTasks)
        {
            task.Status = TaskStatus.Failed;
            task.Result = TaskFailureReasons.AgentTerminated;
            task.CompletedAt = timestamp;
        }

        await scope.SaveChangesAsync();
        scope.Complete();

        // Publish events
        await notificationService.PublishAgentKilled(agentId, TaskFailureReasons.AgentTerminated);
        await notificationService.PublishAgentStatusChanged(
            agentId, oldStatus.ToString(), AgentStatus.Killed.ToString());

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ActivateAsync(string agentId, DateTime timestamp)
    {
        using var scope = scopeService.GetWriteScope();
        var agent = await scope.Agents.FindAsync(agentId);

        if (agent == null || agent.Status != AgentStatus.Starting)
            return false;

        agent.Status = AgentStatus.Running;
        agent.LastHeartbeat = timestamp;
        if (agent.StartedAt == default)
            agent.StartedAt = timestamp;

        await scope.SaveChangesAsync();
        scope.Complete();

        await notificationService.PublishAgentStatusChanged(
            agentId, nameof(AgentStatus.Starting), nameof(AgentStatus.Running));

        return true;
    }
}
