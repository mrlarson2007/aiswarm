namespace AgentLauncher.Services;

public class AgentHealthConfiguration
{
    public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
    public bool AutoRestartOnFailure { get; set; } = true;
    public int MaxRestartAttempts { get; set; } = 3;
    public TimeSpan RestartCooldown { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan GracefulShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public class AgentHealthStatus
{
    public bool IsHealthy { get; set; }
    public string Reason { get; set; } = string.Empty;
    public TimeSpan TimeSinceLastHeartbeat { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public DateTime CheckTime { get; set; }
}