using AISwarm.Infrastructure.Models.A2A;

namespace AISwarm.Infrastructure.Services;

/// <summary>
/// Service for launching and managing A2A (Agent-to-Agent) protocol agents.
/// </summary>
public interface IA2AService
{
    /// <summary>
    /// Launch a new A2A agent with the specified configuration.
    /// </summary>
    /// <param name="config">Agent launch configuration</param>
    /// <returns>Information about the launched agent instance</returns>
    Task<A2AAgentInstance> LaunchAgentAsync(A2AAgentConfig config);

    /// <summary>
    /// Get the current status of a running A2A agent.
    /// </summary>
    /// <param name="agentId">Unique identifier of the agent</param>
    /// <returns>Current agent status information</returns>
    Task<A2AAgentStatus> GetAgentStatusAsync(string agentId);

    /// <summary>
    /// Stop a running A2A agent gracefully.
    /// </summary>
    /// <param name="agentId">Unique identifier of the agent to stop</param>
    /// <returns>True if agent was stopped successfully, false otherwise</returns>
    Task<bool> StopAgentAsync(string agentId);

    /// <summary>
    /// Get all currently running A2A agent instances.
    /// </summary>
    /// <returns>Collection of running agent instances</returns>
    Task<IEnumerable<A2AAgentInstance>> GetRunningAgentsAsync();
}
