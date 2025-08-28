using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Eventing;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace AISwarm.Tests.McpTools;

public class AgentManagementMcpToolTests
    : ISystemUnderTest<AgentManagementMcpTool>
{
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;
    private readonly TestLogger _logger;
    private readonly FakeContextService _fakeContextService;
    private readonly FakeGitService _fakeGitService;
    private readonly Mock<IInteractiveTerminalService> _fakeTerminalService;
    private readonly FakeFileSystemService _fakeFileSystemService;
    private readonly GeminiService _geminiService;
    private readonly LocalAgentService _localAgentService;
    private readonly TestEnvironmentService _testEnvironmentService;
    private AgentManagementMcpTool? _systemUnderTest;

    public AgentManagementMcpTool SystemUnderTest =>
        _systemUnderTest ??= new AgentManagementMcpTool(
            _scopeService,
            _fakeContextService,
            _fakeGitService,
            _geminiService,
            _localAgentService,
            _testEnvironmentService,
            _logger);

    protected AgentManagementMcpToolTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _timeService = new FakeTimeService();
        _scopeService = new DatabaseScopeService(new TestDbContextFactory(options));
        _logger = new TestLogger();
        _fakeContextService = new FakeContextService();
        var mockNotificationService = new Mock<IAgentNotificationService>();
        var mockProcessTerminationService = new Mock<IProcessTerminationService>();
        IAgentStateService agentStateService = new AgentStateService(
            _scopeService,
            mockNotificationService.Object,
            mockProcessTerminationService.Object);
        _fakeGitService = new FakeGitService();
        _fakeTerminalService = new Mock<IInteractiveTerminalService>();
        _fakeFileSystemService = new FakeFileSystemService();
        _geminiService = new GeminiService(
            _fakeTerminalService.Object,
            _logger,
            _fakeFileSystemService);

        _localAgentService = new LocalAgentService(
            _timeService,
            _scopeService,
            agentStateService);

        _testEnvironmentService = new TestEnvironmentService();
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

        [Fact]
        public async Task WhenValidPersona_ShouldReturnAgents()
        {
            // Arrange
            await CreateAgentAsync("agent1", "implementer", AgentStatus.Running);
            await CreateAgentAsync("agent2", "reviewer", AgentStatus.Running);
            var existingPersona = "implementer";

            // Act
            var result = await SystemUnderTest.ListAgentsAsync(existingPersona);

            // Assert
            result.Success.ShouldBeTrue();
            result.Agents.ShouldNotBeNull();
            result.Agents.ShouldHaveSingleItem();
            var foundAgent = result.Agents.First();
            foundAgent.AgentId.ShouldBe("agent1");
            foundAgent.Status.ShouldBe("Running");
            foundAgent.PersonaId.ShouldBe("implementer");
        }

        [Fact]
        public async Task WhenNoPersonaFilter_ShouldReturnAllAgents()
        {
            // Arrange
            await CreateAgentAsync("agent1", "implementer", AgentStatus.Running);
            await CreateAgentAsync("agent2", "reviewer", AgentStatus.Running);

            // Act
            var result = await SystemUnderTest.ListAgentsAsync();

            // Assert
            result.Success.ShouldBeTrue();
            result.Agents.ShouldNotBeNull();
            result.Agents.Length.ShouldBe(2);
            result.Agents[0].AgentId.ShouldBe("agent1");
            result.Agents[0].Status.ShouldBe("Running");
            result.Agents[0].PersonaId.ShouldBe("implementer");
            result.Agents[1].AgentId.ShouldBe("agent2");
            result.Agents[1].Status.ShouldBe("Running");
            result.Agents[1].PersonaId.ShouldBe("reviewer");
        }

        [Fact]
        public async Task WhenNoAgentsExist_ShouldReturnEmptyArray()
        {
            // Arrange - No agents created

            // Act
            var result = await SystemUnderTest.ListAgentsAsync();

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

            _fakeTerminalService.Setup(t => t.LaunchTerminalInteractive(
                It.IsAny<string>(),
                It.IsAny<string>()))
                .Returns(true);

            _fakeTerminalService.Setup(t => t.LaunchTerminalInteractive(
                It.Is<string>(x => x.Contains("gemini")),
                It.IsAny<string>()))
                .Returns(true);

            _fakeFileSystemService.AddFile(
                "/test/repo/test-branch/implementer_context.md");

            // Act
            var result = await SystemUnderTest.LaunchAgentAsync(persona, description, model, worktreeName);

            // Assert
            result.Success.ShouldBeTrue();
            result.AgentId.ShouldNotBeNull();
            result.ProcessId.ShouldBeNull();
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
            _fakeTerminalService.Setup(t => t.LaunchTerminalInteractive(
                It.Is<string>(x => x.Contains("--yolo")),
                It.IsAny<string>()))
                .Returns(true);

            _fakeFileSystemService.AddFile(
                "/test/repo/test-branch/implementer_context.md");

            // Act
            var result = await SystemUnderTest.LaunchAgentAsync(
                "implementer",
                "Test task",
                null,
                null,
                true);

            // Assert
            result.Success.ShouldBeTrue();
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

        [Fact]
        public async Task WhenAgentFound_ShouldTerminateAgent()
        {
            // Arrange
            var existingAgentId = "existing-agent";
            await CreateAgentAsync(
                existingAgentId,
                "implementer",
                AgentStatus.Running,
                processId: "12345");

            // Act
            var result = await SystemUnderTest.KillAgentAsync(existingAgentId);

            // Assert
            result.Success.ShouldBeTrue();
            result.ErrorMessage.ShouldBeNull();

            var listResult = await SystemUnderTest.ListAgentsAsync();
            listResult.ShouldNotBeNull();
            listResult.Agents.ShouldNotBeNull();
            listResult.Agents.Length.ShouldBe(1);
            listResult.Agents[0].AgentId.ShouldBe(existingAgentId);
            listResult.Agents[0].Status.ShouldBe("Killed");
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
