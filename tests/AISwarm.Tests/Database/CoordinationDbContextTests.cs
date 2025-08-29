using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AISwarm.Tests.Database;

public class CoordinationDbContextTests : IDisposable
{
    private readonly CoordinationDbContext _context;

    public CoordinationDbContextTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new CoordinationDbContext(options);
    }

    [Fact]
    public async Task WhenSavingAgent_ShouldPersistToDatabase()
    {
        // Arrange
        var agent = new Agent
        {
            Id = "agent-123",
            PersonaId = "planner",
            WorkingDirectory = "/test/path",
            Status = AgentStatus.Running,
            StartedAt = DateTime.UtcNow,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow
        };

        // Act
        _context.Agents.Add(agent);
        await _context.SaveChangesAsync();

        // Assert
        var savedAgent = await _context.Agents.FindAsync("agent-123");
        savedAgent.ShouldNotBeNull();
        savedAgent.PersonaId.ShouldBe("planner");
        savedAgent.Status.ShouldBe(AgentStatus.Running);
    }

    [Fact]
    public async Task WhenQueryingAgents_ShouldFilterByStatus()
    {
        // Arrange
        var runningAgent = new Agent { Id = "agent-1", Status = AgentStatus.Running, PersonaId = "planner" };
        var stoppedAgent = new Agent { Id = "agent-2", Status = AgentStatus.Stopped, PersonaId = "implementer" };

        _context.Agents.AddRange(runningAgent, stoppedAgent);
        await _context.SaveChangesAsync();

        // Act
        var runningAgents = await _context.Agents
            .Where(a => a.Status == AgentStatus.Running)
            .ToListAsync();

        // Assert
        runningAgents.Count.ShouldBe(1);
        runningAgents[0].Id.ShouldBe("agent-1");
    }

    [Fact]
    public async Task WhenUpdatingHeartbeat_ShouldPersistChanges()
    {
        // Arrange
        var agent = new Agent
        {
            Id = "agent-123",
            PersonaId = "planner",
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-5)
        };

        _context.Agents.Add(agent);
        await _context.SaveChangesAsync();

        // Act
        var newHeartbeatTime = DateTime.UtcNow;
        agent.UpdateHeartbeat(newHeartbeatTime);
        await _context.SaveChangesAsync();

        // Assert
        var updatedAgent = await _context.Agents.FindAsync("agent-123");
        updatedAgent!.LastHeartbeat.ShouldBe(newHeartbeatTime);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
