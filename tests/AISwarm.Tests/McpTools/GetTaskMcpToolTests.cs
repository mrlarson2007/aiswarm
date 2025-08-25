using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;

namespace AISwarm.Tests.McpTools;

public class GetTaskMcpToolTests
    : IDisposable, ISystemUnderTest<GetTaskMcpTool>
{
    private readonly CoordinationDbContext _dbContext;
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;
    private GetTaskMcpTool? _systemUnderTest;

    public GetTaskMcpTool SystemUnderTest =>
        _systemUnderTest ??= new GetTaskMcpTool(_scopeService);

    public GetTaskMcpToolTests()
    {
        _timeService = new FakeTimeService();

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

    public class GetTasksByStatusTests : GetTaskMcpToolTests
    {
        [Fact]
        public async Task WhenInvalidStatus_ShouldReturnFailure()
        {
            // Arrange
            var invalidStatus = "InvalidStatus";

            // Act
            var result = await SystemUnderTest.GetTasksByStatusAsync(invalidStatus);

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("Invalid status");
            result.Tasks.ShouldBeNull();
        }

        [Fact]
        public async Task WhenNoTasksWithStatus_ShouldReturnEmptyArray()
        {
            // Arrange
            var validStatus = "Pending";

            // Act
            var result = await SystemUnderTest.GetTasksByStatusAsync(validStatus);

            // Assert
            result.Success.ShouldBeTrue();
            result.ErrorMessage.ShouldBeNull();
            result.Tasks.ShouldNotBeNull();
            result.Tasks.ShouldBeEmpty();
        }

        [Theory]
        [InlineData("Pending")]
        [InlineData("InProgress")]
        [InlineData("Completed")]
        [InlineData("Failed")]
        public async Task WhenTasksExistWithStatus_ShouldReturnFilteredTasks(string statusFilter)
        {
            // Arrange
            _ = await CreateTaskAsync("task1", TaskStatus.Pending, "agent1");
            _ = await CreateTaskAsync("task2", TaskStatus.InProgress, "agent2");
            _ = await CreateTaskAsync("task3", TaskStatus.Completed, "agent1");
            _ = await CreateTaskAsync("task4", TaskStatus.Failed, "agent3");
            _ = await CreateTaskAsync("task5", Enum.Parse<TaskStatus>(statusFilter), "agent4");

            // Act
            var result = await SystemUnderTest.GetTasksByStatusAsync(statusFilter);

            // Assert
            result.Success.ShouldBeTrue();
            result.ErrorMessage.ShouldBeNull();
            result.Tasks.ShouldNotBeNull();

            // Should return tasks that match the filter (task5 plus any others with same status)
            var expectedTasks = result
                .Tasks
                .Where(t => t.Status == statusFilter);
            expectedTasks.ShouldNotBeEmpty();

            // Verify all returned tasks have the correct status
            result
                .Tasks
                .ShouldAllBe(t => t.Status == statusFilter);

            // Verify task5 is included
            var task5Result = result
                .Tasks
                .Single(t => t.TaskId == "task5");
            task5Result.Status.ShouldBe(statusFilter);
            task5Result.AgentId.ShouldBe("agent4");
        }

        [Theory]
        [InlineData("InProgress")]
        [InlineData("Completed")]
        public async Task WhenTasksHaveTimestamps_ShouldReturnTimestampFields(string statusFilter)
        {
            // Arrange
            var baseTime = _timeService.UtcNow;
            var taskStatus = Enum.Parse<TaskStatus>(statusFilter);
            _ = await CreateTaskAsync("timestampTask", taskStatus, "agent1");

            // Act
            var result = await SystemUnderTest.GetTasksByStatusAsync(statusFilter);

            // Assert
            result.Success.ShouldBeTrue();
            result.Tasks.ShouldNotBeNull();
            result.Tasks.Length.ShouldBe(1);

            var returnedTask = result.Tasks[0];
            returnedTask.TaskId.ShouldBe("timestampTask");

            if (taskStatus == TaskStatus.InProgress)
            {
                returnedTask.StartedAt.ShouldBe(baseTime);
                returnedTask.CompletedAt.ShouldBeNull();
            }
            else if (taskStatus == TaskStatus.Completed)
            {
                returnedTask.CompletedAt.ShouldBe(baseTime);
            }
        }
    }

    public class GetTaskStatusTests : GetTaskMcpToolTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("non-existent-task-id")]
        [InlineData("550e8400-e29b-41d4-a716-446655440000")] // Random GUID that doesn't exist
        public async Task WhenTaskIdIsInvalidOrNotFound_ShouldReturnNullValues(string taskId)
        {
            // Act
            var result = await SystemUnderTest.GetTaskStatusAsync(taskId);

            // Assert
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldBeNull();
            result.Status.ShouldBeNull();
            result.AgentId.ShouldBeNull();
            result.StartedAt.ShouldBeNull();
            result.CompletedAt.ShouldBeNull();
        }

        [Theory]
        [InlineData("Pending")]
        [InlineData("InProgress")]
        [InlineData("Completed")]
        [InlineData("Failed")]
        public async Task WhenTaskHasAgentId_ShouldReturnAgentIdForAllStatuses(string statusName)
        {
            // Arrange
            var expectedAgentId = "test-agent-123";
            var taskId = await CreateTaskForStatusTestAsync(statusName, "Test persona", "Test description", expectedAgentId);

            // Act
            var result = await SystemUnderTest.GetTaskStatusAsync(taskId);

            // Assert
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldBe(taskId);
            result.Status.ShouldBe(statusName);
            result.AgentId.ShouldBe(expectedAgentId);
            result.StartedAt.ShouldBe(_timeService.UtcNow.AddMinutes(1));
            result.CompletedAt.ShouldBe(_timeService.UtcNow.AddMinutes(5));
        }

        [Theory]
        [InlineData("Pending")]
        [InlineData("InProgress")]
        [InlineData("Completed")]
        [InlineData("Failed")]
        public async Task WhenTaskHasEmptyAgentId_ShouldReturnEmptyAgentIdForAllStatuses(string statusName)
        {
            // Arrange
            var taskId = await CreateTaskForStatusTestAsync(statusName, "Test persona", "Test description", string.Empty);

            // Act
            var result = await SystemUnderTest.GetTaskStatusAsync(taskId);

            // Assert
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldBe(taskId);
            result.Status.ShouldBe(statusName);
            result.AgentId.ShouldBeEmpty();
            result.StartedAt.ShouldBe(_timeService.UtcNow.AddMinutes(1));
            result.CompletedAt.ShouldBe(_timeService.UtcNow.AddMinutes(5));
        }
    }

    public class GetTasksByAgentIdTests : GetTaskMcpToolTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("non-existent-agent")]
        public async Task WhenNoTasksForAgent_ShouldReturnEmptyArray(string? agentId)
        {
            // Arrange - Create some tasks but not for the target agent
            await CreateTaskAsync("task1", TaskStatus.Pending, "existing-agent-1");
            await CreateTaskAsync("task2", TaskStatus.InProgress, "existing-agent-2");

            // Act
            var result = await SystemUnderTest.GetTasksByAgentIdAsync(agentId!);

            // Assert
            result.Success.ShouldBeTrue();
            result.ErrorMessage.ShouldBeNull();
            result.Tasks.ShouldNotBeNull();
            result.Tasks.ShouldBeEmpty();
        }

        [Fact]
        public async Task WhenAgentExistsButNoTasks_ShouldReturnEmptyArray()
        {
            // Arrange - Create a task for an agent, then query for different agent that has no tasks
            await CreateTaskAsync("task1", TaskStatus.Pending, "agent-with-tasks");
            var agentWithNoTasks = "agent-without-tasks";

            // Act
            var result = await SystemUnderTest.GetTasksByAgentIdAsync(agentWithNoTasks);

            // Assert
            result.Success.ShouldBeTrue();
            result.ErrorMessage.ShouldBeNull();
            result.Tasks.ShouldNotBeNull();
            result.Tasks.ShouldBeEmpty();
        }

        [Fact]
        public async Task WhenSearchingForNullAgentId_ShouldReturnTasksWithNullAgentId()
        {
            // Arrange - Create tasks with null and non-null agent IDs
            await CreateTaskAsync("task1", TaskStatus.Pending);
            await CreateTaskAsync("task2", TaskStatus.InProgress, "actual-agent");
            await CreateTaskAsync("task3", TaskStatus.Completed);

            // Act
            var result = await SystemUnderTest.GetTasksByAgentIdAsync(null!);

            // Assert
            result.Success.ShouldBeTrue();
            result.ErrorMessage.ShouldBeNull();
            result.Tasks.ShouldNotBeNull();
            result.Tasks.Length.ShouldBe(2);
            result.Tasks.ShouldAllBe(t => t.AgentId == null);

            // Verify specific tasks are included
            result.Tasks.Any(t => t.TaskId == "task1").ShouldBeTrue();
            result.Tasks.Any(t => t.TaskId == "task3").ShouldBeTrue();
        }

        [Fact]
        public async Task WhenTasksExistForAgent_ShouldReturnFilteredTasks()
        {
            // Arrange
            var targetAgentId = "agent-123";
            await CreateTaskAsync("task1", TaskStatus.Pending, targetAgentId);
            await CreateTaskAsync("task2", TaskStatus.InProgress, "other-agent");
            await CreateTaskAsync("task3", TaskStatus.Completed, targetAgentId);
            await CreateTaskAsync("task4", TaskStatus.Failed, "another-agent");

            // Act
            var result = await SystemUnderTest.GetTasksByAgentIdAsync(targetAgentId);

            // Assert
            result.Success.ShouldBeTrue();
            result.ErrorMessage.ShouldBeNull();
            result.Tasks.ShouldNotBeNull();
            result.Tasks.Length.ShouldBe(2);

            // Verify all returned tasks belong to the target agent
            result.Tasks.ShouldAllBe(t => t.AgentId == targetAgentId);

            // Verify specific tasks are included
            var returnedTask1 = result.Tasks.Single(t => t.TaskId == "task1");
            returnedTask1.Status.ShouldBe("Pending");
            returnedTask1.AgentId.ShouldBe(targetAgentId);

            var returnedTask3 = result.Tasks.Single(t => t.TaskId == "task3");
            returnedTask3.Status.ShouldBe("Completed");
            returnedTask3.AgentId.ShouldBe(targetAgentId);
        }

        [Fact]
        public async Task WhenTasksHaveTimestamps_ShouldReturnTimestampFields()
        {
            // Arrange
            var agentId = "timestamp-agent";
            var baseTime = _timeService.UtcNow;
            await CreateTaskAsync("inProgressTask", TaskStatus.InProgress, agentId);
            await CreateTaskAsync("completedTask", TaskStatus.Completed, agentId);

            // Act
            var result = await SystemUnderTest.GetTasksByAgentIdAsync(agentId);

            // Assert
            result.Success.ShouldBeTrue();
            result.Tasks.ShouldNotBeNull();
            result.Tasks.Length.ShouldBe(2);

            var inProgressTask = result.Tasks.Single(t => t.TaskId == "inProgressTask");
            inProgressTask.StartedAt.ShouldBe(baseTime);
            inProgressTask.CompletedAt.ShouldBeNull();

            var completedTask = result.Tasks.Single(t => t.TaskId == "completedTask");
            completedTask.CompletedAt.ShouldBe(baseTime);
        }
    }

    public class GetTasksByAgentIdAndStatusTests : GetTaskMcpToolTests
    {
        [Fact]
        public async Task WhenInvalidStatus_ShouldReturnFailure()
        {
            // Arrange
            var agentId = "test-agent";
            var invalidStatus = "InvalidStatus";

            // Act
            var result = await SystemUnderTest.GetTasksByAgentIdAndStatusAsync(agentId, invalidStatus);

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("Invalid status");
            result.Tasks.ShouldBeNull();
        }

        [Theory]
        [InlineData("agent-1", "Pending")]
        [InlineData("agent-2", "InProgress")]
        [InlineData("non-existent-agent", "Completed")]
        public async Task WhenNoTasksMatch_ShouldReturnEmptyArray(string agentId, string status)
        {
            // Arrange
            await CreateTaskAsync("task1", TaskStatus.Pending, "agent-x");
            await CreateTaskAsync("task2", TaskStatus.InProgress, "agent-y");

            // Act
            var result = await SystemUnderTest.GetTasksByAgentIdAndStatusAsync(agentId, status);

            // Assert
            result.Success.ShouldBeTrue();
            result.ErrorMessage.ShouldBeNull();
            result.Tasks.ShouldNotBeNull();
            result.Tasks.ShouldBeEmpty();
        }
    }

    private async Task<WorkItem> CreateTaskAsync(
        string taskId,
        TaskStatus status,
        string? agentId = null)
    {
        using var scope = _scopeService.CreateWriteScope();
        var task = new WorkItem
        {
            Id = taskId,
            Status = status,
            AgentId = agentId,
            Persona = "Test persona",
            Description = "Test description",
            CreatedAt = _timeService.UtcNow,
            StartedAt = status == TaskStatus.InProgress ? _timeService.UtcNow : null,
            CompletedAt = status == TaskStatus.Completed ? _timeService.UtcNow : null
        };

        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return task;
    }

    private async Task<string> CreateTaskForStatusTestAsync(
        string statusName,
        string persona,
        string description,
        string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        var id = Guid.NewGuid().ToString();
        var status = Enum.Parse<TaskStatus>(statusName);

        var task = new WorkItem
        {
            Id = id,
            AgentId = agentId,
            Status = status,
            Persona = persona,
            Description = description,
            CreatedAt = _timeService.UtcNow,
            StartedAt = _timeService.UtcNow.AddMinutes(1),
            CompletedAt = _timeService.UtcNow.AddMinutes(5)
        };

        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return id;
    }
}
