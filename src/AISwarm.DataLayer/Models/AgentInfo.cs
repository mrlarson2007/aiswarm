namespace AISwarm.DataLayer.Models;

/// <summary>
/// Information about a registered agent
/// </summary>
public record AgentInfo
{
    public string Id { get; init; } = string.Empty;
    public string PersonaId { get; init; } = string.Empty;
    public string? AssignedWorktree { get; init; }
    public string Status { get; init; } = "active";
    public DateTime RegisteredAt { get; init; }
    public DateTime LastHeartbeat { get; init; }
}