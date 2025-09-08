namespace AISwarm.Infrastructure.Models.A2A;

/// <summary>
/// Status of an A2A agent instance.
/// </summary>
public enum A2AAgentStatus
{
    /// <summary>
    /// Agent is starting up but not yet ready to receive requests.
    /// </summary>
    Starting,

    /// <summary>
    /// Agent is running and ready to handle A2A requests.
    /// </summary>
    Running,

    /// <summary>
    /// Agent is in the process of shutting down.
    /// </summary>
    Stopping,

    /// <summary>
    /// Agent has stopped gracefully.
    /// </summary>
    Stopped,

    /// <summary>
    /// Agent has encountered an error and is not responding.
    /// </summary>
    Error,

    /// <summary>
    /// Agent status is unknown (e.g., health check failed).
    /// </summary>
    Unknown
}
