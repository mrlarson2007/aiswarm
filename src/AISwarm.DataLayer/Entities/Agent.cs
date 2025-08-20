namespace AISwarm.DataLayer.Entities;

/// <summary>
/// Unified agent entity combining local launcher tracking and coordination server needs
/// </summary>
public class Agent
{
    public string Id { get; set; } = string.Empty;
    public string PersonaId { get; set; } = string.Empty;
    public string AgentType { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public AgentStatus Status { get; set; } = AgentStatus.Starting;
    public DateTime StartedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public string? ProcessId { get; set; }
    public string? Model { get; set; }
    public string? WorktreeName { get; set; }
    public string? AssignedWorktree { get; set; }

    /// <summary>
    /// Update the agent's last heartbeat time
    /// </summary>
    public void UpdateHeartbeat(DateTime heartbeatTime)
    {
        LastHeartbeat = heartbeatTime;
    }

    /// <summary>
    /// Mark the agent as stopped
    /// </summary>
    public void Stop(DateTime stopTime)
    {
        Status = AgentStatus.Stopped;
        StoppedAt = stopTime;
    }
}

/// <summary>
/// Agent status enumeration supporting both launcher and coordination needs
/// </summary>
public enum AgentStatus
{
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed,
    Unhealthy
}