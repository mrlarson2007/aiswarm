using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Database;
using AISwarm.DataLayer.Services;
using AISwarm.Server.McpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AgentLauncher.Tests.McpTools;

public class CreateTaskMcpToolTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDatabaseScopeService _scopeService;

    public CreateTaskMcpToolTests()
    {
        var services = new ServiceCollection();

        // Add database services
        services.AddDbContext<CoordinationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
        services.AddSingleton<ITimeService, TestTimeService>();
        services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();

        // Add MCP tools
        services.AddSingleton<ICreateTaskMcpTool, CreateTaskMcpTool>();

        _serviceProvider = services.BuildServiceProvider();
        _scopeService = _serviceProvider.GetRequiredService<IDatabaseScopeService>();
    }

    [Fact]
    public async Task WhenCreatingTask_ShouldSaveTaskToDatabase()
    {
        // Arrange
        var agentId = "agent-123";
        var persona = "You are a code reviewer. Review code for quality and security.";
        var description = "Review the authentication module for security vulnerabilities";
        var expectedCreatedAt = new DateTime(2025, 8, 21, 10, 0, 0, DateTimeKind.Utc);

        // Create a running agent first
        await CreateRunningAgentAsync(agentId);

        var createTaskTool = _serviceProvider.GetRequiredService<ICreateTaskMcpTool>();

        // Act
        var result = await createTaskTool.CreateTaskAsync(agentId, persona, description);

        // Assert
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldNotBeNull();

        using var scope = _scopeService.CreateReadScope();
        var tasks = scope.Tasks
        .Where(t => t.Id == result.TaskId)
        .Where(t => t.AgentId == agentId)
        .ToList();

        tasks.Count.ShouldBe(1);
        var task = tasks.First();
        task.AgentId.ShouldBe(agentId);
        task.Persona.ShouldBe(persona);
        task.Description.ShouldBe(description);
        task.Status.ShouldBe(AISwarm.DataLayer.Entities.TaskStatus.Pending);
        task.CreatedAt.ShouldBe(expectedCreatedAt);
    }

    [Fact]
    public async Task WhenCreatingTaskForNonExistentAgent_ShouldReturnFailureResult()
    {
        // Arrange
        var nonExistentAgentId = "non-existent-agent";
        var persona = "You are a code reviewer.";
        var description = "Review code";

        var createTaskTool = _serviceProvider.GetRequiredService<ICreateTaskMcpTool>();

        // Act
        var result = await createTaskTool.CreateTaskAsync(nonExistentAgentId, persona, description);

        // Assert
        result.Success.ShouldBeFalse();
        result.TaskId.ShouldBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Agent not found");
        result.ErrorMessage.ShouldContain(nonExistentAgentId);
    }

    [Fact]
    public async Task WhenCreatingTaskForStoppedAgent_ShouldReturnFailureResult()
    {
        // Arrange
        var agentId = "stopped-agent-123";
        var persona = "You are a code reviewer.";
        var description = "Review code";

        // Create a stopped agent first
        await CreateStoppedAgentAsync(agentId);

        var createTaskTool = _serviceProvider.GetRequiredService<ICreateTaskMcpTool>();

        // Act
        var result = await createTaskTool.CreateTaskAsync(agentId, persona, description);

        // Assert
        result.Success.ShouldBeFalse();
        result.TaskId.ShouldBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Agent is not running");
        result.ErrorMessage.ShouldContain(agentId);
    }


    private async Task CreateRunningAgentAsync(string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        var agent = new AISwarm.DataLayer.Entities.Agent
        {
            Id = agentId,
            PersonaId = "test-persona",
            AgentType = "test",
            WorkingDirectory = "/test",
            Status = AISwarm.DataLayer.Entities.AgentStatus.Running,
            LastHeartbeat = _serviceProvider.GetRequiredService<ITimeService>().UtcNow
        };
        scope.Agents.Add(agent);
        await scope.SaveChangesAsync();
        scope.Complete();
    }

    private async Task CreateStoppedAgentAsync(string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        var agent = new AISwarm.DataLayer.Entities.Agent
        {
            Id = agentId,
            PersonaId = "test-persona",
            AgentType = "test",
            WorkingDirectory = "/test",
            Status = AISwarm.DataLayer.Entities.AgentStatus.Stopped,
            LastHeartbeat = _serviceProvider.GetRequiredService<ITimeService>().UtcNow
        };
        scope.Agents.Add(agent);
        await scope.SaveChangesAsync();
        scope.Complete();
    }

    private class TestTimeService : ITimeService
    {
        public DateTime UtcNow => new DateTime(2025, 8, 21, 10, 0, 0, DateTimeKind.Utc);
    }
}
