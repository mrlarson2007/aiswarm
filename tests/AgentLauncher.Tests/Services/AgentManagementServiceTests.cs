using AgentLauncher.Services;
using Shouldly;

namespace AgentLauncher.Tests.Services;

public class AgentManagementServiceTests
{
    private readonly AgentManagementService _systemUnderTest;

    public AgentManagementServiceTests()
    {
        _systemUnderTest = new AgentManagementService();
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
}