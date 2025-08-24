using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AISwarm.Tests.McpTools;

public class ReportTaskCompletionMcpToolTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDatabaseScopeService _scopeService;

    public ReportTaskCompletionMcpToolTests()
    {
        var services = new ServiceCollection();

        // Add database services
        services.AddDbContext<CoordinationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
        services.AddSingleton<ITimeService, FakeTimeService>();
        services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();

        // Add MCP tools
        services.AddSingleton<ReportTaskCompletionMcpTool>();

        _serviceProvider = services.BuildServiceProvider();
        _scopeService = _serviceProvider.GetRequiredService<IDatabaseScopeService>();
    }

    [Fact]
    public async Task WhenReportingTaskCompletion_ShouldUpdateTaskStatusToCompleted()
    {
        // Arrange
        var agentId = "test-agent-123";
        var taskId = "test-task-456";
        var completionResult = "Task completed successfully - implemented user authentication feature";

        await CreateRunningAgentAsync(agentId);
        await CreatePendingTaskAsync(taskId, agentId);

        var tool = _serviceProvider.GetRequiredService<ReportTaskCompletionMcpTool>();

        // Act
        var result = await tool.ReportTaskCompletionAsync(taskId, completionResult);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Message.ShouldContain("Task completed successfully");

        // Verify task was updated in database
        using var scope = _scopeService.CreateReadScope();
        var task = await scope.Tasks.FindAsync(taskId);
        task.ShouldNotBeNull();
        task.Status.ShouldBe(DataLayer.Entities.TaskStatus.Completed);
        task.Result.ShouldBe(completionResult);
        task.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task WhenReportingCompletionForNonExistentTask_ShouldReturnFailureResult()
    {
        // Arrange
        var taskId = "non-existent-task";
        var completionResult = "Some result";

        var tool = _serviceProvider.GetRequiredService<ReportTaskCompletionMcpTool>();

        // Act
        var result = await tool.ReportTaskCompletionAsync(taskId, completionResult);

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

        var tool = _serviceProvider.GetRequiredService<ReportTaskCompletionMcpTool>();

        // Act
        var result = await tool.ReportTaskCompletionAsync(taskId, completionResult);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Message.ShouldContain("already completed");
        result.Message.ShouldContain(taskId);
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
            LastHeartbeat = _serviceProvider.GetRequiredService<ITimeService>().UtcNow
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
            Status = DataLayer.Entities.TaskStatus.Pending,
            Persona = "Test persona content",
            Description = "Test task description",
            Priority = TaskPriority.Normal,
            CreatedAt = _serviceProvider.GetRequiredService<ITimeService>().UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
    }

    private async Task CreateCompletedTaskAsync(string taskId, string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        var timeService = _serviceProvider.GetRequiredService<ITimeService>();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = DataLayer.Entities.TaskStatus.Completed,
            Persona = "Test persona content",
            Description = "Test task description",
            Priority = TaskPriority.Normal,
            CreatedAt = timeService.UtcNow,
            CompletedAt = timeService.UtcNow,
            Result = "Previously completed"
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
    }
}
