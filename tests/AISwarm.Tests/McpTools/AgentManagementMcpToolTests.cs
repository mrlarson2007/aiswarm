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
    private readonly TestLogger _logger;

    public AgentManagementMcpToolTests()
    {
        var services = new ServiceCollection();

        services.AddDbContext<CoordinationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
        _timeService = new FakeTimeService();
        _logger = new TestLogger();
        services.AddSingleton<ITimeService>(_timeService);
        services.AddSingleton<IAppLogger>(_logger);
        services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();
        
        // Add mock services for agent launching
        services.AddSingleton<IContextService, FakeContextService>();
        services.AddSingleton<IGitService, FakeGitService>();
        services.AddSingleton<IGeminiService, FakeGeminiService>();
        services.AddSingleton<IFileSystemService, FakeFileSystemService>();
        services.AddSingleton<ILocalAgentService, FakeLocalAgentService>();
        services.AddSingleton<IEnvironmentService, TestEnvironmentService>();
        
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

    // LaunchAgent Tests
    [Fact]
    public async Task LaunchAgentAsync_WhenInvalidPersona_ShouldReturnFailure()
    {
        // Arrange
        var invalidPersona = "";
        var description = "Test task description";

        // Act
        var result = await SystemUnderTest.LaunchAgentAsync(invalidPersona, description);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Persona is required");
        result.AgentId.ShouldBeNull();
    }

    [Fact]
    public async Task LaunchAgentAsync_WhenInvalidAgentType_ShouldReturnFailure()
    {
        // Arrange
        var invalidPersona = "invalid-agent-type";
        var description = "Test task description";
        
        var fakeContextService = _serviceProvider.GetRequiredService<IContextService>() as FakeContextService;
        fakeContextService!.FailureMessage = string.Empty; // Reset any previous failure state

        // Act
        var result = await SystemUnderTest.LaunchAgentAsync(invalidPersona, description);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Invalid agent type");
        result.AgentId.ShouldBeNull();
    }

    [Fact]
    public async Task LaunchAgentAsync_WhenNotInGitRepository_ShouldReturnFailure()
    {
        // Arrange
        var persona = "implementer";
        var description = "Test task description";
        
        var fakeGitService = _serviceProvider.GetRequiredService<IGitService>() as FakeGitService;
        fakeGitService!.IsRepository = false; // Not in a git repository

        // Act
        var result = await SystemUnderTest.LaunchAgentAsync(persona, description);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("git repository");
        result.AgentId.ShouldBeNull();
    }

    // KillAgent Tests
    [Fact]
    public async Task KillAgentAsync_WhenAgentNotFound_ShouldReturnFailure()
    {
        // Arrange
        var nonExistentAgentId = "non-existent-agent";

        // Act
        var result = await SystemUnderTest.KillAgentAsync(nonExistentAgentId);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Agent not found");
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