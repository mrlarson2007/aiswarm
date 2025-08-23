using AgentLauncher.Services;
using AISwarm.Shared.Contracts;
using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Database;
using AISwarm.DataLayer.Entities;
using AISwarm.DataLayer.Services;
using AISwarm.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Moq;
using AgentStatus = AISwarm.DataLayer.Entities.AgentStatus;

namespace AgentLauncher.Tests.Services;

public class LocalAgentServiceTests : IDisposable
{
    private readonly LocalAgentService _systemUnderTest;
    private readonly FakeTimeService _timeService;
    private readonly CoordinationDbContext _dbContext;
    private readonly IDatabaseScopeService _scopeService;

    public LocalAgentServiceTests()
    {
        _timeService = new FakeTimeService();
        
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new CoordinationDbContext(options);
        _scopeService = new DatabaseScopeService(_dbContext);
        
        _systemUnderTest = new LocalAgentService(_timeService, _scopeService);
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

        // Assert - Check database directly instead of using service API
        agentId.ShouldNotBeNullOrEmpty();
        
        var agentInDb = await _dbContext.Agents.FindAsync(agentId);
        agentInDb.ShouldNotBeNull();
        agentInDb.Id.ShouldBe(agentId);
        agentInDb.PersonaId.ShouldBe("planner");
        agentInDb.AgentType.ShouldBe("planner");
        agentInDb.WorkingDirectory.ShouldBe("/test/path");
        agentInDb.Model.ShouldBe("gemini-1.5-pro");
        agentInDb.WorktreeName.ShouldBe("main");
        agentInDb.Status.ShouldBe(AgentStatus.Starting);
        agentInDb.RegisteredAt.ShouldBe(_timeService.UtcNow);
        agentInDb.LastHeartbeat.ShouldBe(_timeService.UtcNow);
        agentInDb.ProcessId.ShouldBeNull();
        agentInDb.StoppedAt.ShouldBeNull();
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

        // Assert - Check database directly for heartbeat update
        success.ShouldBeTrue();
        
        var agentInDb = await _dbContext.Agents.FindAsync(agentId);
        agentInDb.ShouldNotBeNull();
        agentInDb.LastHeartbeat.ShouldBe(_timeService.UtcNow);
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
    public async Task WhenKillingAgent_ShouldUpdateStatusAndKillTime()
    {
        // Arrange
        var request = new AgentRegistrationRequest
        {
            PersonaId = "implementer",
            AgentType = "implementer",
            WorkingDirectory = "/test/path"
        };
        var agentId = await _systemUnderTest.RegisterAgentAsync(request);
        await _systemUnderTest.MarkAgentRunningAsync(agentId, "54321");
        
        // Advance time
        _timeService.AdvanceTime(TimeSpan.FromMinutes(3));

        // Act
        await _systemUnderTest.KillAgentAsync(agentId);

        // Assert
        var agent = await _systemUnderTest.GetAgentAsync(agentId);
        agent!.Status.ShouldBe(AgentStatus.Killed);
        agent.StoppedAt.ShouldBe(_timeService.UtcNow);
    }

    [Fact]
    public async Task WhenKillingAgentWithProcessId_ShouldTerminateProcess()
    {
        // Arrange
        var mockProcessService = new Mock<IProcessTerminationService>();
        var serviceWithProcessKill = new LocalAgentService(_timeService, _scopeService, mockProcessService.Object);
        
        var request = new AgentRegistrationRequest
        {
            PersonaId = "tester",
            AgentType = "tester",
            WorkingDirectory = "/kill/test"
        };
        var agentId = await serviceWithProcessKill.RegisterAgentAsync(request);
        await serviceWithProcessKill.MarkAgentRunningAsync(agentId, "98765");

        // Act
        await serviceWithProcessKill.KillAgentAsync(agentId);

        // Assert
        mockProcessService.Verify(p => p.KillProcessAsync("98765"), Times.Once);
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

    [Fact]
    public async Task WhenRegisteringMultipleAgents_ShouldCreateUniqueIds()
    {
        // Arrange
        var request1 = new AgentRegistrationRequest { PersonaId = "planner", AgentType = "planner", WorkingDirectory = "/path1" };
        var request2 = new AgentRegistrationRequest { PersonaId = "implementer", AgentType = "implementer", WorkingDirectory = "/path2" };

        // Act
        var agentId1 = await _systemUnderTest.RegisterAgentAsync(request1);
        var agentId2 = await _systemUnderTest.RegisterAgentAsync(request2);

        // Assert - Check database directly for both agents
        agentId1.ShouldNotBe(agentId2);
        agentId1.ShouldNotBeNullOrEmpty();
        agentId2.ShouldNotBeNullOrEmpty();
        
        var agent1InDb = await _dbContext.Agents.FindAsync(agentId1);
        var agent2InDb = await _dbContext.Agents.FindAsync(agentId2);
        
        agent1InDb.ShouldNotBeNull();
        agent1InDb.PersonaId.ShouldBe("planner");
        agent1InDb.WorkingDirectory.ShouldBe("/path1");
        
        agent2InDb.ShouldNotBeNull();
        agent2InDb.PersonaId.ShouldBe("implementer");
        agent2InDb.WorkingDirectory.ShouldBe("/path2");
        
        // Verify database contains exactly 2 agents
        var totalAgents = await _dbContext.Agents.CountAsync();
        totalAgents.ShouldBe(2);
    }

    [Fact]
    public async Task WhenRegisteringAgentWithAllOptionalFields_ShouldSetAllProperties()
    {
        // Arrange
        var request = new AgentRegistrationRequest
        {
            PersonaId = "reviewer",
            AgentType = "reviewer",
            WorkingDirectory = "/test/review-path",
            Model = "gemini-1.5-flash",
            WorktreeName = "feature-branch"
        };

        // Act
        var agentId = await _systemUnderTest.RegisterAgentAsync(request);

        // Assert
        var agent = await _systemUnderTest.GetAgentAsync(agentId);
        agent.ShouldNotBeNull();
        agent.Id.ShouldBe(agentId);
        agent.PersonaId.ShouldBe("reviewer");
        agent.AgentType.ShouldBe("reviewer");
        agent.WorkingDirectory.ShouldBe("/test/review-path");
        agent.Model.ShouldBe("gemini-1.5-flash");
        agent.WorktreeName.ShouldBe("feature-branch");
        agent.Status.ShouldBe(AgentStatus.Starting);
        agent.RegisteredAt.ShouldBe(_timeService.UtcNow);
        agent.LastHeartbeat.ShouldBe(_timeService.UtcNow);
        agent.ProcessId.ShouldBeNull();
        agent.StoppedAt.ShouldBeNull();
    }

    [Fact]
    public async Task WhenRegisteringAgentWithMinimalFields_ShouldSetRequiredPropertiesOnly()
    {
        // Arrange
        var request = new AgentRegistrationRequest
        {
            PersonaId = "tester",
            AgentType = "tester",
            WorkingDirectory = "/test/minimal-path"
            // Model and WorktreeName are null
        };

        // Act
        var agentId = await _systemUnderTest.RegisterAgentAsync(request);

        // Assert
        var agent = await _systemUnderTest.GetAgentAsync(agentId);
        agent.ShouldNotBeNull();
        agent.PersonaId.ShouldBe("tester");
        agent.AgentType.ShouldBe("tester");
        agent.WorkingDirectory.ShouldBe("/test/minimal-path");
        agent.Model.ShouldBeNull();
        agent.WorktreeName.ShouldBeNull();
        agent.Status.ShouldBe(AgentStatus.Starting);
    }

    [Fact]
    public async Task WhenAgentCompletesFullLifecycle_ShouldTrackAllTimestamps()
    {
        // Arrange
        var request = new AgentRegistrationRequest
        {
            PersonaId = "implementer",
            AgentType = "implementer",
            WorkingDirectory = "/lifecycle/test"
        };
        
        var registrationTime = _timeService.UtcNow;

        // Act & Assert - Registration
        var agentId = await _systemUnderTest.RegisterAgentAsync(request);
        var agent = await _systemUnderTest.GetAgentAsync(agentId);
        agent!.RegisteredAt.ShouldBe(registrationTime);
        agent.LastHeartbeat.ShouldBe(registrationTime);
        agent.Status.ShouldBe(AgentStatus.Starting);

        // Act & Assert - Mark Running
        _timeService.AdvanceTime(TimeSpan.FromSeconds(5));
        var startTime = _timeService.UtcNow;
        await _systemUnderTest.MarkAgentRunningAsync(agentId, "pid-12345");
        
        agent = await _systemUnderTest.GetAgentAsync(agentId);
        agent!.Status.ShouldBe(AgentStatus.Running);
        agent.ProcessId.ShouldBe("pid-12345");
        agent.StartedAt.ShouldBe(startTime);

        // Act & Assert - Heartbeat Update
        _timeService.AdvanceTime(TimeSpan.FromMinutes(1));
        var heartbeatTime = _timeService.UtcNow;
        await _systemUnderTest.UpdateHeartbeatAsync(agentId);
        
        agent = await _systemUnderTest.GetAgentAsync(agentId);
        agent!.LastHeartbeat.ShouldBe(heartbeatTime);

        // Act & Assert - Stop Agent
        _timeService.AdvanceTime(TimeSpan.FromMinutes(10));
        var stopTime = _timeService.UtcNow;
        await _systemUnderTest.StopAgentAsync(agentId);
        
        agent = await _systemUnderTest.GetAgentAsync(agentId);
        agent!.Status.ShouldBe(AgentStatus.Stopped);
        agent.StoppedAt.ShouldBe(stopTime);
        
        // Verify all timestamps are different and in correct order
        agent.RegisteredAt.ShouldBeLessThan(agent.StartedAt);
        agent.StartedAt.ShouldBeLessThan(agent.LastHeartbeat);
        agent.LastHeartbeat.ShouldBeLessThan(agent.StoppedAt!.Value);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}