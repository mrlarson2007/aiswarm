using ModelContextProtocol.Server;
using System.ComponentModel;
using AISwarm.DataLayer;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.Server.McpTools;

/// <summary>
/// MCP tool implementation for agents to request their next task
/// </summary>
[McpServerToolType]
public class GetNextTaskMcpTool(IDatabaseScopeService scopeService)
{
    public GetNextTaskConfiguration Configuration
    {
        get;
        set;
    } = GetNextTaskConfiguration.Production;

    /// <summary>
    /// Gets the next pending task for the specified agent
    /// </summary>
    /// <param name="agentId">ID of the agent requesting a task</param>
    /// <returns>Result with task information or error message</returns>
    [McpServerTool(Name = "get_next_task")]
    [Description("Gets the next pending task for the specified agent")]
    public async Task<GetNextTaskResult> GetNextTaskAsync(
        [Description("ID of the agent requesting a task")] string agentId)
    {
        return await GetNextTaskAsync(agentId, Configuration);
    }

    /// <summary>
    /// Gets the next pending task for the specified agent with custom configuration
    /// </summary>
    /// <param name="agentId">ID of the agent requesting a task</param>
    /// <param name="configuration">Polling configuration for timeouts and intervals</param>
    /// <returns>Result with task information or error message</returns>
    public async Task<GetNextTaskResult> GetNextTaskAsync(
        string agentId,
        GetNextTaskConfiguration configuration)
    {
        // First validate that the agent exists
        using (var scope = scopeService.CreateReadScope())
        {
            var agent = await scope.Agents.FindAsync(agentId);
            if (agent == null)
            {
                return GetNextTaskResult
                    .Failure($"Agent not found: {agentId}");
            }
        }

        var startTime = DateTime.UtcNow;
        var endTime = startTime.Add(configuration.TimeToWaitForTask);

        // Poll for tasks until timeout
        while (DateTime.UtcNow < endTime)
        {
            // Create a fresh scope for each poll to see new tasks
            using (var scope = scopeService.CreateReadScope())
            {
                // Look for the next pending task for this agent
                var pendingTask = await scope.Tasks
                    .Where(t => t.AgentId == agentId)
                    .Where(t => t.Status == DataLayer.Entities.TaskStatus.Pending)
                    .OrderByDescending(t => t.Priority)
                    .ThenBy(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                // If task found, return it immediately
                if (pendingTask != null)
                {
                    return GetNextTaskResult.SuccessWithTask(
                        pendingTask.Id,
                        pendingTask.Persona,
                        pendingTask.Description);
                }

                // No assigned tasks found, look for unassigned tasks to claim
                var unassignedTask = await scope.Tasks
                    .Where(t => t.AgentId == null || t.AgentId == string.Empty)
                    .Where(t => t.Status == DataLayer.Entities.TaskStatus.Pending)
                    .OrderByDescending(t => t.Priority)
                    .ThenBy(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (unassignedTask != null)
                {
                    // Claim the unassigned task by setting the AgentId
                    // Need to use a write scope for this operation
                    return await ClaimUnassignedTaskAsync(
                        unassignedTask.Id, agentId);
                }
            }

            // Wait for the polling interval before checking again
            await Task.Delay(configuration.PollingInterval);
        }

        // Timeout reached, no tasks available
        return GetNextTaskResult.NoTasksAvailable();
    }

    /// <summary>
    /// Claims an unassigned task by setting the AgentId to the requesting agent
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
        {
            return GetNextTaskResult.NoTasksAvailable();
        }

        // Verify task is still unassigned and pending
        if (!string.IsNullOrEmpty(task.AgentId) ||
            task.Status != AISwarm.DataLayer.Entities.TaskStatus.Pending)
        {
            return GetNextTaskResult.NoTasksAvailable();
        }

        // Claim the task
        task.AgentId = agentId;

        await scope.SaveChangesAsync();
        scope.Complete();

        return GetNextTaskResult.SuccessWithTask(
            task.Id,
            task.Persona,
            task.Description);
    }
}
