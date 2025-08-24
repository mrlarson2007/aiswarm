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

    [Fact]
    public async Task WhenTaskDoesNotExists_ShouldReturnNoPendingTasks()
    {
        // Arrange
        var taskId = Guid.NewGuid().ToString();

        // Act
        var result = await SystemUnderTest.GetTaskStatusAsync(taskId);

        // Assert
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldBeNull();
        result.AgentId.ShouldBeNull();
        result.StartedAt.ShouldBeNull();
        result.CompletedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("non-existent-task-id")]
    public async Task WhenTaskIdIsInvalidOrNotFound_ShouldReturnNoPendingTasks(string taskId)
    {
        // Act
        var result = await SystemUnderTest.GetTaskStatusAsync(taskId);

        // Assert
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldBeNull();
        result.AgentId.ShouldBeNull();
        result.StartedAt.ShouldBeNull();
        result.CompletedAt.ShouldBeNull();
    }

    private async Task<string> CreatePendingTaskAsync(string persona, string description)
    {
        using var scope = _scopeService.CreateWriteScope();
        var id = Guid.NewGuid().ToString();
        var task = new WorkItem
        {
            Id = id,
            AgentId = string.Empty,
            Status = AISwarm.DataLayer.Entities.TaskStatus.Pending,
            Persona = persona,
            Description = description,
            CreatedAt = _timeService.UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return id;
    }
}
