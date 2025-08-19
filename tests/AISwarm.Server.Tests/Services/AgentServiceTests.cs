using Shouldly;
using Xunit;
using AISwarm.Shared.Contracts;
using AISwarm.Server.Services;
using AISwarm.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.Server.Tests.Services;

/// <summary>
/// Tests for agent registration and management service
/// Following TDD principles: RED-GREEN-REFACTOR-COMMIT
/// </summary>
public class AgentServiceTests : IDisposable
{
    private readonly CoordinationDbContext _dbContext;
    private readonly IAgentService _systemUnderTest;

    public AgentServiceTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new CoordinationDbContext(options);
        _systemUnderTest = new AgentService(_dbContext);
    }

    [Fact]
    public async Task WhenRegisteringNewAgent_ShouldReturnAgentId()
    {
        // Arrange
        var agentRequest = new RegisterAgentRequest
        {
            PersonaId = "planner",
            AssignedWorktree = null
        };

        // Act
        var result = await _systemUnderTest.RegisterAgentAsync(agentRequest);

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldStartWith("agent-");
    }

    [Fact]
    public async Task WhenRegisteringAgent_ShouldPersistToDatabase()
    {
        // Arrange
        var agentRequest = new RegisterAgentRequest
        {
            PersonaId = "implementer",
            AssignedWorktree = "user-auth-feature"
        };

        // Act
        var agentId = await _systemUnderTest.RegisterAgentAsync(agentRequest);

        // Assert - Agent should be retrievable from database
        var retrievedAgent = await _systemUnderTest.GetAgentAsync(agentId);
        retrievedAgent.ShouldNotBeNull();
        retrievedAgent.Id.ShouldBe(agentId);
        retrievedAgent.PersonaId.ShouldBe("implementer");
        retrievedAgent.AssignedWorktree.ShouldBe("user-auth-feature");
        retrievedAgent.Status.ShouldBe("active");
    }

    [Fact]
    public async Task WhenUpdatingHeartbeat_ShouldUpdateLastHeartbeatTimestamp()
    {
        // Arrange
        var agentRequest = new RegisterAgentRequest
        {
            PersonaId = "reviewer",
            AssignedWorktree = null
        };
        
        var agentId = await _systemUnderTest.RegisterAgentAsync(agentRequest);
        var originalAgent = await _systemUnderTest.GetAgentAsync(agentId);
        var originalHeartbeat = originalAgent!.LastHeartbeat;
        
        // Wait a bit to ensure timestamp difference
        await Task.Delay(10);

        // Act
        var updateResult = await _systemUnderTest.UpdateHeartbeatAsync(agentId);

        // Assert
        updateResult.ShouldBeTrue();
        
        var updatedAgent = await _systemUnderTest.GetAgentAsync(agentId);
        updatedAgent.ShouldNotBeNull();
        updatedAgent.LastHeartbeat.ShouldBeGreaterThan(originalHeartbeat);
    }

    [Fact]
    public async Task WhenUpdatingHeartbeatForNonExistentAgent_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentAgentId = "agent-nonexistent123";

        // Act
        var result = await _systemUnderTest.UpdateHeartbeatAsync(nonExistentAgentId);

        // Assert
        result.ShouldBeFalse();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}