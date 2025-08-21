namespace AISwarm.Server.McpTools;

/// <summary>
/// MCP tool for creating tasks and assigning them to agents
/// </summary>
public interface ICreateTaskMcpTool
{
    /// <summary>
    /// Creates a new task and assigns it to the specified agent
    /// </summary>
    /// <param name="agentId">ID of the agent to assign the task to</param>
    /// <param name="persona">Full persona markdown content for the agent</param>
    /// <param name="description">Description of what the agent should accomplish</param>
    /// <returns>Result indicating success with task ID or failure with error message</returns>
    Task<CreateTaskResult> CreateTaskAsync(string agentId, string persona, string description);
}