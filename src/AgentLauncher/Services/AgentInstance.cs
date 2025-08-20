namespace AgentLauncher.Services;

public class AgentInstance
{
    public required string Id { get; init; }
    public required string AgentType { get; init; }
    public required string WorkingDirectory { get; init; }
    public AgentStatus Status { get; set; }
    public DateTime StartedAt { get; init; }
    public DateTime? StoppedAt { get; set; }
    public string? ProcessId { get; set; }
    public string? Model { get; init; }
    public string? WorktreeName { get; init; }
}

public enum AgentStatus
{
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed
}