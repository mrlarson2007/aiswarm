using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Entities;

namespace AISwarm.Server.McpTools;

/// <summary>
/// MCP tool implementation for creating tasks and assigning them to agents
/// </summary>
public class CreateTaskMcpTool : ICreateTaskMcpTool
{
    private readonly IDatabaseScopeService _scopeService;
    private readonly ITimeService _timeService;

    public CreateTaskMcpTool(IDatabaseScopeService scopeService, ITimeService timeService)
    {
        _scopeService = scopeService;
        _timeService = timeService;
    }

    /// <summary>
    /// Creates a new task and assigns it to the specified agent
    /// </summary>
    /// <param name="agentId">ID of the agent to assign the task to</param>
    /// <param name="persona">Full persona markdown content for the agent</param>
    /// <param name="description">Description of what the agent should accomplish</param>
    /// <returns>Result indicating success with task ID or failure with error message</returns>
    public async Task<CreateTaskResult> CreateTaskAsync(string agentId, string persona, string description)
    {
        using var scope = _scopeService.CreateWriteScope();
        
        // Validate that the agent exists
        var agent = await scope.Agents.FindAsync(agentId);
        if (agent == null)
        {
            return CreateTaskResult.Failure($"Agent not found: {agentId}");
        }
        
        // Validate that the agent is running
        if (agent.Status != AgentStatus.Running)
        {
            return CreateTaskResult.Failure($"Agent is not running: {agentId}. Current status: {agent.Status}");
        }
        
        var taskId = Guid.NewGuid().ToString();
        var workItem = new WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = AISwarm.DataLayer.Entities.TaskStatus.Pending,
            Persona = persona,
            Description = description,
            CreatedAt = _timeService.UtcNow
        };
        
        scope.Tasks.Add(workItem);
        await scope.SaveChangesAsync();
        scope.Complete();
        
        return CreateTaskResult.SuccessWithTaskId(taskId);
    }
}