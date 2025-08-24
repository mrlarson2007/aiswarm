using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;

namespace AISwarm.Tests.McpTools;

public class GetTasksByStatusMcpToolTests
{
    private readonly FakeTimeService _timeService = new();
    private readonly IDatabaseScopeService _scopeService;

    public GetTasksByStatusMcpToolTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new CoordinationDbContext(options);
        _scopeService = new DatabaseScopeService(context);
    }

    [Fact]
    public async Task GetTasksByStatusAsync_WhenInvalidStatus_ShouldReturnFailure()
    {
        // Arrange
        var tool = new GetTasksByStatusMcpTool(_scopeService);
        var invalidStatus = "InvalidStatus";

        // Act
        var result = await tool.GetTasksByStatusAsync(invalidStatus);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Invalid status");
        result.Tasks.ShouldBeNull();
    }

    [Fact]
    public async Task GetTasksByStatusAsync_WhenNoTasksWithStatus_ShouldReturnEmptyArray()
    {
        // Arrange
        var tool = new GetTasksByStatusMcpTool(_scopeService);
        var validStatus = "Pending";

        // Act
        var result = await tool.GetTasksByStatusAsync(validStatus);

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
        var task1 = await CreateTaskAsync("task1", TaskStatus.Pending, "agent1");
        var task2 = await CreateTaskAsync("task2", TaskStatus.InProgress, "agent2");
        var task3 = await CreateTaskAsync("task3", TaskStatus.Completed, "agent1");
        var task4 = await CreateTaskAsync("task4", TaskStatus.Failed, "agent3");
        var task5 = await CreateTaskAsync("task5", Enum.Parse<TaskStatus>(statusFilter), "agent4");

        var tool = new GetTasksByStatusMcpTool(_scopeService);

        // Act
        var result = await tool.GetTasksByStatusAsync(statusFilter);

        // Assert
        result.Success.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
        result.Tasks.ShouldNotBeNull();
        
        // Should return tasks that match the filter (task5 plus any others with same status)
        var expectedTasks = result.Tasks.Where(t => t.Status == statusFilter);
        expectedTasks.ShouldNotBeEmpty();
        
        // Verify all returned tasks have the correct status
        result.Tasks.ShouldAllBe(t => t.Status == statusFilter);
        
        // Verify task5 is included
        var task5Result = result.Tasks.Single(t => t.TaskId == "task5");
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
        var task = await CreateTaskAsync("timestampTask", taskStatus, "agent1");

        var tool = new GetTasksByStatusMcpTool(_scopeService);

        // Act
        var result = await tool.GetTasksByStatusAsync(statusFilter);

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
}