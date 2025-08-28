using System.ComponentModel;
using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Server.Entities;
using AISwarm.Infrastructure.Eventing;
using ModelContextProtocol.Server;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;

namespace AISwarm.Server.McpTools;

/// <summary>
///     MCP tool implementation for creating tasks and assigning them to agents
/// </summary>
[McpServerToolType]
public class CreateTaskMcpTool(
    IDatabaseScopeService scopeService,
    ITimeService timeService,
    IWorkItemNotificationService workItemNotifications)
{
    /// <summary>
    ///     Creates a new task and assigns it to the specified agent
    /// </summary>
    /// <param name="agentId">ID of the agent to assign the task to</param>
    /// <param name="persona">Full persona markdown content for the agent</param>
    /// <param name="description">Description of what the agent should accomplish</param>
    /// <param name="priority">Priority of the task (Low, Normal, High, Critical)</param>
    /// <returns>Result indicating success with task ID or failure with error message</returns>
    [McpServerTool(Name = "create_task")]
    [Description("Creates a new task and assigns it to the specified agent")]
    public async Task<CreateTaskResult> CreateTaskAsync(
        [Description("ID of the agent to assign the task to (optional - leave empty for unassigned task)")]
        string? agentId,
        [Description("Agent persona (implementer, reviewer, planner, etc.)")]
        string persona,
        [Description("Description of what the agent should accomplish")]
        string description,
        [Description("Priority of the task: Low, Normal, High, or Critical")]
        TaskPriority priority = TaskPriority.Normal)
    {
        using var scope = scopeService.CreateWriteScope();

        // Only validate agent if agentId is provided (for assigned tasks)
        if (!string.IsNullOrEmpty(agentId))
        {
            // Validate that the agent exists
            var agent = await scope.Agents.FindAsync(agentId);
            if (agent == null)
                return CreateTaskResult
                    .Failure($"Agent not found: {agentId}");

            // Validate that the agent is in a valid state to receive tasks
            if (agent.Status != AgentStatus.Running && agent.Status != AgentStatus.Starting)
                return CreateTaskResult
                    .Failure($"Agent is not in a valid state to receive tasks: {agentId}. " +
                             $"Current status: {agent.Status}. Agent must be Running or Starting.");
        }

        var taskId = Guid.NewGuid().ToString();
        var workItem = new WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = TaskStatus.Pending,
            PersonaId = persona,  // This is the routing tag
            Description = description,
            Priority = priority,
            CreatedAt = timeService.UtcNow
        };

        scope.Tasks.Add(workItem);
        await scope.SaveChangesAsync();
        scope.Complete();

        // Publish non-durable notification that a task was created
        await workItemNotifications.PublishTaskCreated(taskId, agentId, persona);

        return CreateTaskResult.SuccessWithTaskId(taskId);
    }
}
