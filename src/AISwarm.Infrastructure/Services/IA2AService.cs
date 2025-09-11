using AISwarm.Infrastructure.Models.A2A;

namespace AISwarm.Infrastructure;

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

}
