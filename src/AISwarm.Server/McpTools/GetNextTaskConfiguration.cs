namespace AISwarm.Server.McpTools;

/// <summary>
/// Configuration for GetNextTask polling behavior
/// </summary>
public class GetNextTaskConfiguration
{
    /// <summary>
    /// Maximum time to wait for a task before giving up (default: 100ms for testing)
    /// </summary>
    public TimeSpan TimeToWaitForTask { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Interval between polling attempts (default: 50ms for testing)
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Production configuration with longer timeouts suitable for real agent use
    /// </summary>
    public static GetNextTaskConfiguration Production => new()
    {
        TimeToWaitForTask = TimeSpan.FromMinutes(5),
        PollingInterval = TimeSpan.FromSeconds(1)
    };
}
