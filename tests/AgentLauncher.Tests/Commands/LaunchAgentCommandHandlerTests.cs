using AgentLauncher.Commands;
using AgentLauncher.Services;
using AgentLauncher.Services.Logging;
using AgentLauncher.Tests.TestDoubles;
using AgentLauncher.Models;
using Shouldly;
using Moq;
using AgentLauncher.Services.External;

namespace AgentLauncher.Tests.Commands;

public class LaunchAgentCommandHandlerTests
{
    private readonly Mock<IContextService> _context = new();
    private readonly PassThroughProcessLauncher _process = new();
    private readonly FakeFileSystemService _fs = new();
    private readonly Mock<IGeminiService> _gemini = new();
    private readonly Mock<ILocalAgentService> _localAgentService = new();
    private readonly TestLogger _logger = new();
    private readonly TestEnvironmentService _env = new() { CurrentDirectory = "/repo" };
    private readonly GitService _git;

    public LaunchAgentCommandHandlerTests()
    {
        _git = new GitService(_process, _fs, _logger);
    }

    private LaunchAgentCommandHandler SystemUnderTest => new(
        _context.Object,
        _logger,
        _env,
        _git,
        _gemini.Object,
        _fs,
        _localAgentService.Object
    );

    [Fact]
    public async Task WhenDryRun_ShouldNotCreateContextOrLaunch()
    {
        // Act
        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: null,
                worktree: null,
                directory: null,
                dryRun: true);
        result.ShouldBeTrue();

