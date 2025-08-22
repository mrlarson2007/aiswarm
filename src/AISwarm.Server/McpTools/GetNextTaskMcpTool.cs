using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Entities;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.Server.McpTools;

/// <summary>
/// MCP tool implementation for agents to request their next task
/// </summary>
[McpServerToolType]
public class GetNextTaskMcpTool
{
    private readonly IDatabaseScopeService _scopeService;
    private readonly GetNextTaskConfiguration _defaultConfiguration;

    public GetNextTaskMcpTool(
        IDatabaseScopeService scopeService)
    {
        _scopeService = scopeService;
        _defaultConfiguration = new GetNextTaskConfiguration();
    }

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
        return await GetNextTaskAsync(agentId, _defaultConfiguration);
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
        using var scope = _scopeService.CreateReadScope();

        // Validate that the agent exists
        var agent = await scope.Agents.FindAsync(agentId);
        if (agent == null)
        {
            return GetNextTaskResult
                .Failure($"Agent not found: {agentId}");
        }

        var startTime = DateTime.UtcNow;
        var endTime = startTime.Add(configuration.TimeToWaitForTask);

        // Poll for tasks until timeout
        while (DateTime.UtcNow < endTime)
        {
            // Look for the next pending task for this agent
            var pendingTask = await scope.Tasks
                .Where(t => t.AgentId == agentId && t.Status == AISwarm.DataLayer.Entities.TaskStatus.Pending)
                .OrderBy(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            // If task found, return it immediately
            if (pendingTask != null)
            {
                return GetNextTaskResult.SuccessWithTask(
                    pendingTask.Id,
                    pendingTask.Persona,
                    pendingTask.Description);
            }

            // Wait for the polling interval before checking again
            await Task.Delay(configuration.PollingInterval);
        }

        // Timeout reached, no tasks available
        return GetNextTaskResult.NoTasksAvailable();
    }
}