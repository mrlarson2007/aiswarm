using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using AgentStatus = AISwarm.DataLayer.Entities.AgentStatus;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;

namespace AISwarm.Tests.Services;

public class LocalAgentServiceTests : IDisposable
{
    private readonly CoordinationDbContext _dbContext;
    private readonly IDatabaseScopeService _scopeService;
    private readonly LocalAgentService _systemUnderTest;
    private readonly FakeTimeService _timeService;

    public LocalAgentServiceTests()
    {
        _timeService = new FakeTimeService();

        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new CoordinationDbContext(options);
        _scopeService = new DatabaseScopeService(_dbContext);

        _systemUnderTest = new LocalAgentService(_timeService, _scopeService);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
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
    public async Task WhenUpdatingHeartbeatForStartingAgent_ShouldTransitionToRunning()
    {
        // Arrange
        var request = new AgentRegistrationRequest
        {
            PersonaId = "tester",
            AgentType = "tester",
            WorkingDirectory = "/test/path"
        };
        var agentId = await _systemUnderTest.RegisterAgentAsync(request);

        // Verify agent starts with Starting status
        var agent = await _systemUnderTest.GetAgentAsync(agentId);
        agent!.Status.ShouldBe(AgentStatus.Starting);

        // Advance time
        _timeService.AdvanceTime(TimeSpan.FromMinutes(1));

        // Act - Update heartbeat should transition Starting agent to Running
        var success = await _systemUnderTest.UpdateHeartbeatAsync(agentId);

        // Assert
        success.ShouldBeTrue();

        var updatedAgent = await _systemUnderTest.GetAgentAsync(agentId);
        updatedAgent!.Status.ShouldBe(AgentStatus.Running);
        updatedAgent.LastHeartbeat.ShouldBe(_timeService.UtcNow);
    }

    [Fact]
    public async Task WhenUpdatingHeartbeatForRunningAgent_ShouldStayRunning()
    {
        // Arrange
        var request = new AgentRegistrationRequest
        {
            PersonaId = "tester",
            AgentType = "tester",
            WorkingDirectory = "/test/path"
        };
        var agentId = await _systemUnderTest.RegisterAgentAsync(request);
        await _systemUnderTest.MarkAgentRunningAsync(agentId, "12345");

        // Verify agent is Running
        var agent = await _systemUnderTest.GetAgentAsync(agentId);
        agent!.Status.ShouldBe(AgentStatus.Running);

        // Advance time
        _timeService.AdvanceTime(TimeSpan.FromMinutes(1));

        // Act - Update heartbeat should keep Running agent as Running
        var success = await _systemUnderTest.UpdateHeartbeatAsync(agentId);

        // Assert
        success.ShouldBeTrue();

        var updatedAgent = await _systemUnderTest.GetAgentAsync(agentId);
        updatedAgent!.Status.ShouldBe(AgentStatus.Running);
        updatedAgent.LastHeartbeat.ShouldBe(_timeService.UtcNow);
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
        agent.ShouldBeNull<Agent>();
    }

    [Fact]
    public async Task WhenRegisteringMultipleAgents_ShouldCreateUniqueIds()
    {
        // Arrange
        var request1 =
            new AgentRegistrationRequest { PersonaId = "planner", AgentType = "planner", WorkingDirectory = "/path1" };
        var request2 = new AgentRegistrationRequest
        {
            PersonaId = "implementer",
            AgentType = "implementer",
            WorkingDirectory = "/path2"
        };

        // Act
        var agentId1 = await _systemUnderTest.RegisterAgentAsync(request1);
        var agentId2 = await _systemUnderTest.RegisterAgentAsync(request2);

        // Assert - Check database directly for both agents
        agentId1.ShouldNotBe<string>(agentId2);
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
        agent.ShouldNotBeNull<Agent>();
        agent.Id.ShouldBe(agentId);
        agent.PersonaId.ShouldBe("reviewer");
        agent.AgentType.ShouldBe("reviewer");
        agent.WorkingDirectory.ShouldBe("/test/review-path");
        agent.Model.ShouldBe("gemini-1.5-flash");
        agent.WorktreeName.ShouldBe("feature-branch");
        agent.Status.ShouldBe(AgentStatus.Starting);
        agent.RegisteredAt.ShouldBe(_timeService.UtcNow);
        agent.LastHeartbeat.ShouldBe(_timeService.UtcNow);
        agent.ProcessId.ShouldBeNull<string>();
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
        agent.ShouldNotBeNull<Agent>();
        agent.PersonaId.ShouldBe("tester");
        agent.AgentType.ShouldBe("tester");
        agent.WorkingDirectory.ShouldBe("/test/minimal-path");
        agent.Model.ShouldBeNull<string>();
        agent.WorktreeName.ShouldBeNull<string>();
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

    [Fact]
    public async Task WhenKillingAgentWithInProgressTasks_ShouldFailDanglingTasks()
    {
        // Arrange
        var request = new AgentRegistrationRequest
        {
            PersonaId = "implementer",
            AgentType = "implementer",
            WorkingDirectory = "/test/path"
        };
        var agentId = await _systemUnderTest.RegisterAgentAsync(request);

        // Create some tasks for this agent
        var task1 = new WorkItem
        {
            Id = "task-1",
            AgentId = agentId,
            Status = TaskStatus.InProgress,
            Persona = "implementer",
            Description = "Task 1 in progress",
            CreatedAt = _timeService.UtcNow,
            StartedAt = _timeService.UtcNow
        };

        var task2 = new WorkItem
        {
            Id = "task-2",
            AgentId = agentId,
            Status = TaskStatus.Pending,
            Persona = "implementer",
            Description = "Task 2 pending",
            CreatedAt = _timeService.UtcNow
        };

        var task3 = new WorkItem
        {
            Id = "task-3",
            AgentId = "other-agent",
            Status = TaskStatus.InProgress,
            Persona = "implementer",
            Description = "Task 3 for different agent",
            CreatedAt = _timeService.UtcNow
        };

        _dbContext.Tasks.AddRange(task1, task2, task3);
        await _dbContext.SaveChangesAsync();

        // Act
        await _systemUnderTest.KillAgentAsync(agentId);

        // Assert
        var updatedTask1 = await _dbContext.Tasks.FindAsync("task-1");
        var updatedTask2 = await _dbContext.Tasks.FindAsync("task-2");
        var updatedTask3 = await _dbContext.Tasks.FindAsync("task-3");

        // Task 1 was InProgress for this agent - should be Failed
        updatedTask1!.Status.ShouldBe(TaskStatus.Failed);
        updatedTask1.Result.ShouldNotBeNull();
        updatedTask1.Result.ShouldContain("Agent terminated");
        updatedTask1.CompletedAt.ShouldBe(_timeService.UtcNow);

        // Task 2 was only Pending for this agent - should remain Pending
        updatedTask2!.Status.ShouldBe(TaskStatus.Pending);
        updatedTask2.Result.ShouldBeNull();
        updatedTask2.CompletedAt.ShouldBeNull();

        // Task 3 belongs to different agent - should be unchanged
        updatedTask3!.Status.ShouldBe(TaskStatus.InProgress);
        updatedTask3.Result.ShouldBeNull();
        updatedTask3.CompletedAt.ShouldBeNull();
    }
}
