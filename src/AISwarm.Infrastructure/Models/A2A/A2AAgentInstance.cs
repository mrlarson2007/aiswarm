namespace AISwarm.Infrastructure.Models.A2A;

/// <summary>
/// Information about a running A2A agent instance.
/// </summary>
public class A2AAgentInstance
{
    /// <summary>
    /// Unique identifier for this agent instance.
    /// </summary>
    public required string AgentId { get; set; }

    /// <summary>
    /// Name of the agent (from launch configuration).
    /// </summary>
    public required string AgentName { get; set; }

    /// <summary>
    /// Base URL for A2A protocol endpoints (e.g., "http://localhost:3001").
    /// </summary>
    public required string AgentUrl { get; set; }

    /// <summary>
    /// Operating system process ID.
    /// </summary>
    public required int ProcessId { get; set; }

    /// <summary>
    /// Current status of the agent.
    /// </summary>
    public required A2AAgentStatus Status { get; set; }

    /// <summary>
    /// Capabilities this agent supports.
    /// </summary>
    public required string[] Capabilities { get; set; }

    /// <summary>
    /// Optional description of the agent.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When the agent was launched.
    /// </summary>
    public required DateTime LaunchedAt { get; set; }

    /// <summary>
    /// Last time the agent responded to a health check.
    /// </summary>
    public DateTime? LastHealthCheck { get; set; }

    /// <summary>
    /// Git worktree path if applicable.
    /// </summary>
    public string? WorktreePath { get; set; }
}
