using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AISwarm.Tests.McpTools;

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
        services.AddSingleton<ITimeService, FakeTimeService>();
        services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();

        // Add MCP tools
        services.AddSingleton<CreateTaskMcpTool>();

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

        var createTaskTool = _serviceProvider.GetRequiredService<CreateTaskMcpTool>();

        // Act
        var result = await createTaskTool.CreateTaskAsync(agentId, persona, description);

        // Assert
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldNotBeNull();

        using var scope = _scopeService.CreateReadScope();
        var tasks = Queryable.Where(scope.Tasks, t => t.Id == result.TaskId)
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

        var createTaskTool = _serviceProvider.GetRequiredService<CreateTaskMcpTool>();

        // Act
        var result = await createTaskTool.CreateTaskAsync(nonExistentAgentId, persona, description);

        // Assert
        ShouldBeBooleanExtensions.ShouldBeFalse(result.Success);
        ShouldBeNullExtensions.ShouldBeNull<string>(result.TaskId);
        ShouldBeNullExtensions.ShouldNotBeNull<string>(result.ErrorMessage);
        ShouldBeStringTestExtensions.ShouldContain(result.ErrorMessage, "Agent not found");
        ShouldBeStringTestExtensions.ShouldContain(result.ErrorMessage, nonExistentAgentId);
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

        var createTaskTool = _serviceProvider.GetRequiredService<CreateTaskMcpTool>();

        // Act
        var result = await createTaskTool.CreateTaskAsync(agentId, persona, description);

        // Assert
        ShouldBeBooleanExtensions.ShouldBeFalse(result.Success);
        ShouldBeNullExtensions.ShouldBeNull<string>(result.TaskId);
        ShouldBeNullExtensions.ShouldNotBeNull<string>(result.ErrorMessage);
        ShouldBeStringTestExtensions.ShouldContain(result.ErrorMessage, "Agent is not running");
        ShouldBeStringTestExtensions.ShouldContain(result.ErrorMessage, agentId);
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
}
