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

    [Fact]
    public async Task GetTasksByStatusAsync_WhenTasksExistWithStatus_ShouldReturnTasksArray()
    {
        // Arrange
        var task1 = await CreateTaskAsync("task1", TaskStatus.Pending, "agent1");
        var task2 = await CreateTaskAsync("task2", TaskStatus.Pending, "agent2");
        var task3 = await CreateTaskAsync("task3", TaskStatus.InProgress, "agent1");

        var tool = new GetTasksByStatusMcpTool(_scopeService);

        // Act
        var result = await tool.GetTasksByStatusAsync("Pending");

        // Assert
        result.Success.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
        result.Tasks.ShouldNotBeNull();
        result.Tasks.Length.ShouldBe(2);
        
        var returnedTask1 = result.Tasks.Single(t => t.TaskId == "task1");
        returnedTask1.Status.ShouldBe("Pending");
        returnedTask1.AgentId.ShouldBe("agent1");
        
        var returnedTask2 = result.Tasks.Single(t => t.TaskId == "task2");
        returnedTask2.Status.ShouldBe("Pending");
        returnedTask2.AgentId.ShouldBe("agent2");
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