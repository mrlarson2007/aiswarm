namespace AgentLauncher.Services;

/// <summary>
///     Configuration for agent monitoring timeouts and intervals
/// </summary>
public class AgentMonitoringConfiguration
{
    /// <summary>
    ///     How long to wait before considering an agent unresponsive (minutes)
    /// </summary>
    public int HeartbeatTimeoutMinutes
    {
        get;
        set;
    } = 5;

    /// <summary>
    ///     How often to check for unresponsive agents (minutes)
    /// </summary>
    public int CheckIntervalMinutes
    {
        get;
        set;
    } = 1;
}
