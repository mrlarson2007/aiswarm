using AISwarm.Shared.Contracts;
using AISwarm.Shared.Models;
using AISwarm.Server.Data;
using AISwarm.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.Server.Services;

/// <summary>
/// Agent registration and management service with database persistence
/// </summary>
public class AgentService : IAgentService
{
    private readonly CoordinationDbContext _dbContext;

    public AgentService(CoordinationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> RegisterAgentAsync(RegisterAgentRequest request)
    {
        // Generate unique agent ID
        var agentId = $"agent-{Guid.NewGuid():N}";
        
        // Create agent entity
        var agent = new Agent
        {
            Id = agentId,
            PersonaId = request.PersonaId,
            AssignedWorktree = request.AssignedWorktree,
            Status = "active",
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow
        };

        // Persist to database
        _dbContext.Agents.Add(agent);
        await _dbContext.SaveChangesAsync();

        return agentId;
    }

    public async Task<AgentInfo?> GetAgentAsync(string agentId)
    {
        var agent = await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.Id == agentId);

        if (agent == null)
            return null;

        return new AgentInfo
        {
            Id = agent.Id,
            PersonaId = agent.PersonaId,
            AssignedWorktree = agent.AssignedWorktree,
            Status = agent.Status,
            RegisteredAt = agent.RegisteredAt,
            LastHeartbeat = agent.LastHeartbeat
        };
    }

    public async Task<bool> UpdateHeartbeatAsync(string agentId)
    {
        // GREEN phase: Implement actual heartbeat update
        var agent = await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.Id == agentId);

        if (agent == null)
            return false;

        // Update the heartbeat timestamp
        agent.LastHeartbeat = DateTime.UtcNow;
        
        // Save changes to database
        await _dbContext.SaveChangesAsync();
        
        return true;
    }
}