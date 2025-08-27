using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Server.McpTools;
using AISwarm.Infrastructure.Eventing;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;

namespace AISwarm.Tests.McpTools;

public class CreateTaskMcpToolTests : IDisposable, ISystemUnderTest<CreateTaskMcpTool>
{
    private readonly CoordinationDbContext _dbContext;
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;
    private readonly IWorkItemNotificationService _notifier;
    private CreateTaskMcpTool? _systemUnderTest;

    public CreateTaskMcpTool SystemUnderTest =>
        _systemUnderTest ??= new CreateTaskMcpTool(
            _scopeService,
            _timeService,
            _notifier);

    public CreateTaskMcpToolTests()
    {
        _timeService = new FakeTimeService();
    _notifier = new WorkItemNotificationService(new InMemoryEventBus());

        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new CoordinationDbContext(options);
        _scopeService = new DatabaseScopeService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    public class TaskCreationTests : CreateTaskMcpToolTests
    {
        [Fact(Timeout = 5000)]
        public async Task WhenCreatingUnassignedTask_ShouldPublishTaskCreatedEvent()
        {
            // Arrange
            var persona = "planner";
            var description = "Plan next steps";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var token = cts.Token;
            var received = new List<EventEnvelope>();

            var readTask = Task.Run(async () =>
            {
                await foreach (var evt in (_notifier as WorkItemNotificationService)!
                    .SubscribeForPersona(persona, token))
                {
                    received.Add(evt);
                    break;
                }
            }, token);

            await Task.Delay(5, token);

            // Act
            var result = await SystemUnderTest.CreateTaskAsync(null, persona, description);
            result.Success.ShouldBeTrue();

            // Assert - event should be delivered
            await readTask;
            received.Count.ShouldBe(1);
            var payload = (TaskCreatedPayload)received[0].Payload!;
            payload.TaskId.ShouldBe(result.TaskId);
            payload.AgentId.ShouldBeNull();
            payload.Persona.ShouldBe(persona);
        }
        [Fact]
        public async Task WhenCreatingTaskForRunningAgent_ShouldSaveTaskToDatabase()
        {
            // Arrange
            var agentId = "agent-123";
            var persona = "You are a code reviewer. Review code for quality and security.";
            var description = "Review the authentication module for security vulnerabilities";

            // Create a running agent first
            using (var scope = _scopeService.CreateWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "test-persona",
                    AgentType = "test",
                    WorkingDirectory = "/test",
                    Status = AgentStatus.Running,
                    RegisteredAt = _timeService.UtcNow,
                    LastHeartbeat = _timeService.UtcNow
                });
                await scope.SaveChangesAsync();
            }

            // Act
            var result = await SystemUnderTest.CreateTaskAsync(agentId, persona, description);

            // Assert
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldNotBeNull();

            // Assert - Check database directly instead of using service API
            var taskInDb = await _dbContext.Tasks.FindAsync(result.TaskId);
            taskInDb.ShouldNotBeNull();
            taskInDb.Id.ShouldBe(result.TaskId);
            taskInDb.AgentId.ShouldBe(agentId);
            taskInDb.Persona.ShouldBe(persona);
            taskInDb.Description.ShouldBe(description);
            taskInDb.Status.ShouldBe(TaskStatus.Pending);
            taskInDb.Priority.ShouldBe(TaskPriority.Normal);
            taskInDb.CreatedAt.ShouldBe(_timeService.UtcNow);
            taskInDb.StartedAt.ShouldBeNull();
            taskInDb.CompletedAt.ShouldBeNull();
            taskInDb.Result.ShouldBeNull();
        }

