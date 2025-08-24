using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;

namespace AISwarm.Tests.McpTools;

public class GetTaskMcpToolTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;

    public GetTaskMcpToolTests()
    {
        var services = new ServiceCollection();

        services.AddDbContext<CoordinationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
        _timeService = new FakeTimeService();
        services.AddSingleton<ITimeService>(_timeService);
        services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();
        services.AddSingleton<GetTaskMcpTool>();

        _serviceProvider = services.BuildServiceProvider();
        _scopeService = _serviceProvider.GetRequiredService<IDatabaseScopeService>();
    }

    private GetTaskMcpTool SystemUnderTest => _serviceProvider.GetRequiredService<GetTaskMcpTool>();

    // GetTasksByStatus Tests
    [Fact]
    public async Task GetTasksByStatusAsync_WhenInvalidStatus_ShouldReturnFailure()
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
    public async Task GetTasksByStatusAsync_WhenNoTasksWithStatus_ShouldReturnEmptyArray()
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
    public async Task GetTasksByStatusAsync_WhenTasksExistWithStatus_ShouldReturnFilteredTasks(string statusFilter)
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
    public async Task GetTasksByStatusAsync_WhenTasksHaveTimestamps_ShouldReturnTimestampFields(string statusFilter)
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

    // GetTaskStatus Tests
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("non-existent-task-id")]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")] // Random GUID that doesn't exist
    public async Task GetTaskStatusAsync_WhenTaskIdIsInvalidOrNotFound_ShouldReturnNullValues(
        string taskId)
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
    public async Task GetTaskStatusAsync_WhenTaskHasAgentId_ShouldReturnAgentIdForAllStatuses(string statusName)
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
    public async Task GetTaskStatusAsync_WhenTaskHasEmptyAgentId_ShouldReturnEmptyAgentIdForAllStatuses(string statusName)
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

    // GetTasksByAgentId Tests
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetTasksByAgentIdAsync_WhenAgentIdIsInvalidOrEmpty_ShouldReturnFailure(string? agentId)
    {
        // Act
        var result = await SystemUnderTest.GetTasksByAgentIdAsync(agentId!);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Agent ID cannot be null or empty");
        result.Tasks.ShouldBeNull();
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
            StartedAt = status == TaskStatus.InProgress ? _timeService.UtcNow : null,
            CompletedAt = status == TaskStatus.Completed ? _timeService.UtcNow : null
        };

        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
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
