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
    private readonly ITimeService _timeService;

    public AgentService(CoordinationDbContext dbContext, ITimeService timeService)
    {
        _dbContext = dbContext;
        _timeService = timeService;
    }

    public async Task<string> RegisterAgentAsync(RegisterAgentRequest request)
    {
        var agentId = $"agent-{Guid.NewGuid():N}";
        
        var agent = new Agent
        {
            Id = agentId,
            PersonaId = request.PersonaId,
            AssignedWorktree = request.AssignedWorktree,
            Status = "active",
            RegisteredAt = _timeService.UtcNow,
            LastHeartbeat = _timeService.UtcNow
        };

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
        var agent = await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.Id == agentId);

        if (agent == null)
            return false;

        // Update timestamp to track agent liveness for coordination health monitoring
        agent.LastHeartbeat = _timeService.UtcNow;
        
        await _dbContext.SaveChangesAsync();
        
        return true;
    }
}