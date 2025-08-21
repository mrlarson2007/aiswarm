using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Database;
using AISwarm.DataLayer.Services;
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
        
        // TODO: Get reference to MCP tool once implemented
        // var createTaskTool = _serviceProvider.GetRequiredService<ICreateTaskMcpTool>();
        
        // Act
        // TODO: Call the MCP tool
        // await createTaskTool.ExecuteAsync(agentId, persona, description);
        
        // Assert
        using var scope = _scopeService.CreateReadScope();
        var tasks = scope.Tasks.Where(t => t.AgentId == agentId).ToList();
        
        tasks.Count.ShouldBe(1);
        var task = tasks.First();
        task.AgentId.ShouldBe(agentId);
        task.Persona.ShouldBe(persona);
        task.Description.ShouldBe(description);
        task.Status.ShouldBe(AISwarm.DataLayer.Entities.TaskStatus.Pending);
        task.CreatedAt.ShouldNotBe(default);
    }

    private class TestTimeService : ITimeService
    {
        public DateTime UtcNow => new DateTime(2025, 8, 21, 10, 0, 0, DateTimeKind.Utc);
    }
}