using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AISwarm.Tests.McpTools;

public class AgentManagementMcpToolTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;

    public AgentManagementMcpToolTests()
    {
        var services = new ServiceCollection();

        services.AddDbContext<CoordinationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
        _timeService = new FakeTimeService();
        services.AddSingleton<ITimeService>(_timeService);
        services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();
        services.AddSingleton<AgentManagementMcpTool>();

        _serviceProvider = services.BuildServiceProvider();
        _scopeService = _serviceProvider.GetRequiredService<IDatabaseScopeService>();
    }

    private AgentManagementMcpTool SystemUnderTest => _serviceProvider.GetRequiredService<AgentManagementMcpTool>();

    // ListAgents Tests
    [Fact]
    public async Task ListAgentsAsync_WhenNonExistentPersonaFilter_ShouldReturnEmptyArray()
    {
        // Arrange
        await CreateAgentAsync("agent1", "implementer", AgentStatus.Running);
        await CreateAgentAsync("agent2", "reviewer", AgentStatus.Running);
        var nonExistentPersona = "nonexistent-persona";

        // Act
        var result = await SystemUnderTest.ListAgentsAsync(nonExistentPersona);

        // Assert
        result.Success.ShouldBeTrue();
        result.Agents.ShouldNotBeNull();
        result.Agents.ShouldBeEmpty();
    }

    private async Task<Agent> CreateAgentAsync(
        string agentId,
        string personaId,
        AgentStatus status,
        string? processId = null)
    {
        using var scope = _scopeService.CreateWriteScope();
        var agent = new Agent
        {
            Id = agentId,
            PersonaId = personaId,
            AgentType = personaId,
            Status = status,
            RegisteredAt = _timeService.UtcNow,
            LastHeartbeat = _timeService.UtcNow,
            StartedAt = _timeService.UtcNow,
            ProcessId = processId ?? "12345",
            WorkingDirectory = "/test/directory"
        };

        scope.Agents.Add(agent);
        await scope.SaveChangesAsync();
        scope.Complete();
        return agent;
    }
}