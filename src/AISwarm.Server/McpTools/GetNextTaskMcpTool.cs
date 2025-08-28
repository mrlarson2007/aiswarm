using System.ComponentModel;
using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Eventing;
using AISwarm.Server.Entities;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;

namespace AISwarm.Server.McpTools;

/// <summary>
///     MCP tool implementation for agents to request their next task
/// </summary>
[McpServerToolType]
public class GetNextTaskMcpTool(
    IDatabaseScopeService scopeService,
    ILocalAgentService localAgentService,
    IWorkItemNotificationService workItemNotifications)
{
    public GetNextTaskConfiguration Configuration
    {
        get;
        set;
    } = GetNextTaskConfiguration.Production;

    /// <summary>
    ///     Gets the next pending task for the specified agent
    /// </summary>
    /// <param name="agentId">ID of the agent requesting a task</param>
    /// <param name="timeoutMs">Optional timeout in milliseconds to wait for a task before returning a synthetic no-task result</param>
    /// <returns>Result with task information or error message</returns>
    [McpServerTool(Name = "get_next_task")]
    [Description("Gets the next pending task for the specified agent")]
    public async Task<GetNextTaskResult> GetNextTaskAsync(
        [Description("ID of the agent requesting a task")]
        string agentId,
        [Description("Optional timeout in milliseconds to wait for a task before returning a synthetic no-task result")]
        int? timeoutMs = null)
    {
        var effective = new GetNextTaskConfiguration
        {
            TimeToWaitForTask = timeoutMs.HasValue && timeoutMs.Value > 0
                ? TimeSpan.FromMilliseconds(timeoutMs.Value)
                : Configuration.TimeToWaitForTask,
            PollingInterval = Configuration.PollingInterval
        };
        return await GetNextTaskAsync(agentId, effective);
    }

    /// <summary>
    ///     Gets the next pending task for the specified agent with custom configuration
    /// </summary>
    /// <param name="agentId">ID of the agent requesting a task</param>
    /// <param name="configuration">Polling configuration for timeouts and intervals</param>
    /// <returns>Result with task information or error message</returns>
    public async Task<GetNextTaskResult> GetNextTaskAsync(
        string agentId,
        GetNextTaskConfiguration configuration)
    {
        var agentInfo = await GetAgentInfoAsync(agentId);
        if (agentInfo == null)
        {
            return GetNextTaskResult
                .Failure($"Agent not found: {agentId}");
        }

        var preferredTaskId = await workItemNotifications.TryConsumeTaskCreatedAsync(
                agentId, agentInfo.PersonaId, CancellationToken.None);

        // Update agent heartbeat since the agent is actively requesting tasks
        await localAgentService.UpdateHeartbeatAsync(agentId);

        var preferred = await TryGetOrClaimTaskAsync(agentInfo, preferredTaskId);
        if (preferred != null)
            return preferred;

        // Wait for new task events
        using var cts = new CancellationTokenSource(configuration.TimeToWaitForTask);
        try
        {
            do
            {
                preferredTaskId = await TryGetNextTaskIdAsync(agentInfo, cts.Token);
                var result = await TryGetOrClaimTaskAsync(agentInfo, preferredTaskId);
                if (result != null)
                    return result;
            } while (preferredTaskId != null);
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                await cts.CancelAsync();
            }
        }

        return GetNextTaskResult.NoTasksAvailable();
    }

    private async Task<string?> TryGetNextTaskIdAsync(AgentInfo agentInfo, CancellationToken cancellationToken)
    {
        try
        {
            return await workItemNotifications
                .SubscribeForAgentOrPersona(agentInfo.AgentId, agentInfo.PersonaId, cancellationToken)
                .Select(e => (e.Payload as TaskCreatedPayload)?.TaskId)
                .Where(id => id != null)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Return null when cancelled (timeout reached)
            return null;
        }
    }

    private async Task<AgentInfo?> GetAgentInfoAsync(string agentId)
    {
        using var scope = scopeService.CreateReadScope();
        var agent = await scope.Agents.FindAsync(agentId);
        if (agent == null)
            return null;
        return new AgentInfo(agent.Id, agent.PersonaId);
    }

    private async Task<GetNextTaskResult?> TryGetOrClaimTaskAsync(
        AgentInfo agentInfo,
        string? preferredTaskId = null)
    {
        using var scope = scopeService.CreateReadScope();

        // If we received an event for a specific task, try to honor it first
        if (!string.IsNullOrWhiteSpace(preferredTaskId))
        {
            var evtTask = await scope.Tasks.FindAsync(preferredTaskId);
            if (evtTask != null && evtTask.Status == TaskStatus.Pending)
            {
                if (evtTask.AgentId == agentInfo.AgentId)
                {
                    return GetNextTaskResult.SuccessWithTask(
                        evtTask.Id,
                        evtTask.PersonaId ?? string.Empty,
                        evtTask.Description);
                }

                // If unassigned and persona matches (or no persona), try to claim this exact task
                if (string.IsNullOrEmpty(evtTask.AgentId) &&
                    (string.IsNullOrEmpty(evtTask.PersonaId) || evtTask.PersonaId == agentInfo.PersonaId))
                {
                    return await ClaimUnassignedTaskAsync(evtTask.Id, agentInfo.AgentId);
                }
            }
        }

        var pendingTask = await scope.Tasks
            .Where(t => t.AgentId == agentInfo.AgentId)
            .Where(t => t.Status == TaskStatus.Pending)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (pendingTask != null)
            return GetNextTaskResult.SuccessWithTask(
                pendingTask.Id,
                pendingTask.PersonaId ?? string.Empty,
                pendingTask.Description);

        var unassignedTask = await scope.Tasks
            .Where(t => t.AgentId == null || t.AgentId == string.Empty)
            .Where(t => t.Status == TaskStatus.Pending)
            .Where(t => string.IsNullOrEmpty(t.PersonaId) || t.PersonaId == agentInfo.PersonaId)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (unassignedTask != null)
            return await ClaimUnassignedTaskAsync(unassignedTask.Id, agentInfo.AgentId);

        return null;
    }

    /// <summary>
    ///     Claims an unassigned task by setting the AgentId to the requesting agent
    /// </summary>
    /// <param name="taskId">ID of the task to claim</param>
    /// <param name="agentId">ID of the agent claiming the task</param>
    /// <returns>Result with claimed task information or failure if task no longer available</returns>
    private async Task<GetNextTaskResult> ClaimUnassignedTaskAsync(
        string taskId,
        string agentId)
    {
        using var scope = scopeService.CreateWriteScope();

        // Re-fetch the task to ensure it's still unassigned (race condition protection)
        var task = await scope.Tasks.FindAsync(taskId);

        if (task == null)
            return GetNextTaskResult.NoTasksAvailable();

        // Verify task is still unassigned and pending
        if (!string.IsNullOrEmpty(task.AgentId) ||
            task.Status != TaskStatus.Pending)
            return GetNextTaskResult.NoTasksAvailable();

        // Claim the task
        task.AgentId = agentId;

        await scope.SaveChangesAsync();
        scope.Complete();

        return GetNextTaskResult.SuccessWithTask(
            task.Id,
            task.PersonaId ?? string.Empty,
            task.Description);
    }

    record AgentInfo(string AgentId, string PersonaId);
}
