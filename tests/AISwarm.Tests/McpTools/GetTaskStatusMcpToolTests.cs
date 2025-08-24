using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AISwarm.Tests.McpTools;

public class GetTaskStatusMcpToolTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;

    public GetTaskStatusMcpToolTests()
    {
        var services = new ServiceCollection();

        services.AddDbContext<CoordinationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
        _timeService = new FakeTimeService();
        services.AddSingleton<ITimeService>(_timeService);
        services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();

        services.AddSingleton<GetTaskStatusMcpTool>();

        _serviceProvider = services.BuildServiceProvider();
        _scopeService = _serviceProvider.GetRequiredService<IDatabaseScopeService>();
    }

    private GetTaskStatusMcpTool SystemUnderTest => _serviceProvider.GetRequiredService<GetTaskStatusMcpTool>();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("non-existent-task-id")]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")] // Random GUID that doesn't exist
    public async Task WhenTaskIdIsInvalidOrNotFound_ShouldReturnNoPendingTasks(
        string taskId)
    {
        // Act
        var result = await SystemUnderTest.GetTaskStatusAsync(
            taskId);

        // Assert
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldBeNull();
        result.AgentId.ShouldBeNull();
        result.StartedAt.ShouldBeNull();
        result.CompletedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("InProgress")]
    [InlineData("Completed")]
    [InlineData("Failed")]
    public async Task WhenTaskExistsWithStatus_ShouldReturnTaskDetails(
        string statusName)
    {
        // Arrange
        var taskId = await CreateTaskWithStatusAsync(
            statusName,
            "Test persona", "Test description");

        // Act
        var result = await SystemUnderTest.GetTaskStatusAsync(
            taskId);

        // Assert
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldBe(taskId);
        result.Status.ShouldBe(statusName);
        result.AgentId.ShouldBeEmpty();
        result.StartedAt.ShouldBe(_timeService.UtcNow.AddMinutes(1));
        result.CompletedAt.ShouldBe(_timeService.UtcNow.AddMinutes(5));
    }

    private async Task<string> CreateTaskWithStatusAsync(
        string statusName,
        string persona,
        string description)
    {
        using var scope = _scopeService.CreateWriteScope();
        var id = Guid.NewGuid().ToString();
        var status = Enum.Parse<DataLayer.Entities.TaskStatus>(
            statusName);

        var task = new WorkItem
        {
            Id = id,
            AgentId = string.Empty,
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
