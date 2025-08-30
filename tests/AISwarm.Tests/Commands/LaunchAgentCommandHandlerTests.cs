using AgentLauncher.Commands;
using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace AISwarm.Tests.Commands;

public class LaunchAgentCommandHandlerTests
    : ISystemUnderTest<LaunchAgentCommandHandler>
{
    private const string ExpectedPromptFormatString =
        "I've just created \"{0}\". Please read it for your instructions.";

    private readonly Mock<IContextService> _context;
    private readonly TestEnvironmentService _env;
    private readonly FakeFileSystemService _fs;
    private readonly IGeminiService _gemini;
    private readonly GitService _git;
    private readonly ILocalAgentService _localAgentService;
    private readonly TestLogger _logger;
    private readonly Mock<IAgentStateService> _mockAgentStateService;
    private readonly PassThroughProcessLauncher _process;
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;
    private LaunchAgentCommandHandler? _systemUnderTest;

    public LaunchAgentCommandHandlerTests()
    {
        _context = new Mock<IContextService>();
        _process = new PassThroughProcessLauncher();
        _fs = new FakeFileSystemService();
        _logger = new TestLogger();
        _env = new TestEnvironmentService { CurrentDirectory = "/repo" };
        _timeService = new FakeTimeService();

        // Set up database with in-memory provider for testing
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var scopeService = new DatabaseScopeService(new TestDbContextFactory(options));
        _scopeService = scopeService;

        _git = new GitService(_process, _fs, _logger);

        // Create a real GeminiService using PassThroughProcessLauncher via terminal service
        var terminalService = new WindowsTerminalService(_process);
        _gemini = new GeminiService(terminalService, _logger, _fs);

        _mockAgentStateService = new Mock<IAgentStateService>();
        // Create real LocalAgentService with test doubles for dependencies
        _localAgentService = new LocalAgentService(
            _timeService,
            scopeService,
            _mockAgentStateService.Object);
    }

    public LaunchAgentCommandHandler SystemUnderTest =>
        _systemUnderTest ??= new LaunchAgentCommandHandler(
            _context.Object,
            _logger,
            _env,
            _git,
            _gemini,
            _fs,
            _localAgentService);

    public class DryRunTests : LaunchAgentCommandHandlerTests
    {
        [Fact]
        public async Task WhenDryRun_ShouldNotCreateContextOrLaunch()
        {
            // Act
            var result = await SystemUnderTest.RunAsync(
                "planner",
                null,
                null,
                null,
                true);
            result.ShouldBeTrue();

            // Assert
            _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _logger.Infos.ShouldContain(s => s.Contains("Dry run mode"));

            // Should not have launched any Gemini processes
            _process.Invocations.ShouldNotContain(i => i.File.Contains("pwsh") && i.Arguments.Contains("gemini"));
        }

        [Fact]
        public async Task WhenDryRunAndNoWorkTree_ShouldLogPlannedLaunchDetailsWithoutWorktree()
        {
            // Act (no worktree)
            var result = await SystemUnderTest.RunAsync(
                "planner",
                "gemini-1.5-flash",
                null,
                "/custom",
                true);
            result.ShouldBeTrue();

            // Assert
            _logger.Infos.ShouldContain(s => s.Contains("Dry run mode"));
            _logger.Infos.ShouldContain(s => s.Contains("Agent: planner"));
            _logger.Infos.ShouldContain(s => s.Contains("Model: gemini-1.5-flash"));
            _logger.Infos.ShouldContain(s => s.Contains("Workspace: Current branch"));
            _logger.Infos.ShouldContain(s => s.Contains("Working directory: /custom"));
            var planned = $"Planned context file: {Path.Join("/custom", "planner_context.md")}";
            _logger.Infos.ShouldContain(s => s.Contains(planned));
            _logger.Infos.ShouldContain(s => s.Contains("Manual launch:"));
            _logger.Infos.ShouldNotContain(s => s.Contains("Worktree (planned):"));
        }

        [Fact]
        public async Task WhenDryRunAndWithWorktree_ShouldLogPlannedWorktreeAndDetails()
        {
            // Act (with worktree; directory default current)
            var result = await SystemUnderTest.RunAsync(
                "planner",
                "gemini-1.5-flash",
                "feature_x",
                null,
                true);
            result.ShouldBeTrue();

            // Assert
            _logger.Infos.ShouldContain(s => s.Contains("Dry run mode"));
            _logger.Infos.ShouldContain(s => s.Contains("Agent: planner"));
            _logger.Infos.ShouldContain(s => s.Contains("Worktree (planned): feature_x"));
            _logger.Infos.ShouldContain(s => s.Contains("Manual launch:"));
        }

        [Fact]
        public async Task WhenDryRunWithInvalidWorktree_ShouldStillReportPlannedInvalidName()
        {
            var result = await SystemUnderTest.RunAsync(
                "planner",
                null,
                "bad?name",
                null,
                true);
            result.ShouldBeTrue();

            _logger.Infos.ShouldContain(i => i.Contains("Worktree (planned): bad?name"));
            _logger.Errors.ShouldBeEmpty();
        }

        [Fact]
        public async Task WhenDryRunWithModel_ShouldIncludeModelInManualCommand()
        {
            var result = await SystemUnderTest.RunAsync(
                "planner",
                "gemini-2.0-ultra",
                null,
                null,
                true);
            result.ShouldBeTrue();

            _logger.Infos.ShouldContain(i => i.Contains("-m gemini-2.0-ultra"));
        }
    }

    public class WorktreeTests : LaunchAgentCommandHandlerTests
    {
        [Fact]
        public async Task WhenWorktreeSpecified_ShouldCreateWorktree_ThenContext_InNewDirectory()
        {
            // Arrange expected git command sequence
            _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
                new ProcessResult(true, ".git", string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"),
                new ProcessResult(true, "/repo", string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("worktree list"),
                new ProcessResult(true, string.Empty, string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("worktree add"),
                new ProcessResult(true, "Created", string.Empty, 0));
            _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
                .ReturnsAsync("/repo-feature_x/planner_context.md")
                .Callback(() => _fs.AddFile("/repo-feature_x/planner_context.md"));

            // Act (non-dry-run)
            var result = await SystemUnderTest.RunAsync(
                "planner",
                null,
                "feature_x",
                null,
                false);
            result.ShouldBeTrue();

            // Assert (desired future behavior) - these will fail until handler updated
            _context.Verify(c => c.CreateContextFile("planner", It.Is<string>(p => p.Contains("feature_x"))),
                Times.Once);
        }

        [Fact]
        public async Task WhenWorktreeInvalid_ShouldLogErrorAndAbort()
        {
            // Act
            var result = await SystemUnderTest.RunAsync(
                "planner",
                null,
                "bad?name",
                null,
                false);
            result.ShouldBeFalse();

            // Assert
            _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _logger.Errors.ShouldContain(e => e.Contains("Invalid worktree name"));
        }

        [Fact]
        public async Task WhenWorktreeAddFails_ShouldLogErrorAndAbort()
        {
            _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
                new ProcessResult(true, ".git", string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"),
                new ProcessResult(true, "/repo", string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("worktree list"),
                new ProcessResult(true, string.Empty, string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("worktree add"),
                new ProcessResult(false, string.Empty, "fatal: permission denied", 1));

            var result = await SystemUnderTest.RunAsync(
                "planner",
                null,
                "feat_fail",
                null,
                false);
            result.ShouldBeFalse();

            _logger.Errors.ShouldContain(e => e.Contains("Failed to create worktree"));
            _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            // Should not have launched any Gemini processes since worktree creation failed
            _process.Invocations.ShouldNotContain(i => i.File == "pwsh.exe" && i.Arguments.Contains("gemini"));
        }

        [Fact]
        public async Task WhenWorktreeAlreadyExists_ShouldSucceed()
        {
            var porcelain = "worktree /repo/worktrees/feature_dup\nbranch refs/heads/feature_dup\n";
            _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
                new ProcessResult(true, ".git", string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"),
                new ProcessResult(true, "/repo", string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("worktree list"),
                new ProcessResult(true, porcelain, string.Empty, 0));
            _context.Setup(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("/repo/worktrees/feature_dup/planner_context.md");
            _fs.AddFile("/repo/worktrees/feature_dup/planner_context.md");

            var result = await SystemUnderTest.RunAsync(
                "planner",
                null,
                "feature_dup",
                null,
                false);
            result.ShouldBeTrue();

            _logger.Errors.ShouldNotContain(e => e.Contains("already exists"));
            _context.Verify(c => c.CreateContextFile("planner", "/repo/worktrees/feature_dup"), Times.Once);
        }

        [Fact]
        public async Task WhenWorktreeCreated_ShouldLaunchGeminiFromWorktree()
        {
            _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
                new ProcessResult(true, ".git", string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"),
                new ProcessResult(true, "/repo", string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("worktree list"),
                new ProcessResult(true, string.Empty, string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("worktree add"),
                new ProcessResult(true, "Created", string.Empty, 0));
            _context.Setup(c => c.CreateContextFile("planner", It.Is<string>(p => p.Contains("repo-feature_launch"))))
                .ReturnsAsync("/repo-feature_launch/planner_context.md")
                .Callback(() =>
                {
                    // Set up context file to exist after it's "created" by the context service
                    _fs.AddFile("/repo-feature_launch/planner_context.md");
                });

            var result = await SystemUnderTest.RunAsync(
                "planner",
                "gemini-1.5-pro",
                "feature_launch",
                null,
                false);

            result.ShouldBeTrue();

            // Assert - Check that Gemini was launched with correct arguments from worktree directory
            _process.Invocations.ShouldContain(i =>
                i.File == "pwsh.exe" &&
                i.Arguments.Contains("gemini") &&
                i.Arguments.Contains("-m \"gemini-1.5-pro\"") &&
                i.Arguments.Contains(string.Format(ExpectedPromptFormatString,
                    "/repo-feature_launch/planner_context.md")));
        }

        [Fact]
        public async Task WhenDirectoryAndWorktreeProvided_WorktreePathShouldOverride()
        {
            _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
                new ProcessResult(true, ".git", string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"),
                new ProcessResult(true, "/repo", string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("worktree list"),
                new ProcessResult(true, string.Empty, string.Empty, 0));
            _process.Enqueue("git", a => a.StartsWith("worktree add"),
                new ProcessResult(true, "Created", string.Empty, 0));
            _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
                .ReturnsAsync("/repo-feature_override/planner_context.md")
                .Callback(() => _fs.AddFile("/repo-feature_override/planner_context.md"));

            var result = await SystemUnderTest.RunAsync(
                "planner",
                null,
                "feature_override",
                "/some/other/dir",
                false);
            result.ShouldBeTrue();

            _context.Verify(
                c => c.CreateContextFile("planner", It.Is<string>(p => p.Contains("repo-feature_override"))),
                Times.Once);
        }

        [Fact]
        public async Task WhenWorktreeWhitespace_ShouldIgnoreAndProceedWithoutWorktree()
        {
            _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
                .ReturnsAsync("/repo/planner_context.md");

            // Set up context file to exist (Gemini service checks this)
            _fs.AddFile("/repo/planner_context.md");

            var result = await SystemUnderTest.RunAsync(
                "planner",
                null,
                "  \t  ",
                null,
                false);
            result.ShouldBeTrue();

            _process.Invocations.ShouldNotContain(i => i.Arguments.StartsWith("worktree add"));

            // Assert - Check that Gemini was launched without worktree (normal case)
            _process.Invocations.ShouldContain(i =>
                    i.File == "pwsh.exe" &&
                    i.Arguments.Contains("gemini") &&
                    i.Arguments.Contains(string.Format(ExpectedPromptFormatString, "/repo/planner_context.md")) &&
                    !i.Arguments.Contains("-m ") // No model specified
            );
        }
    }

    public class StandardLaunchTests : LaunchAgentCommandHandlerTests
    {
        [Fact]
        public async Task WhenNonDryRunWithoutWorktree_ShouldLaunchGeminiWithNullModel()
        {
            // Arrange context creation
            _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
                .ReturnsAsync("/repo/planner_context.md");

            // Set up context file to exist (Gemini service checks this)
            _fs.AddFile("/repo/planner_context.md");

            // Act
            var result = await SystemUnderTest.RunAsync(
                "planner",
                null,
                null,
                null,
                false);
            result.ShouldBeTrue();

            // Assert - Check that Gemini was launched with correct arguments
            _process.Invocations.ShouldContain(i =>
                    i.File == "pwsh.exe" &&
                    i.Arguments.Contains("gemini") &&
                    i.Arguments.Contains(string.Format(ExpectedPromptFormatString, "/repo/planner_context.md")) &&
                    !i.Arguments.Contains("-m ") // No model specified
            );
        }

        [Fact]
        public async Task WhenNonDryRunWithModel_ShouldLaunchGeminiWithModel()
        {
            // Arrange context creation
            _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
                .ReturnsAsync("/repo/planner_context.md");

            // Set up context file to exist (Gemini service checks this)
            _fs.AddFile("/repo/planner_context.md");

            // Act
            var result = await SystemUnderTest.RunAsync(
                "planner",
                "gemini-1.5-pro",
                null,
                null,
                false);
            result.ShouldBeTrue();

            // Assert - Check that Gemini was launched with correct model arguments
            _process.Invocations.ShouldContain(i =>
                i.File == "pwsh.exe" &&
                i.Arguments.Contains("gemini") &&
                i.Arguments.Contains("-m \"gemini-1.5-pro\"") &&
                i.Arguments.Contains(string.Format(ExpectedPromptFormatString, "/repo/planner_context.md"))
            );
        }

        [Fact]
        public async Task WhenDirectorySpecifiedWithoutWorktree_ShouldUseDirectoryForContext()
        {
            _context.Setup(c => c.CreateContextFile("planner", "/customdir"))
                .ReturnsAsync("/customdir/planner_context.md")
                .Callback(() => _fs.AddFile("/customdir/planner_context.md"));

            var result = await SystemUnderTest.RunAsync(
                "planner",
                null,
                null,
                "/customdir",
                false);
            result.ShouldBeTrue();

            _context.Verify(c => c.CreateContextFile("planner", "/customdir"), Times.Once);
        }

        [Fact]
        public async Task WhenGeminiLaunchThrows_ShouldSurfaceError()
        {
            _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
                .ReturnsAsync("/repo/planner_context.md");

            var result = await SystemUnderTest.RunAsync(
                "planner",
                null,
                null,
                null,
                false);

            // Should fail because context file doesn't exist
            result.ShouldBeFalse();
            _logger.Errors.ShouldContain(e => e.Contains("Context file not found"));
        }
    }

    public class GitRepoTests : LaunchAgentCommandHandlerTests
    {
        [Fact]
        public async Task WhenNotInGitRepo_ShouldLogErrorAndAbort()
        {
            _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
                new ProcessResult(false, string.Empty, "not a repo", 128));

            var result = await SystemUnderTest.RunAsync(
                "planner",
                null,
                "feature_new",
                null,
                false);
            result.ShouldBeFalse();

            _logger.Errors.ShouldContain(e => e.Contains("Not in a git repository") || e.Contains("not a git"));
            _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }

    public class MonitoringTests : LaunchAgentCommandHandlerTests
    {
        [Fact]
        public async Task WhenMonitorEnabled_ShouldRegisterAgentInDatabase()
        {
            // Arrange
            _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
                .ReturnsAsync("/repo/planner_context.md");

            // Set up context file to exist (Gemini service checks this)
            _fs.AddFile("/repo/planner_context.md");

            // Act
            var result = await SystemUnderTest.RunAsync(
                "planner",
                "gemini-1.5-pro",
                null,
                "/repo",
                false,
                true);

            // Assert
            result.ShouldBeTrue();

            // Verify agent was actually registered in database
            using var scope = _scopeService.GetReadScope();
            var agents = await scope.Agents.ToListAsync();
            agents.ShouldHaveSingleItem();
            var agent = agents.First();
            agent.PersonaId.ShouldBe("planner");
            agent.WorkingDirectory.ShouldBe("/repo");
            agent.Model.ShouldBe("gemini-1.5-pro");
            agent.Status.ShouldBe(AgentStatus.Starting);
            agent.RegisteredAt.ShouldBe(_timeService.UtcNow);

            _logger.Infos.ShouldContain(i => i.Contains("Registered agent") && i.Contains(agent.Id));
        }

        [Fact]
        public async Task WhenMonitorEnabledAndAgentRegistered_ShouldConfigureGeminiWithSettingsFile()
        {
            // Arrange
            _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
                .ReturnsAsync("/repo/planner_context.md");

            // Set up context file to exist (Gemini service checks this)
            _fs.AddFile("/repo/planner_context.md");

            // Act
            var result = await SystemUnderTest.RunAsync(
                "planner",
                "gemini-1.5-pro",
                null,
                "/repo",
                false,
                true);

            // Assert
            result.ShouldBeTrue();

            // Get the registered agent from database
            using var scope2 = _scopeService.GetReadScope();
            var agents = await scope2.Agents.ToListAsync();
            agents.ShouldHaveSingleItem();
            var agent = agents.First();

            // Should have launched Gemini with agent settings
            _process.Invocations.ShouldContain(i =>
                i.File == "pwsh.exe" &&
                i.Arguments.Contains("gemini") &&
                i.Arguments.Contains("-m \"gemini-1.5-pro\"") &&
                i.Arguments.Contains(string.Format(ExpectedPromptFormatString, "/repo/planner_context.md"))
            );

            // Should have created Gemini configuration file
            var configContent = _fs.GetFileContent("/repo/.gemini/settings.json");
            configContent.ShouldNotBeNull();
            configContent.ShouldContain(agent.Id);
            configContent.ShouldContain("aiswarm");

            _logger.Infos.ShouldContain(i => i.Contains("Configuring Gemini with agent settings"));
        }

        [Fact]
        public async Task WhenMonitorEnabledAndAgentRegistered_ShouldAppendAgentIdToContextFile()
        {
            // Arrange
            _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
                .ReturnsAsync("/repo/planner_context.md");

            // Set up context file to exist (Gemini service checks this)
            _fs.AddFile("/repo/planner_context.md");

            // Act
            var result = await SystemUnderTest.RunAsync(
                "planner",
                "gemini-1.5-pro",
                null,
                "/repo",
                false,
                true);

            // Assert
            result.ShouldBeTrue();

            // Get the registered agent from database
            using var scope3 = _scopeService.GetReadScope();
            var agents = await scope3.Agents.ToListAsync();
            agents.ShouldHaveSingleItem();
            var agent = agents.First();

            // Verify agent information was appended to context file
            var contextContent = _fs.GetFileContent("/repo/planner_context.md");
            contextContent.ShouldNotBeNull();
            contextContent.ShouldContain(agent.Id);
            contextContent.ShouldContain("mcp_aiswarm_get_next_task");
            contextContent.ShouldContain("mcp_aiswarm_create_task");
            contextContent.ShouldContain("mcp_aiswarm_report_task_completion");
            contextContent.ShouldContain("Task Management Workflow");
        }

        [Fact]
        public async Task WhenMonitorEnabledAndAgentRegistered_ShouldAppendCorrectMcpToolInstructions()
        {
            // Arrange
            _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
                .ReturnsAsync("/repo/planner_context.md");

            // Set up context file to exist (Gemini service checks this)
            _fs.AddFile("/repo/planner_context.md");

            // Act
            var result = await SystemUnderTest.RunAsync(
                "planner",
                "gemini-1.5-pro",
                null,
                "/repo",
                false,
                true);

            // Assert
            result.ShouldBeTrue();

            // Get the registered agent from database
            using var scope4 = _scopeService.GetReadScope();
            var agents = await scope4.Agents.ToListAsync();
            agents.ShouldHaveSingleItem();
            var agent = agents.First();

            // Verify correct MCP tool instructions were appended to context file
            var contextContent = _fs.GetFileContent("/repo/planner_context.md");
            contextContent.ShouldNotBeNull();
            contextContent.ShouldContain(agent.Id);

            // Should reference actual MCP tool names
            contextContent.ShouldContain("mcp_aiswarm_get_next_task");
            contextContent.ShouldContain("mcp_aiswarm_create_task");
            contextContent.ShouldContain("mcp_aiswarm_report_task_completion");

            // Should include proper parameter names
            contextContent.ShouldContain("agentId");
            contextContent.ShouldContain("taskId");
            contextContent.ShouldContain("result");

            // Should include task completion workflow
            contextContent.ShouldContain("report_task_completion");
        }

        [Fact]
        public async Task WhenMonitorDisabled_ShouldUseLegacyGeminiLaunch()
        {
            // Arrange
            _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
                .ReturnsAsync("/repo/planner_context.md");

            // Set up context file to exist (Gemini service checks this)
            _fs.AddFile("/repo/planner_context.md");

            // Act
            var result = await SystemUnderTest.RunAsync(
                "planner",
                "gemini-1.5-pro",
                null,
                "/repo",
                false,
                false);

            // Assert
            result.ShouldBeTrue();

            // Should have launched Gemini without agent settings (no configuration file)
            _process.Invocations.ShouldContain(i =>
                i.File == "pwsh.exe" &&
                i.Arguments.Contains("gemini") &&
                i.Arguments.Contains("-m \"gemini-1.5-pro\"") &&
                i.Arguments.Contains(string.Format(ExpectedPromptFormatString, "/repo/planner_context.md"))
            );

            // Should not have created configuration file since monitor is disabled
            var configContent = _fs.GetFileContent("/repo/.gemini/settings.json");
            configContent.ShouldBeNull();

            _logger.Infos.ShouldNotContain(i => i.Contains("Configuring Gemini with agent settings"));
        }
    }
}
