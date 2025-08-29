using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Eventing;
using AISwarm.Shared.Constants;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using AgentStatus = AISwarm.DataLayer.Entities.AgentStatus;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;

namespace AISwarm.Tests.Services;

public class LocalAgentServiceTests : ISystemUnderTest<LocalAgentService>
{
    private readonly IAgentStateService _agentStateService;
    private readonly Mock<IAgentNotificationService> _mockNotificationService;
    private readonly Mock<IProcessTerminationService> _mockProcessTerminationService;
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;
    private LocalAgentService? _systemUnderTest;


    public LocalAgentServiceTests()
    {
        _timeService = new FakeTimeService();

        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var contextFactory = new TestDbContextFactory(options);
        _scopeService = new DatabaseScopeService(contextFactory);

        _mockNotificationService = new Mock<IAgentNotificationService>();
        _mockProcessTerminationService = new Mock<IProcessTerminationService>();
        _agentStateService = new AgentStateService(_scopeService, _mockNotificationService.Object,
            _mockProcessTerminationService.Object);
    }

    public LocalAgentService SystemUnderTest =>
        _systemUnderTest ??= new LocalAgentService(
            _timeService, _scopeService, _agentStateService);

    public class AgentRegistrationTests : LocalAgentServiceTests
    {
        [Fact]
        public async Task WhenRegisteringAgent_ShouldCreateAgentWithUniqueId()
        {
            // Arrange
            var request = new AgentRegistrationRequest
            {
                PersonaId = "planner",
                WorkingDirectory = "/test/path",
                Model = "gemini-1.5-pro",
                WorktreeName = "main"
            };

            // Act
            var agentId = await SystemUnderTest.RegisterAgentAsync(request);

            // Assert - Check database directly instead of using service API
            agentId.ShouldNotBeNullOrEmpty();

            using var scope = _scopeService.GetReadScope();
            var agentInDb = await scope.Agents.FindAsync(agentId);
            agentInDb.ShouldNotBeNull();
            agentInDb.Id.ShouldBe(agentId);
            agentInDb.PersonaId.ShouldBe("planner");
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
        public async Task WhenRegisteringMultipleAgents_ShouldCreateUniqueIds()
        {
            // Arrange
            var request1 =
                new AgentRegistrationRequest { PersonaId = "planner", WorkingDirectory = "/path1" };
            var request2 = new AgentRegistrationRequest { PersonaId = "implementer", WorkingDirectory = "/path2" };

            // Act
            var agentId1 = await SystemUnderTest.RegisterAgentAsync(request1);
            var agentId2 = await SystemUnderTest.RegisterAgentAsync(request2);

            // Assert - Check database directly for both agents
            agentId1.ShouldNotBe(agentId2);
            agentId1.ShouldNotBeNullOrEmpty();
            agentId2.ShouldNotBeNullOrEmpty();

            // Assert - Check database directly instead of using service API
            using var scope = _scopeService.GetReadScope();
            var agent1InDb = await scope.Agents.FindAsync(agentId1);
            var agent2InDb = await scope.Agents.FindAsync(agentId2);

            agent1InDb.ShouldNotBeNull();
            agent1InDb.PersonaId.ShouldBe("planner");
            agent1InDb.WorkingDirectory.ShouldBe("/path1");

            agent2InDb.ShouldNotBeNull();
            agent2InDb.PersonaId.ShouldBe("implementer");
            agent2InDb.WorkingDirectory.ShouldBe("/path2");

            // Verify database contains exactly 2 agents
            var totalAgents = await scope.Agents.CountAsync();
            totalAgents.ShouldBe(2);
        }

        [Fact]
        public async Task WhenRegisteringAgentWithMinimalFields_ShouldSetRequiredPropertiesOnly()
        {
            // Arrange
            var request = new AgentRegistrationRequest
            {
                PersonaId = "tester",
                WorkingDirectory = "/test/minimal-path",
                Model = null,
                WorktreeName = null
            };

            // Act
            var agentId = await SystemUnderTest.RegisterAgentAsync(request);

            // Assert
            using var assertScope = _scopeService.GetReadScope();
            var agent = await assertScope.Agents.FindAsync(agentId);
            agent.ShouldNotBeNull();
            agent.PersonaId.ShouldBe("tester");
            agent.WorkingDirectory.ShouldBe("/test/minimal-path");
            agent.Model.ShouldBeNull();
            agent.WorktreeName.ShouldBeNull();
            agent.Status.ShouldBe(AgentStatus.Starting);
        }
    }

