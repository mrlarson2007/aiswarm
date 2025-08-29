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
    IWorkItemNotificationService workItemNotifications,
    ITimeService timeService)
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
    /// <param name="timeoutMs">Optional timeout in milliseconds to wait for a task before returning a synthetic no-task result. 
    /// Valid range: 0 to Int32.MaxValue (2,147,483,647ms ≈ 24.8 days). 
    /// - null (default): No wait, returns immediately if no tasks available
    /// - 0: No wait, same as null
    /// - Positive values: Wait up to specified milliseconds for new tasks
    /// - Negative values: Treated as 0 (no wait)
    /// Returns synthetic 'system:requery:...' task ID when timeout expires without finding tasks.</param>
    /// <returns>Result with task information or error message</returns>
    [McpServerTool(Name = "get_next_task")]
    [Description("Gets the next pending task for the specified agent")]
    public async Task<GetNextTaskResult> GetNextTaskAsync(
        [Description("ID of the agent requesting a task")]
        string agentId,
        [Description("Optional timeout in milliseconds (0-2147483647) to wait for tasks. null/0 = no wait, positive = wait duration")]
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

        // Wait for new task events with retry limit to prevent infinite loops
        using var cts = new CancellationTokenSource(configuration.TimeToWaitForTask);
        try
        {
            const int maxRetries = 10; // Prevent infinite loops in race conditions
            int retryCount = 0;

            do
            {
                preferredTaskId = await TryGetNextTaskIdAsync(agentInfo, cts.Token);
                var result = await TryGetOrClaimTaskAsync(agentInfo, preferredTaskId);
                if (result != null)
                    return result;

                retryCount++;
                if (retryCount >= maxRetries)
                {
                    // Break out of potential infinite loop after max retries
                    break;
                }
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
        using var scope = scopeService.GetReadScope();
        var agent = await scope.Agents.FindAsync(agentId);
        if (agent == null)
            return null;
        return new AgentInfo(agent.Id, agent.PersonaId);
    }

    private async Task<GetNextTaskResult?> TryGetOrClaimTaskAsync(
        AgentInfo agentInfo,
        string? preferredTaskId = null)
    {
        using var scope = scopeService.GetReadScope();

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

        // First, check for in-progress tasks assigned to this agent
        var inProgressTask = await scope.Tasks
            .Where(t => t.AgentId == agentInfo.AgentId)
            .Where(t => t.Status == TaskStatus.InProgress)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (inProgressTask != null)
        {
            return GetNextTaskResult.SuccessWithTask(
                inProgressTask.Id,
                inProgressTask.PersonaId ?? string.Empty,
                inProgressTask.Description);
        }

        // Then check for pending tasks assigned to this agent
        var pendingTask = await scope.Tasks
            .Where(t => t.AgentId == agentInfo.AgentId)
            .Where(t => t.Status == TaskStatus.Pending)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (pendingTask != null)
        {
            // Mark the assigned task as in progress when agent picks it up
            return await StartAssignedTaskAsync(pendingTask.Id, agentInfo.AgentId);
        }

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
    ///     Starts an assigned task by marking it as InProgress and setting the start time
    /// </summary>
    /// <param name="taskId">ID of the task to start</param>
    /// <param name="agentId">ID of the agent starting the task</param>
    /// <returns>Result with task information or failure if task no longer available</returns>
    private async Task<GetNextTaskResult> StartAssignedTaskAsync(
        string taskId,
        string agentId)
    {
        using var scope = scopeService.GetWriteScope();

        // Re-fetch the task to ensure it's still assigned to this agent
        var task = await scope.Tasks.FindAsync(taskId);

        if (task == null)
            return GetNextTaskResult.NoTasksAvailable();

        // Verify task is still assigned to this agent and pending
        if (task.AgentId != agentId || task.Status != TaskStatus.Pending)
            return GetNextTaskResult.NoTasksAvailable();

        // Start the task
        task.Status = TaskStatus.InProgress;
        task.StartedAt = timeService.UtcNow;

        await scope.SaveChangesAsync();
        scope.Complete();

        // Publish TaskClaimed event for consistency with ClaimUnassignedTaskAsync
        await workItemNotifications.PublishTaskClaimed(task.Id, agentId);

        return GetNextTaskResult.SuccessWithTask(
            task.Id,
            task.PersonaId ?? string.Empty,
            task.Description);
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
        using var scope = scopeService.GetWriteScope();

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
        task.Status = TaskStatus.InProgress;
        task.StartedAt = timeService.UtcNow;

        await scope.SaveChangesAsync();
        scope.Complete();

        // Publish TaskClaimed event
        await workItemNotifications.PublishTaskClaimed(task.Id, agentId);

        return GetNextTaskResult.SuccessWithTask(
            task.Id,
            task.PersonaId ?? string.Empty,
            task.Description);
    }

    record AgentInfo(string AgentId, string PersonaId);
}