        // Assert
        _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _logger.Infos.ShouldContain(s => s.Contains("Dry run mode"));
        _gemini.Verify(g => g.LaunchInteractiveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AgentSettings?>()), Times.Never);
    }

    [Fact]
    public async Task WhenDryRunAndNoWorkTree_ShouldLogPlannedLaunchDetailsWithoutWorktree()
    {

        // Act (no worktree)
        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: "gemini-1.5-flash",
                worktree: null,
                directory: "/custom",
                dryRun: true);
        result.ShouldBeTrue();

        // Assert
        _logger.Infos.ShouldContain(s => s.Contains("Dry run mode"));
        _logger.Infos.ShouldContain(s => s.Contains("Agent: planner"));
        _logger.Infos.ShouldContain(s => s.Contains("Model: gemini-1.5-flash"));
        _logger.Infos.ShouldContain(s => s.Contains("Workspace: Current branch"));
        _logger.Infos.ShouldContain(s => s.Contains("Working directory: /custom"));
        var planned = $"Planned context file: {Path.Combine("/custom", "planner_context.md")}";
        _logger.Infos.ShouldContain(s => s.Contains(planned));
        _logger.Infos.ShouldContain(s => s.Contains("Manual launch:"));
        _logger.Infos.ShouldNotContain(s => s.Contains("Worktree (planned):"));
    }

    [Fact]
    public async Task WhenDryRunAndWithWorktree_ShouldLogPlannedWorktreeAndDetails()
    {

        // Act (with worktree; directory default current)
        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: "gemini-1.5-flash",
                worktree: "feature_x",
                directory: null,
                dryRun: true);
        result.ShouldBeTrue();

        // Assert
        _logger.Infos.ShouldContain(s => s.Contains("Dry run mode"));
        _logger.Infos.ShouldContain(s => s.Contains("Agent: planner"));
        _logger.Infos.ShouldContain(s => s.Contains("Worktree (planned): feature_x"));
        _logger.Infos.ShouldContain(s => s.Contains("Manual launch:"));
    }

    [Fact]
    public async Task WhenWorktreeSpecified_ShouldCreateWorktree_ThenContext_InNewDirectory()
    {
        // Arrange expected git command sequence
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"), new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"), new ProcessResult(true, "/repo", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"), new ProcessResult(true, string.Empty, string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree add"), new ProcessResult(true, "Created", string.Empty, 0));
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>())).ReturnsAsync("/repo-feature_x/planner_context.md");

        // Act (non-dry-run)
        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: null,
                worktree: "feature_x",
                directory: null,
                dryRun: false);
        result.ShouldBeTrue();

        // Assert (desired future behavior) - these will fail until handler updated
        _context.Verify(c => c.CreateContextFile("planner", It.Is<string>(p => p.Contains("feature_x"))), Times.Once);
    }



    [Fact]
    public async Task WhenWorktreeInvalid_ShouldLogErrorAndAbort()
    {
        // Act
        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: null,
                worktree: "bad?name",
                directory: null,
                dryRun: false);
        result.ShouldBeFalse();

        // Assert
        _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _logger.Errors.ShouldContain(e => e.Contains("Invalid worktree name"));
    }

    [Fact]
    public async Task WhenNonDryRunWithoutWorktree_ShouldLaunchGeminiWithNullModel()
    {
        // Arrange context creation
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
            .ReturnsAsync("/repo/planner_context.md");

        // Act
        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: null,
                worktree: null,
                directory: null,
                dryRun: false);
        result.ShouldBeTrue();

        // Assert
        _gemini.Verify(g => g.LaunchInteractiveAsync("/repo/planner_context.md", null, "/repo", null), Times.Once);
    }

    [Fact]
    public async Task WhenNonDryRunWithModel_ShouldLaunchGeminiWithModel()
    {
        // Arrange context creation
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
            .ReturnsAsync("/repo/planner_context.md");

        // Act
        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: "gemini-1.5-pro",
                worktree: null,
                directory: null,
                dryRun: false);
        result.ShouldBeTrue();

        // Assert
        _gemini.Verify(g => g.LaunchInteractiveAsync("/repo/planner_context.md", "gemini-1.5-pro", "/repo", null), Times.Once);
    }

    // New edge case tests
    [Fact]
    public async Task WhenWorktreeAddFails_ShouldLogErrorAndAbort()
    {
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"), new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"), new ProcessResult(true, "/repo", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"), new ProcessResult(true, string.Empty, string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree add"), new ProcessResult(false, string.Empty, "fatal: permission denied", 1));

        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: null,
                worktree: "feat_fail",
                directory: null,
                dryRun: false);
        result.ShouldBeFalse();

        _logger.Errors.ShouldContain(e => e.Contains("Failed to create worktree"));
        _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _gemini.Verify(g => g.LaunchInteractiveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AgentSettings?>()), Times.Never);
    }

    [Fact]
    public async Task WhenNotInGitRepo_ShouldLogErrorAndAbort()
    {
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"), new ProcessResult(false, string.Empty, "not a repo", 128));

        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: null,
                worktree: "feature_new",
                directory: null,
                dryRun: false);
        result.ShouldBeFalse();

        _logger.Errors.ShouldContain(e => e.Contains("Not in a git repository") || e.Contains("not a git"));
        _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task WhenDirectoryAndWorktreeProvided_WorktreePathShouldOverride()
    {
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"), new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"), new ProcessResult(true, "/repo", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"), new ProcessResult(true, string.Empty, string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree add"), new ProcessResult(true, "Created", string.Empty, 0));
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>())).ReturnsAsync("/repo-feature_override/planner_context.md");

        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: null,
                worktree: "feature_override",
                directory: "/some/other/dir",
                dryRun: false);
        result.ShouldBeTrue();

        _context.Verify(c => c.CreateContextFile("planner", It.Is<string>(p => p.Contains("repo-feature_override"))), Times.Once);
    }

    [Fact]
    public async Task WhenDryRunWithInvalidWorktree_ShouldStillReportPlannedInvalidName()
    {
        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: null,
                worktree: "bad?name",
                directory: null,
                dryRun: true);
        result.ShouldBeTrue();

        _logger.Infos.ShouldContain(i => i.Contains("Worktree (planned): bad?name"));
        _logger.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenWorktreeAlreadyExists_ShouldLogErrorAndAbort()
    {
        var porcelain = "worktree /repo/worktrees/feature_dup\nbranch refs/heads/feature_dup\n";
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"), new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"), new ProcessResult(true, "/repo", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"), new ProcessResult(true, porcelain, string.Empty, 0));

        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: null,
                worktree: "feature_dup",
                directory: null,
                dryRun: false);
        result.ShouldBeFalse();

        _logger.Errors.ShouldContain(e => e.Contains("already exists"));
        _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _gemini.Verify(g => g.LaunchInteractiveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AgentSettings?>()), Times.Never);
    }

    [Fact]
    public async Task WhenWorktreeCreated_ShouldLaunchGeminiFromWorktree()
    {
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"), new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"), new ProcessResult(true, "/repo", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"), new ProcessResult(true, string.Empty, string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree add"), new ProcessResult(true, "Created", string.Empty, 0));
        _context.Setup(c => c.CreateContextFile("planner", It.Is<string>(p => p.Contains("repo-feature_launch"))))
            .ReturnsAsync("/repo-feature_launch/planner_context.md");

        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: "gemini-1.5-pro",
                worktree: "feature_launch",
                directory: null,
                dryRun: false);
        result.ShouldBeTrue();

        _gemini.Verify(g => g.LaunchInteractiveAsync("/repo-feature_launch/planner_context.md", "gemini-1.5-pro", It.IsAny<string>(), null), Times.Once);
    }

    [Fact]
    public async Task WhenDirectorySpecifiedWithoutWorktree_ShouldUseDirectoryForContext()
    {
        _context.Setup(c => c.CreateContextFile("planner", "/customdir"))
            .ReturnsAsync("/customdir/planner_context.md");

        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: null,
                worktree: null,
                directory: "/customdir",
                dryRun: false);
        result.ShouldBeTrue();

        _context.Verify(c => c.CreateContextFile("planner", "/customdir"), Times.Once);
    }

    [Fact]
    public async Task WhenDryRunWithModel_ShouldIncludeModelInManualCommand()
    {
        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: "gemini-2.0-ultra",
                worktree: null,
                directory: null,
                dryRun: true);
        result.ShouldBeTrue();

        _logger.Infos.ShouldContain(i => i.Contains("-m gemini-2.0-ultra"));
    }

    [Fact]
    public async Task WhenWorktreeWhitespace_ShouldIgnoreAndProceedWithoutWorktree()
    {
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
            .ReturnsAsync("/repo/planner_context.md");

        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: null,
                worktree: "  \t  ",
                directory: null,
                dryRun: false);
        result.ShouldBeTrue();

        _process.Invocations.ShouldNotContain(i => i.Arguments.StartsWith("worktree add"));
        _gemini.Verify(g => g.LaunchInteractiveAsync("/repo/planner_context.md", null, "/repo", null), Times.Once);
    }

    [Fact]
    public async Task WhenGeminiLaunchThrows_ShouldSurfaceError()
    {
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
            .ReturnsAsync("/repo/planner_context.md");
        _gemini.Setup(g => g.LaunchInteractiveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AgentSettings?>()))
            .ThrowsAsync(new InvalidOperationException("Gemini CLI not installed"));

        var result = await SystemUnderTest.RunAsync(
                agentType: "planner",
                model: null,
                worktree: null,
                directory: null,
                dryRun: false);
        result.ShouldBeFalse();
        _logger.Errors.ShouldContain(e => e.Contains("Gemini launch failed"));
    }

    [Fact]
    public async Task WhenMonitorEnabled_ShouldRegisterAgentInDatabase()
    {
        // Arrange
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
            .ReturnsAsync("/repo/planner_context.md");
        _localAgentService.Setup(s => s.RegisterAgentAsync(It.IsAny<AgentRegistrationRequest>()))
            .ReturnsAsync("agent-123");

        // Act
        var result = await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: "gemini-1.5-pro",
            worktree: null,
            directory: "/repo",
            dryRun: false,
            monitor: true);
        
        // Assert
        result.ShouldBeTrue();
        _localAgentService.Verify(s => s.RegisterAgentAsync(It.Is<AgentRegistrationRequest>(r =>
            r.AgentType == "planner" &&
            r.WorkingDirectory == "/repo" &&
            r.Model == "gemini-1.5-pro")), Times.Once);
        _logger.Infos.ShouldContain(i => i.Contains("Registered agent") && i.Contains("agent-123"));
    }

    [Fact]
    public async Task WhenMonitorEnabledAndAgentRegistered_ShouldConfigureGeminiWithSettingsFile()
    {
        // Arrange
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
            .ReturnsAsync("/repo/planner_context.md");
        _localAgentService.Setup(s => s.RegisterAgentAsync(It.IsAny<AgentRegistrationRequest>()))
            .ReturnsAsync("agent-456");

        // Act
        var result = await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: "gemini-1.5-pro",
            worktree: null,
            directory: "/repo",
            dryRun: false,
            monitor: true);
        
        // Assert
        result.ShouldBeTrue();
        
        // Should call LaunchInteractiveAsync with agent settings
        _gemini.Verify(g => g.LaunchInteractiveAsync(
            "/repo/planner_context.md", 
            "gemini-1.5-pro", 
            "/repo",
            It.Is<AgentSettings>(s => 
                s.AgentId == "agent-456" && 
                s.McpServerUrl != null)), 
            Times.Once);
        
        _logger.Infos.ShouldContain(i => i.Contains("Configuring Gemini with agent settings"));
    }

    [Fact]
    public async Task WhenMonitorDisabled_ShouldUseLegacyGeminiLaunch()
    {
        // Arrange
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
            .ReturnsAsync("/repo/planner_context.md");

        // Act
        var result = await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: "gemini-1.5-pro",
            worktree: null,
            directory: "/repo",
            dryRun: false,
            monitor: false);
        
        // Assert
        result.ShouldBeTrue();
        
        // Should call traditional LaunchInteractiveAsync (no settings)
        _gemini.Verify(g => g.LaunchInteractiveAsync(
            "/repo/planner_context.md", 
            "gemini-1.5-pro", 
            "/repo",
            null), 
            Times.Once);
        
        // Should not call any other methods - this test should use LaunchInteractiveAsync with agentSettings=null
        _logger.Infos.ShouldNotContain(i => i.Contains("Configuring Gemini with agent settings"));
    }
}
