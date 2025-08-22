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

    private class TestTimeService : ITimeService
    {
        public DateTime UtcNow => new DateTime(2025, 8, 21, 10, 0, 0, DateTimeKind.Utc);
    }
}