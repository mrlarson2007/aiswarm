using Shouldly;
using Xunit;
using AISwarm.Shared.Contracts;
using AISwarm.Server.Services;
using AISwarm.Server.Data;
using Microsoft.EntityFrameworkCore;
using AISwarm.TestDoubles;

namespace AISwarm.Server.Tests.Services;

/// <summary>
/// Tests for agent registration and management service
/// </summary>
public class AgentServiceTests : IDisposable
{
    private readonly CoordinationDbContext _dbContext;
    private readonly FakeTimeService _timeService;
    private readonly IAgentService _systemUnderTest;

    public AgentServiceTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new CoordinationDbContext(options);
        _timeService = new FakeTimeService();
        _systemUnderTest = new AgentService(_dbContext, _timeService);
    }

    [Fact]
    public async Task WhenRegisteringNewAgent_ShouldReturnAgentId()
    {
        var agentRequest = new RegisterAgentRequest
        {
            PersonaId = "planner",
            AssignedWorktree = null
        };

        var result = await _systemUnderTest.RegisterAgentAsync(agentRequest);

        result.ShouldNotBeNullOrEmpty();
        result.ShouldStartWith("agent-");
    }

    [Fact]
    public async Task WhenRegisteringAgent_ShouldPersistToDatabase()
    {
        var agentRequest = new RegisterAgentRequest
        {
            PersonaId = "implementer",
            AssignedWorktree = "user-auth-feature"
        };

        var agentId = await _systemUnderTest.RegisterAgentAsync(agentRequest);

        // Verify agent is persisted directly in database
        var persistedAgent = await _dbContext.Agents.FindAsync(agentId);
        persistedAgent.ShouldNotBeNull();
        persistedAgent.Id.ShouldBe(agentId);
        persistedAgent.PersonaId.ShouldBe("implementer");
        persistedAgent.AssignedWorktree.ShouldBe("user-auth-feature");
        persistedAgent.Status.ShouldBe("active");
    }

    [Fact]
    public async Task WhenUpdatingHeartbeat_ShouldUpdateLastHeartbeatTimestamp()
    {
        var agentRequest = new RegisterAgentRequest
        {
            PersonaId = "reviewer",
            AssignedWorktree = null
        };
        
        var agentId = await _systemUnderTest.RegisterAgentAsync(agentRequest);
        var originalAgent = await _dbContext.Agents.FindAsync(agentId);
        var originalHeartbeat = originalAgent!.LastHeartbeat;
        
        // Advance time to simulate heartbeat interval
        _timeService.AdvanceTime(TimeSpan.FromMinutes(1));

        var updateResult = await _systemUnderTest.UpdateHeartbeatAsync(agentId);

        updateResult.ShouldBeTrue();
        
        // Verify heartbeat updated directly in database
        var updatedAgent = await _dbContext.Agents.FindAsync(agentId);
        updatedAgent.ShouldNotBeNull();
        updatedAgent.LastHeartbeat.ShouldBeGreaterThan(originalHeartbeat);
    }

    [Fact]
    public async Task WhenUpdatingHeartbeatForNonExistentAgent_ShouldReturnFalse()
    {
        var nonExistentAgentId = "agent-nonexistent123";

        var result = await _systemUnderTest.UpdateHeartbeatAsync(nonExistentAgentId);

        result.ShouldBeFalse();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}