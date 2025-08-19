using Shouldly;
using Xunit;
using AISwarm.Shared.Contracts;
using AISwarm.Server.Services;

namespace AISwarm.Server.Tests.Services;

/// <summary>
/// Tests for agent registration and management service
/// Following TDD principles: RED-GREEN-REFACTOR-COMMIT
/// </summary>
public class AgentServiceTests
{
    private IAgentService SystemUnderTest { get; } = new AgentService();

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
        var result = await SystemUnderTest.RegisterAgentAsync(agentRequest);

        // Assert
        result.ShouldNotBeNullOrEmpty();
        result.ShouldStartWith("agent-");
    }
}