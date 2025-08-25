using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AISwarm.Tests.McpTools;

public class AgentManagementMcpToolTests 
    : IDisposable, ISystemUnderTest<AgentManagementMcpTool>
{
    private readonly CoordinationDbContext _dbContext;
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;
    private readonly TestLogger _logger;
    private readonly FakeContextService _fakeContextService;
    private readonly FakeGitService _fakeGitService;
    private readonly FakeGeminiService _fakeGeminiService;
    private readonly FakeLocalAgentService _fakeLocalAgentService;
    private readonly TestEnvironmentService _testEnvironmentService;
    private AgentManagementMcpTool? _systemUnderTest;

    public AgentManagementMcpTool SystemUnderTest =>
        _systemUnderTest ??= new AgentManagementMcpTool(
            _scopeService,
            _fakeContextService,
            _fakeGitService,
            _fakeGeminiService,
            _fakeLocalAgentService,
            _testEnvironmentService,
            _logger);

    public AgentManagementMcpToolTests()
    {
        _timeService = new FakeTimeService();
        _logger = new TestLogger();
        _fakeContextService = new FakeContextService();
        _fakeGitService = new FakeGitService();
        _fakeGeminiService = new FakeGeminiService();
        _fakeLocalAgentService = new FakeLocalAgentService();
        _testEnvironmentService = new TestEnvironmentService();

        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new CoordinationDbContext(options);
        _scopeService = new DatabaseScopeService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    public class ListAgentsTests : AgentManagementMcpToolTests
    {
        [Fact]
        public async Task WhenNonExistentPersonaFilter_ShouldReturnEmptyArray()
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
    }

    public class LaunchAgentTests : AgentManagementMcpToolTests
    {
        [Fact]
        public async Task WhenInvalidPersona_ShouldReturnFailure()
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
        public async Task WhenInvalidAgentType_ShouldReturnFailure()
        {
            // Arrange
            var invalidPersona = "invalid-agent-type";
            var description = "Test task description";

            _fakeContextService.FailureMessage = string.Empty; // Reset any previous failure state

            // Act
            var result = await SystemUnderTest.LaunchAgentAsync(invalidPersona, description);

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("Invalid agent type");
            result.AgentId.ShouldBeNull();
        }

        [Fact]
        public async Task WhenNotInGitRepository_ShouldReturnFailure()
        {
            // Arrange
            var persona = "implementer";
            var description = "Test task description";

            _fakeGitService.IsRepository = false; // Not in a git repository

            // Act
            var result = await SystemUnderTest.LaunchAgentAsync(persona, description);

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("git repository");
            result.AgentId.ShouldBeNull();
        }

        [Fact]
        public async Task WhenValidParameters_ShouldCreateAgentAndLaunchGemini()
        {
            // Arrange
            var persona = "implementer";
            var description = "Test task description";
            var model = "gemini-1.5-flash";
            var worktreeName = "test-branch";

            _fakeGitService.IsRepository = true;
            _fakeGitService.RepositoryRoot = "/test/repo";
            _fakeGitService.CreatedWorktreePath = "/test/repo/test-branch";

            _fakeContextService.CreatedContextPath = "/test/repo/test-branch/implementer_context.md";

            _fakeLocalAgentService.RegisteredAgentId = "test-agent-123";

            _fakeGeminiService.LaunchResult = true;

            // Act
            var result = await SystemUnderTest.LaunchAgentAsync(persona, description, model, worktreeName);

            // Assert
            result.Success.ShouldBeTrue();
            result.AgentId
                .ShouldBe("test-agent-123"); // Should be the registered agent ID from fake service, not random GUID
            result.ProcessId.ShouldBeNull(); // Currently null since IGeminiService doesn't return process ID
            result.ErrorMessage.ShouldBeNull();
        }

        [Fact]
        public async Task WhenYoloIsTrue_ShouldPassYoloToGeminiService()
        {
            // Arrange - Setup for successful launch first
            _fakeGitService.IsRepository = true;
            _fakeGitService.RepositoryRoot = "/test/repo";
            _fakeGitService.CreatedWorktreePath = "/test/repo/test-branch";
            _fakeContextService.CreatedContextPath = "/test/repo/test-branch/implementer_context.md";
            _fakeLocalAgentService.RegisteredAgentId = "test-agent-123";
            _fakeGeminiService.LaunchResult = true;

            // Act
            var result = await SystemUnderTest.LaunchAgentAsync(
                "implementer",
                "Test task",
                null,
                null,
                true);

            // Assert
            result.Success.ShouldBeTrue();
            _fakeGeminiService.LastLaunchYoloParameter.ShouldNotBeNull();
            _fakeGeminiService.LastLaunchYoloParameter.Value.ShouldBeTrue();
        }
    }

    public class KillAgentTests : AgentManagementMcpToolTests
    {
        [Fact]
        public async Task WhenAgentNotFound_ShouldReturnFailure()
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
    }

    private async Task CreateAgentAsync(
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
    }
}
