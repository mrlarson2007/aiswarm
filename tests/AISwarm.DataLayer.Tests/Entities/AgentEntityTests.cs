using AISwarm.DataLayer.Entities;
using Shouldly;

namespace AISwarm.DataLayer.Tests.Entities;

public class AgentEntityTests
{
    [Fact]
    public void WhenCreatingAgent_ShouldHaveAllRequiredProperties()
    {
        // Arrange & Act
        var agent = new Agent
        {
            Id = "agent-123",
            PersonaId = "planner",
            AgentType = "planner",
            WorkingDirectory = "/test/path",
            Status = AgentStatus.Running,
            StartedAt = DateTime.UtcNow,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow
        };

        // Assert
        agent.Id.ShouldBe("agent-123");
        agent.PersonaId.ShouldBe("planner");
        agent.AgentType.ShouldBe("planner");
        agent.WorkingDirectory.ShouldBe("/test/path");
        agent.Status.ShouldBe(AgentStatus.Running);
        agent.StartedAt.ShouldNotBe(default);
        agent.RegisteredAt.ShouldNotBe(default);
        agent.LastHeartbeat.ShouldNotBe(default);
    }

    [Fact]
    public void WhenUpdatingHeartbeat_ShouldUpdateLastHeartbeatTime()
    {
        // Arrange
        var agent = new Agent
        {
            Id = "agent-123",
            PersonaId = "planner",
            LastHeartbeat = DateTime.UtcNow.AddMinutes(-5)
        };
        var newHeartbeatTime = DateTime.UtcNow;

        // Act
        agent.UpdateHeartbeat(newHeartbeatTime);

        // Assert
        agent.LastHeartbeat.ShouldBe(newHeartbeatTime);
    }

    [Fact]
    public void WhenStoppingAgent_ShouldSetStatusAndStoppedTime()
    {
        // Arrange
        var agent = new Agent
        {
            Id = "agent-123",
            Status = AgentStatus.Running
        };
        var stopTime = DateTime.UtcNow;

        // Act
        agent.Stop(stopTime);

        // Assert
        agent.Status.ShouldBe(AgentStatus.Stopped);
        agent.StoppedAt.ShouldBe(stopTime);
    }

    [Fact]
    public void WhenKillingAgent_ShouldSetStatusAndKillTime()
    {
        // Arrange
        var agent = new Agent
        {
            Id = "agent-456",
            Status = AgentStatus.Running
        };
        var killTime = DateTime.UtcNow;

        // Act
        agent.Kill(killTime);

        // Assert
        agent.Status.ShouldBe(AgentStatus.Killed);
        agent.StoppedAt.ShouldBe(killTime);
    }
}