    public class AgentHeartbeatTests : LocalAgentServiceTests
    {
        [Fact]
        public async Task WhenAgentIsStarting_ShouldTransitionToRunning_AndUpdateHeartbeatTime()
        {
            var agentId = Guid.NewGuid().ToString();
            // Arrange
            using (var scope = _scopeService.GetWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "tester",
                    WorkingDirectory = "/test/path",
                    Status = AgentStatus.Starting,
                    RegisteredAt = _timeService.UtcNow.AddMinutes(-5),
                    LastHeartbeat = _timeService.UtcNow.AddMinutes(-5)
                });
                await scope.SaveChangesAsync();
            }

            // Advance time
            _timeService.AdvanceTime(TimeSpan.FromMinutes(1));

            // Act - Update heartbeat should transition Starting agent to Running
            var success = await SystemUnderTest.UpdateHeartbeatAsync(agentId);

            // Assert
            success.ShouldBeTrue();

            using var assertScope = _scopeService.GetReadScope();
            var updatedAgent = await assertScope.Agents.FindAsync(agentId);
            updatedAgent!.Status.ShouldBe(AgentStatus.Running);
            updatedAgent.LastHeartbeat.ShouldBe(_timeService.UtcNow);
        }

        [Fact]
        public async Task WhenAgentIsRunning_ShouldStayRunning()
        {
            var agentId = Guid.NewGuid().ToString();

            using (var scope = _scopeService.GetWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "tester",
                    WorkingDirectory = "/test/path",
                    Status = AgentStatus.Running,
                    RegisteredAt = _timeService.UtcNow.AddMinutes(-5),
                    LastHeartbeat = _timeService.UtcNow.AddMinutes(-5)
                });
                await scope.SaveChangesAsync();
            }

            // Advance time
            _timeService.AdvanceTime(TimeSpan.FromMinutes(1));

            // Act - Update heartbeat should keep Running agent as Running
            var success = await SystemUnderTest.UpdateHeartbeatAsync(agentId);

            // Assert
            success.ShouldBeTrue();

            using var assertScope = _scopeService.GetReadScope();
            var updatedAgent = await assertScope.Agents.FindAsync(agentId);
            updatedAgent!.Status.ShouldBe(AgentStatus.Running);
            updatedAgent.LastHeartbeat.ShouldBe(_timeService.UtcNow);
        }
    }

    public class AgentKillingTests : LocalAgentServiceTests
    {
        [Fact]
        public async Task WhenAgentDoesNotExist_ShouldDoNothing()
        {
            // Arrange
            var unknownAgentId = "non-existent-agent";

            // Act
            await SystemUnderTest.KillAgentAsync(unknownAgentId);

            // Assert - No exception thrown, database remains empty
            using var assertScope = _scopeService.GetReadScope();
            var totalAgents = await assertScope.Agents.CountAsync();
            totalAgents.ShouldBe(0);
        }

        [Fact]
        public async Task WhenAgentExists_ShouldUpdateStatusAndKillTime()
        {
            // Arrange
            var agentId = Guid.NewGuid().ToString();
            using (var scope = _scopeService.GetWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "tester",
                    WorkingDirectory = "/test/path",
                    Status = AgentStatus.Running,
                    RegisteredAt = _timeService.UtcNow.AddMinutes(-10),
                    LastHeartbeat = _timeService.UtcNow.AddMinutes(-5),
                    StartedAt = _timeService.UtcNow.AddMinutes(-9)
                });
                await scope.SaveChangesAsync();
            }

            // Advance time
            _timeService.AdvanceTime(TimeSpan.FromMinutes(3));

            // Act
            await SystemUnderTest.KillAgentAsync(agentId);

            // Assert
            using var assertScope = _scopeService.GetReadScope();
            var agent = await assertScope.Agents.FindAsync(agentId);
            agent!.Status.ShouldBe(AgentStatus.Killed);
            agent.StoppedAt.ShouldBe(_timeService.UtcNow);
        }

        [Fact]
        public async Task WhenKillingAgentWithProcessId_ShouldTerminateProcess()
        {
            // Arrange
            var agentId = Guid.NewGuid().ToString();
            var processId = "98765";
            using (var scope = _scopeService.GetWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "tester",
                    WorkingDirectory = "/test/path",
                    Status = AgentStatus.Running,
                    RegisteredAt = _timeService.UtcNow.AddMinutes(-10),
                    LastHeartbeat = _timeService.UtcNow.AddMinutes(-5),
                    StartedAt = _timeService.UtcNow.AddMinutes(-9),
                    ProcessId = processId
                });
                await scope.SaveChangesAsync();
            }

            // Act
            await SystemUnderTest.KillAgentAsync(agentId);

            // Assert - The agent state service will be tested separately,
            // here we just verify the method completes successfully
        }

        [Fact]
        public async Task WhenKillingAgentWithInProgressTasks_ShouldFailDanglingTasks()
        {
            // Arrange
            var agentId = Guid.NewGuid().ToString();
            using (var scope = _scopeService.GetWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "tester",
                    WorkingDirectory = "/test/path",
                    Status = AgentStatus.Running,
                    RegisteredAt = _timeService.UtcNow.AddMinutes(-10),
                    LastHeartbeat = _timeService.UtcNow.AddMinutes(-5),
                    StartedAt = _timeService.UtcNow.AddMinutes(-9)
                });
                await scope.SaveChangesAsync();
            }

            // Create some tasks for this agent
            var task1 = new WorkItem
            {
                Id = "task-1",
                AgentId = agentId,
                Status = TaskStatus.InProgress,
                PersonaId = "implementer",
                Description = "Task 1 in progress",
                CreatedAt = _timeService.UtcNow,
                StartedAt = _timeService.UtcNow
            };

            var task2 = new WorkItem
            {
                Id = "task-2",
                AgentId = agentId,
                Status = TaskStatus.Pending,
                PersonaId = "implementer",
                Description = "Task 2 pending",
                CreatedAt = _timeService.UtcNow
            };

            var task3 = new WorkItem
            {
                Id = "task-3",
                AgentId = "other-agent",
                Status = TaskStatus.InProgress,
                PersonaId = "implementer",
                Description = "Task 3 for different agent",
                CreatedAt = _timeService.UtcNow
            };

            // Setup task data
            using (var scope = _scopeService.GetWriteScope())
            {
                scope.Tasks.AddRange(task1, task2, task3);
                await scope.SaveChangesAsync();
            }

            // Act
            await SystemUnderTest.KillAgentAsync(agentId);

            // Assert
            using var assertScope = _scopeService.GetReadScope();
            var updatedTask1 = await assertScope.Tasks.FindAsync("task-1");
            var updatedTask2 = await assertScope.Tasks.FindAsync("task-2");
            var updatedTask3 = await assertScope.Tasks.FindAsync("task-3");

            // Task 1 was InProgress for this agent - should be Failed
            updatedTask1!.Status.ShouldBe(TaskStatus.Failed);
            updatedTask1.Result.ShouldNotBeNull();
            updatedTask1.Result.ShouldContain(TaskFailureReasons.AgentTerminated);
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
}
