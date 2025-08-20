using AgentLauncher.Services;
using AISwarm.DataLayer.Contracts;
using Shouldly;

namespace AgentLauncher.Tests.Services;

public class AgentManagementServiceTests
{
    private readonly AgentManagementService _systemUnderTest;

    public AgentManagementServiceTests()
    {
        var configuration = new AgentHealthConfiguration();
        var timeService = new SystemTimeService();
        _systemUnderTest = new AgentManagementService(configuration, timeService);
    }

    [Fact]
    public async Task WhenStartingAgent_ShouldReturnAgentInstanceWithUniqueId()
    {
        // Arrange
        var agentType = "planner";
        var workingDirectory = "/test/path";

        // Act
        var result = await _systemUnderTest.StartAgentAsync(agentType, workingDirectory);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldNotBeNullOrEmpty();
        result.AgentType.ShouldBe(agentType);
        result.WorkingDirectory.ShouldBe(workingDirectory);
        result.Status.ShouldBe(AgentStatus.Running);
        result.StartedAt.ShouldNotBe(default);
    }

    [Fact]
    public async Task WhenStoppingAgent_ShouldChangeStatusToStopped()
    {
        // Arrange
        var agent = await _systemUnderTest.StartAgentAsync("implementer", "/test/path");

        // Act
        var stopped = await _systemUnderTest.StopAgentAsync(agent.Id);

        // Assert
        stopped.ShouldBeTrue();
        var agentStatus = await _systemUnderTest.GetAgentStatusAsync(agent.Id);
        agentStatus.ShouldNotBeNull();
        agentStatus.Status.ShouldBe(AgentStatus.Stopped);
    }

    [Fact]
    public async Task WhenGettingAllAgents_ShouldReturnAllManagedAgents()
    {
        // Arrange
        var agent1 = await _systemUnderTest.StartAgentAsync("planner", "/path1");
        var agent2 = await _systemUnderTest.StartAgentAsync("implementer", "/path2");

        // Act
        var allAgents = await _systemUnderTest.GetAllAgentsAsync();

        // Assert
        allAgents.ShouldNotBeNull();
        allAgents.Count().ShouldBe(2);
        allAgents.ShouldContain(a => a.Id == agent1.Id);
        allAgents.ShouldContain(a => a.Id == agent2.Id);
    }

    [Fact]
    public async Task WhenGettingNonExistentAgent_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = "invalid-id";

        // Act
        var result = await _systemUnderTest.GetAgentStatusAsync(nonExistentId);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task WhenCheckingAgentHealth_ShouldUseConfiguredHeartbeatTimeout()
    {
        // Arrange
        var config = new AgentHealthConfiguration
        {
            HeartbeatTimeout = TimeSpan.FromMinutes(2)
        };
        var serviceWithConfig = new AgentManagementService(config);
        
        var agent = await serviceWithConfig.StartAgentAsync("planner", "/test/path");
        
        // Simulate agent not sending heartbeat for longer than timeout
        var healthStatus = await serviceWithConfig.CheckAgentHealthAsync(agent.Id, 
            lastHeartbeat: DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(3)));

        // Assert
        healthStatus.IsHealthy.ShouldBeFalse();
        healthStatus.Reason.ShouldContain("heartbeat timeout");
    }

    [Fact]
    public async Task WhenAgentExceedsHeartbeatTimeout_ShouldMarkAsUnhealthy()
    {
        // Arrange
        var config = new AgentHealthConfiguration { HeartbeatTimeout = TimeSpan.FromMinutes(1) };
        var serviceWithConfig = new AgentManagementService(config);
        
        var agent = await serviceWithConfig.StartAgentAsync("implementer", "/test/path");
        
        // Simulate stale heartbeat
        var staleHeartbeat = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(2));
        
        // Act
        var healthCheck = await serviceWithConfig.CheckAgentHealthAsync(agent.Id, staleHeartbeat);
        
        // Assert
        healthCheck.IsHealthy.ShouldBeFalse();
        healthCheck.TimeSinceLastHeartbeat.ShouldBeGreaterThan(config.HeartbeatTimeout);
    }
}