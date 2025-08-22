using AISwarm.DataLayer.Contracts;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AISwarm.Server.McpTools;

/// <summary>
/// MCP tool implementation for agents to request their next task
/// </summary>
[McpServerToolType]
public class GetNextTaskMcpTool
{
    private readonly IDatabaseScopeService _scopeService;

    public GetNextTaskMcpTool(
        IDatabaseScopeService scopeService)
    {
        _scopeService = scopeService;
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
        using var scope = _scopeService.CreateReadScope();

        // Validate that the agent exists
        var agent = await scope.Agents.FindAsync(agentId);
        if (agent == null)
        {
            return GetNextTaskResult
                .Failure($"Agent not found: {agentId}");
        }

        // For now, if agent exists, return no tasks available (to make the test pass)
        // We'll implement actual task retrieval in the next test
        return GetNextTaskResult.NoTasksAvailable();
    }
}