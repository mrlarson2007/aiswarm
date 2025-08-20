using AISwarm.DataLayer.Models;

namespace AISwarm.DataLayer.Contracts;

/// <summary>
/// Service interface for agent registration and management
/// </summary>
public interface IAgentService
{
    Task<string> RegisterAgentAsync(RegisterAgentRequest request);
    Task<AgentInfo?> GetAgentAsync(string agentId);
    Task<bool> UpdateHeartbeatAsync(string agentId);
}

/// <summary>
/// Request to register a new agent
/// </summary>
public record RegisterAgentRequest
{
    public string PersonaId { get; init; } = string.Empty;
    public string? AssignedWorktree { get; init; }
}