        [Fact]
        public async Task WhenCreatingTaskForStartingAgent_ShouldCreateTaskSuccessfully()
        {
            // Arrange
            var agentId = "starting-agent-123";
            var persona = "You are a code reviewer. Review code for quality and security.";
            var description = "Review code while agent is starting up";

            // Create a starting agent first
            using (var scope = _scopeService.CreateWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "test-persona",
                    AgentType = "test",
                    WorkingDirectory = "/test",
                    Status = AgentStatus.Starting,
                    RegisteredAt = _timeService.UtcNow,
                    LastHeartbeat = _timeService.UtcNow
                });
                await scope.SaveChangesAsync();
            }

            // Act
            var result = await SystemUnderTest.CreateTaskAsync(agentId, persona, description);

            // Assert
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldNotBeNull();

            // Assert - Check database directly instead of using service API
            var taskInDb = await _dbContext.Tasks.FindAsync(result.TaskId);
            taskInDb.ShouldNotBeNull();
            taskInDb.AgentId.ShouldBe(agentId);
            taskInDb.Persona.ShouldBe(persona);
            taskInDb.Description.ShouldBe(description);
            taskInDb.Status.ShouldBe(TaskStatus.Pending);
        }

        [Fact]
        public async Task WhenCreatingUnassignedTask_ShouldCreateTaskWithNullAgentId()
        {
            // Arrange
            var persona = "You are a code reviewer. Review code for quality and security.";
            var description = "Review the authentication module for security vulnerabilities";

            // Act
            var result = await SystemUnderTest.CreateTaskAsync(null, persona, description);

            // Assert
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldNotBeNull();

            // Assert - Check database directly instead of using service API
            var taskInDb = await _dbContext.Tasks.FindAsync(result.TaskId);
            taskInDb.ShouldNotBeNull();
            taskInDb.AgentId.ShouldBeNull();
            taskInDb.Persona.ShouldBe(persona);
            taskInDb.Description.ShouldBe(description);
            taskInDb.Status.ShouldBe(TaskStatus.Pending);
        }

        [Fact]
        public async Task WhenCreatingTaskWithHighPriority_ShouldSetPriorityCorrectly()
        {
            // Arrange
            var agentId = "agent-priority-test";
            var persona = "You are a security expert.";
            var description = "Critical security vulnerability fix";

            using (var scope = _scopeService.CreateWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "security-expert",
                    AgentType = "security",
                    WorkingDirectory = "/security",
                    Status = AgentStatus.Running,
                    RegisteredAt = _timeService.UtcNow,
                    LastHeartbeat = _timeService.UtcNow
                });
                await scope.SaveChangesAsync();
            }

            // Act
            var result = await SystemUnderTest.CreateTaskAsync(agentId, persona, description, TaskPriority.Critical);

            // Assert
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldNotBeNull();

            // Assert - Check database directly for priority setting
            var taskInDb = await _dbContext.Tasks.FindAsync(result.TaskId);
            taskInDb.ShouldNotBeNull();
            taskInDb.Priority.ShouldBe(TaskPriority.Critical);
            taskInDb.AgentId.ShouldBe(agentId);
        }
    }

    public class TaskCreationFailureTests : CreateTaskMcpToolTests
    {
        [Fact]
        public async Task WhenCreatingTaskForNonExistentAgent_ShouldReturnFailureResult()
        {
            // Arrange
            var nonExistentAgentId = "non-existent-agent";
            var persona = "You are a code reviewer.";
            var description = "Review code";

            // Act
            var result = await SystemUnderTest.CreateTaskAsync(nonExistentAgentId, persona, description);

            // Assert
            result.Success.ShouldBeFalse();
            result.TaskId.ShouldBeNull();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("Agent not found");
            result.ErrorMessage.ShouldContain(nonExistentAgentId);

            // Assert - Verify no task was created in database
            var totalTasks = await _dbContext.Tasks.CountAsync();
            totalTasks.ShouldBe(0);
        }

        [Fact]
        public async Task WhenCreatingTaskForStoppedAgent_ShouldReturnFailureResult()
        {
            // Arrange
            var agentId = "stopped-agent-123";
            var persona = "You are a code reviewer.";
            var description = "Review code";

            // Create a stopped agent first
            using (var scope = _scopeService.CreateWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "test-persona",
                    AgentType = "test",
                    WorkingDirectory = "/test",
                    Status = AgentStatus.Stopped,
                    RegisteredAt = _timeService.UtcNow,
                    LastHeartbeat = _timeService.UtcNow,
                    StoppedAt = _timeService.UtcNow
                });
                await scope.SaveChangesAsync();
            }

            // Act
            var result = await SystemUnderTest.CreateTaskAsync(agentId, persona, description);

            // Assert
            result.Success.ShouldBeFalse();
            result.TaskId.ShouldBeNull();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("Agent is not in a valid state to receive tasks");
            result.ErrorMessage.ShouldContain(agentId);
            result.ErrorMessage.ShouldContain("Stopped");

            // Assert - Verify no task was created in database
            var totalTasks = await _dbContext.Tasks.CountAsync();
            totalTasks.ShouldBe(0);
        }

        [Fact]
        public async Task WhenCreatingTaskForKilledAgent_ShouldReturnFailureResult()
        {
            // Arrange
            var agentId = "killed-agent-123";
            var persona = "You are a code reviewer.";
            var description = "Review code";

            // Create a killed agent first
            using (var scope = _scopeService.CreateWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "test-persona",
                    AgentType = "test",
                    WorkingDirectory = "/test",
                    Status = AgentStatus.Killed,
                    RegisteredAt = _timeService.UtcNow,
                    LastHeartbeat = _timeService.UtcNow,
                    StoppedAt = _timeService.UtcNow
                });
                await scope.SaveChangesAsync();
            }

            // Act
            var result = await SystemUnderTest.CreateTaskAsync(agentId, persona, description);

            // Assert
            result.Success.ShouldBeFalse();
            result.TaskId.ShouldBeNull();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("Agent is not in a valid state to receive tasks");
            result.ErrorMessage.ShouldContain(agentId);
            result.ErrorMessage.ShouldContain("Killed");

            // Assert - Verify no task was created in database
            var totalTasks = await _dbContext.Tasks.CountAsync();
            totalTasks.ShouldBe(0);
        }
    }
}
