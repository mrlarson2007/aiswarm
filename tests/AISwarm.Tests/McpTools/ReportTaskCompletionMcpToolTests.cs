using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure.Eventing;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;

namespace AISwarm.Tests.McpTools;

public class ReportTaskCompletionMcpToolTests
    : IDisposable, ISystemUnderTest<ReportTaskCompletionMcpTool>
{
    private readonly CoordinationDbContext _dbContext;
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;
    private readonly IEventBus<TaskEventType, ITaskLifecyclePayload> _bus =
        new InMemoryEventBus<TaskEventType, ITaskLifecyclePayload>();
    private readonly IWorkItemNotificationService _notifier;
    private ReportTaskCompletionMcpTool? _systemUnderTest;

    public ReportTaskCompletionMcpTool SystemUnderTest =>
        _systemUnderTest ??= new ReportTaskCompletionMcpTool(_scopeService, _timeService, _notifier);

    public ReportTaskCompletionMcpToolTests()
    {
        _timeService = new FakeTimeService();
        _notifier = new WorkItemNotificationService(_bus);

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

    public class TaskReportCompletionTests : ReportTaskCompletionMcpToolTests
    {
        [Fact]
        public async Task WhenReportingTaskCompletion_ShouldUpdateTaskStatusToCompleted()
        {
            // Arrange
            var agentId = "test-agent-123";
            var taskId = "test-task-456";
            var completionResult = "Task completed successfully - implemented user authentication feature";

            await CreateRunningAgentAsync(agentId);
            await CreatePendingTaskAsync(taskId, agentId);

            // Act
            var result = await SystemUnderTest.ReportTaskCompletionAsync(taskId, completionResult);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Message.ShouldContain("Task completed successfully");

            // Assert - Check database directly instead of using service API
            using var scope = _scopeService.CreateReadScope();
            var task = await scope.Tasks.FindAsync(taskId);
            task.ShouldNotBeNull();
            task.Status.ShouldBe(TaskStatus.Completed);
            task.Result.ShouldBe(completionResult);
            task.CompletedAt.ShouldNotBeNull();
        }

        [Fact]
        public async Task WhenReportingCompletionForNonExistentTask_ShouldReturnFailureResult()
        {
            // Arrange
            var taskId = "non-existent-task";
            var completionResult = "Some result";

            // Act
            var result = await SystemUnderTest.ReportTaskCompletionAsync(taskId, completionResult);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Message.ShouldContain("Task not found");
            result.Message.ShouldContain(taskId);
        }

        [Fact]
        public async Task WhenReportingCompletionForAlreadyCompletedTask_ShouldReturnFailureResult()
        {
            // Arrange
            var agentId = "test-agent-123";
            var taskId = "test-task-456";
            var completionResult = "New completion result";

            await CreateRunningAgentAsync(agentId);
            await CreateCompletedTaskAsync(taskId, agentId);

            // Act
            var result = await SystemUnderTest.ReportTaskCompletionAsync(taskId, completionResult);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Message.ShouldContain("already completed");
            result.Message.ShouldContain(taskId);
        }

        [Fact]
        public async Task WhenReportingCompletionForFailedTask_ShouldUpdateStatusToCompleted()
        {
            // Arrange
            var taskId = "failed-task";
            var agentId = "test-agent";
            var errorMessage = "Task failed due to some error";
            await CreateFailedTaskAsync(taskId, agentId);

            // Act
            var result = await SystemUnderTest.ReportTaskCompletionAsync(taskId, errorMessage);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            using var scope = _scopeService.CreateReadScope();
            var task = await scope.Tasks.FindAsync(taskId);
            task.ShouldNotBeNull();
            task.Status.ShouldBe(TaskStatus.Completed);
            task.Result.ShouldBe(errorMessage);
            task.CompletedAt.ShouldNotBeNull();
        }

        [Fact(Timeout = 5000)]
        public async Task WhenReportingTaskCompletion_ShouldPublishTaskCompletedEvent()
        {
            var agentId = "notify-agent";
            var taskId = "notify-complete";
            await CreateInProgressTaskAsync(taskId, agentId);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var token = cts.Token;
            var received = new List<TaskEventEnvelope>();

            var readTask = Task.Run(async () =>
            {
                await foreach (var evt in _notifier.SubscibeForTaskCompletion([taskId], token))
                {
                    if (evt.Type == TaskEventType.Completed)
                    {
                        received.Add(evt);
                        break;
                    }
                }
            }, token);

            await Task.Delay(30, token);
            var result = await SystemUnderTest.ReportTaskCompletionAsync(taskId, "done");
            result.IsSuccess.ShouldBeTrue();

            await readTask;
            received.Count.ShouldBe(1);
        }
    }

    public class TaskReportFailureTests : ReportTaskCompletionMcpToolTests
    {
        [Fact]
        public async Task WhenTaskExists_ShouldMarkTaskAsFailed()
        {
            // Arrange
            var taskId = "test-task-failure";
            var agentId = "test-agent";
            var errorMessage = "Unable to find yolo parameter in codebase";
            await CreateInProgressTaskAsync(taskId, agentId);

            // Act
            var result = await SystemUnderTest.ReportTaskFailureAsync(taskId, errorMessage);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            // Assert - Check database directly instead of using service API
            using var scope = _scopeService.CreateReadScope();
            var task = await scope.Tasks.FindAsync(taskId);
            task.ShouldNotBeNull();
            task.Status.ShouldBe(TaskStatus.Failed);
            task.Result.ShouldBe(errorMessage);
            task.CompletedAt.ShouldNotBeNull();
        }

        [Fact]
        public async Task WhenTaskNotFound_ShouldReturnFailure()
        {
            // Arrange
            var nonExistentTaskId = "non-existent-task";
            var errorMessage = "Task failed due to missing dependency";

            // Act
            var result = await SystemUnderTest.ReportTaskFailureAsync(nonExistentTaskId, errorMessage);

            // Assert
            result.IsSuccess.ShouldBeFalse();
            result.Message.ShouldBe($"Task not found: {nonExistentTaskId}");
        }

        [Fact]
        public async Task WhenTaskAlreadyCompleted_ShouldStillMarkAsFailed()
        {
            // Arrange
            var taskId = "completed-task";
            var agentId = "test-agent";
            var errorMessage = "Task failed after completion";
            await CreateCompletedTaskAsync(taskId, agentId);

            // Act
            var result = await SystemUnderTest.ReportTaskFailureAsync(taskId, errorMessage);

            // Assert
            result.IsSuccess.ShouldBeTrue();

            // Assert - Check database directly instead of using service API
            using var scope = _scopeService.CreateReadScope();
            var task = await scope.Tasks.FindAsync(taskId);
            task.ShouldNotBeNull();
            task.Status.ShouldBe(TaskStatus.Failed);
            task.Result.ShouldBe(errorMessage);
        }

        [Fact(Timeout = 5000)]
        public async Task WhenReportingTaskFailure_ShouldPublishTaskFailedEvent()
        {
            var agentId = "notify-agent-2";
            var taskId = "notify-failed";
            await CreateInProgressTaskAsync(taskId, agentId);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var token = cts.Token;
            var received = new List<TaskEventEnvelope>();

            var readTask = Task.Run(async () =>
            {
                await foreach (var evt in _notifier.SubscibeForTaskCompletion([taskId], token))
                {
                    if (evt.Type == TaskEventType.Failed)
                    {
                        received.Add(evt);
                        break;
                    }
                }
            }, token);
            await Task.Delay(5, token);

            var result = await SystemUnderTest.ReportTaskFailureAsync(taskId, "error");
            result.IsSuccess.ShouldBeTrue();

            await readTask;
            received.Count.ShouldBe(1);
        }
    }

    private async Task CreateRunningAgentAsync(string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        var agent = new Agent
        {
            Id = agentId,
            PersonaId = "test-persona",
            AgentType = "test",
            WorkingDirectory = "/test",
            Status = AgentStatus.Running,
            RegisteredAt = _timeService.UtcNow,
            LastHeartbeat = _timeService.UtcNow
        };
        scope.Agents.Add(agent);
        await scope.SaveChangesAsync();
        scope.Complete();
    }

    private async Task CreatePendingTaskAsync(string taskId, string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = TaskStatus.Pending,
            Persona = "Test persona content",
            Description = "Test task description",
            Priority = TaskPriority.Normal,
            CreatedAt = _timeService.UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
    }

    private async Task CreateInProgressTaskAsync(string taskId, string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = TaskStatus.InProgress,
            Persona = "Test persona content",
            Description = "Test task description",
            Priority = TaskPriority.Normal,
            CreatedAt = _timeService.UtcNow,
            StartedAt = _timeService.UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
    }

    private async Task CreateCompletedTaskAsync(string taskId, string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = TaskStatus.Completed,
            Persona = "Test persona content",
            Description = "Test task description",
            Priority = TaskPriority.Normal,
            CreatedAt = _timeService.UtcNow,
            CompletedAt = _timeService.UtcNow,
            Result = "Previously completed"
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
    }

    private async Task CreateFailedTaskAsync(string taskId, string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = TaskStatus.Failed,
            Persona = "Test persona content",
            Description = "Test task description",
            Priority = TaskPriority.Normal,
            CreatedAt = _timeService.UtcNow,
            CompletedAt = _timeService.UtcNow,
            Result = "Previously completed"
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
    }
}
