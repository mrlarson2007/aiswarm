using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Database;
using AISwarm.DataLayer.Services;
using AISwarm.Server.McpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace AgentLauncher.Tests.McpTools;

public class GetNextTaskMcpToolTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDatabaseScopeService _scopeService;

    public GetNextTaskMcpToolTests()
    {
        var services = new ServiceCollection();

        // Add database services
        services.AddDbContext<CoordinationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
        services.AddSingleton<ITimeService, TestTimeService>();
        services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();

        // Add MCP tools
        services.AddSingleton<GetNextTaskMcpTool>();

        _serviceProvider = services.BuildServiceProvider();
        _scopeService = _serviceProvider.GetRequiredService<IDatabaseScopeService>();
    }

    [Fact]
    public async Task WhenNonExistentAgent_ShouldReturnFailureResult()
    {
        // Arrange
        var nonExistentAgentId = "non-existent-agent";

        var getNextTaskTool = _serviceProvider.GetRequiredService<GetNextTaskMcpTool>();

        // Act
        var result = await getNextTaskTool.GetNextTaskAsync(nonExistentAgentId);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Agent not found");
        result.ErrorMessage.ShouldContain(nonExistentAgentId);
    }

    [Fact]
    public async Task WhenAgentHasNoTasks_ShouldReturnNoTasksWithReinforcingPrompt()
    {
        // Arrange
        var agentId = "agent-no-tasks";
        
        // Create a running agent with no tasks
        await CreateRunningAgentAsync(agentId);

        var getNextTaskTool = _serviceProvider.GetRequiredService<GetNextTaskMcpTool>();

        // Act
        var result = await getNextTaskTool.GetNextTaskAsync(agentId);

        // Assert
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldBeNull();
        result.Persona.ShouldBeNull();
        result.Description.ShouldBeNull();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("No tasks available");
        result.Message.ShouldContain("call this tool again");
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

    private class TestTimeService : ITimeService
    {
        public DateTime UtcNow => new DateTime(2025, 8, 21, 10, 0, 0, DateTimeKind.Utc);
    }
}