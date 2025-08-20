using AgentLauncher.Services;
using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Entities;
using Shouldly;
using AgentStatus = AISwarm.DataLayer.Entities.AgentStatus;

namespace AgentLauncher.Tests.Services;

public class LocalAgentServiceTests
{
    private readonly LocalAgentService _systemUnderTest;
    private readonly TestTimeService _timeService;

    public LocalAgentServiceTests()
    {
        _timeService = new TestTimeService();
        _systemUnderTest = new LocalAgentService(_timeService);
    }

    [Fact]
    public async Task WhenRegisteringAgent_ShouldCreateAgentWithUniqueId()
    {
        // Arrange
        var request = new AgentRegistrationRequest
        {
            PersonaId = "planner",
            AgentType = "planner", 
            WorkingDirectory = "/test/path",
            Model = "gemini-1.5-pro",
            WorktreeName = "main"
        };

        // Act
        var agentId = await _systemUnderTest.RegisterAgentAsync(request);

        // Assert
        agentId.ShouldNotBeNullOrEmpty();
        
        var agent = await _systemUnderTest.GetAgentAsync(agentId);
        agent.ShouldNotBeNull();
        agent.PersonaId.ShouldBe("planner");
        agent.AgentType.ShouldBe("planner");
        agent.WorkingDirectory.ShouldBe("/test/path");
        agent.Status.ShouldBe(AgentStatus.Starting);
        agent.RegisteredAt.ShouldBe(_timeService.UtcNow);
        agent.LastHeartbeat.ShouldBe(_timeService.UtcNow);
    }

    [Fact]
    public async Task WhenUpdatingHeartbeat_ShouldUpdateLastHeartbeatTime()
    {
        // Arrange
        var request = new AgentRegistrationRequest
        {
            PersonaId = "implementer",
            AgentType = "implementer",
            WorkingDirectory = "/test/path"
        };
        var agentId = await _systemUnderTest.RegisterAgentAsync(request);
        
        // Advance time
        _timeService.AdvanceTime(TimeSpan.FromMinutes(2));

        // Act
        var success = await _systemUnderTest.UpdateHeartbeatAsync(agentId);

        // Assert
        success.ShouldBeTrue();
        
        var agent = await _systemUnderTest.GetAgentAsync(agentId);
        agent!.LastHeartbeat.ShouldBe(_timeService.UtcNow);
    }

    [Fact]
    public async Task WhenMarkingAgentRunning_ShouldUpdateStatusAndStartTime()
    {
        // Arrange
        var request = new AgentRegistrationRequest
        {
            PersonaId = "tester",
            AgentType = "tester",
            WorkingDirectory = "/test/path"
        };
        var agentId = await _systemUnderTest.RegisterAgentAsync(request);
        var processId = "12345";
        
        // Advance time slightly
        _timeService.AdvanceTime(TimeSpan.FromSeconds(1));

        // Act
        await _systemUnderTest.MarkAgentRunningAsync(agentId, processId);

        // Assert
        var agent = await _systemUnderTest.GetAgentAsync(agentId);
        agent!.Status.ShouldBe(AgentStatus.Running);
        agent.ProcessId.ShouldBe(processId);
        agent.StartedAt.ShouldBe(_timeService.UtcNow);
    }

    [Fact]
    public async Task WhenStoppingAgent_ShouldUpdateStatusAndStopTime()
    {
        // Arrange
        var request = new AgentRegistrationRequest
        {
            PersonaId = "reviewer",
            AgentType = "reviewer",
            WorkingDirectory = "/test/path"
        };
        var agentId = await _systemUnderTest.RegisterAgentAsync(request);
        await _systemUnderTest.MarkAgentRunningAsync(agentId, "12345");
        
        // Advance time
        _timeService.AdvanceTime(TimeSpan.FromMinutes(5));

        // Act
        await _systemUnderTest.StopAgentAsync(agentId);

        // Assert
        var agent = await _systemUnderTest.GetAgentAsync(agentId);
        agent!.Status.ShouldBe(AgentStatus.Stopped);
        agent.StoppedAt.ShouldBe(_timeService.UtcNow);
    }

    [Fact]
    public async Task WhenCheckingHealthOfUnknownAgent_ShouldReturnNull()
    {
        // Arrange
        var unknownAgentId = "non-existent-agent";

        // Act
        var agent = await _systemUnderTest.GetAgentAsync(unknownAgentId);

        // Assert
        agent.ShouldBeNull();
    }
}

/// <summary>
/// Test double for ITimeService that allows time manipulation
/// </summary>
public class TestTimeService : ITimeService
{
    private DateTime _currentTime = new DateTime(2025, 8, 20, 10, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow => _currentTime;

    public void AdvanceTime(TimeSpan timeSpan)
    {
        _currentTime = _currentTime.Add(timeSpan);
    }
}