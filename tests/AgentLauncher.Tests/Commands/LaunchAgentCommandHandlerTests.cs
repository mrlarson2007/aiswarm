using AgentLauncher.Commands;
using AgentLauncher.Services;
using AgentLauncher.Services.Logging;
using AgentLauncher.Tests.TestDoubles;
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
    private readonly TestLogger _logger = new();
    private readonly TestEnvironmentService _env = new() { CurrentDirectory = "/repo" };
    private readonly GitService _git;

    public LaunchAgentCommandHandlerTests()
    {
    _git = new GitService(_process, _fs);
    }

    private LaunchAgentCommandHandler SystemUnderTest => new(
        _context.Object,
        _logger,
        _env,
        _git,
        _gemini.Object
    );

    [Fact]
    public async Task WhenDryRun_ShouldNotCreateContextOrLaunch()
    {
        // Act
        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: null,
            worktree: null,
            directory: null,
            dryRun: true);

        // Assert
        _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _logger.Infos.ShouldContain(s => s.Contains("Dry run mode"));
        _gemini.Verify(g => g.LaunchInteractiveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task WhenDryRunAndNoWorkTree_ShouldLogPlannedLaunchDetailsWithoutWorktree()
    {

        // Act (no worktree)
        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: "gemini-1.5-flash",
            worktree: null,
            directory: "/custom",
            dryRun: true);

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
        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: "gemini-1.5-flash",
            worktree: "feature_x",
            directory: null,
            dryRun: true);

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
        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: null,
            worktree: "feature_x",
            directory: null,
            dryRun: false);

        // Assert (desired future behavior) - these will fail until handler updated
        _context.Verify(c => c.CreateContextFile("planner", It.Is<string>(p => p.Contains("feature_x"))), Times.Once);
    }



    [Fact]
    public async Task WhenWorktreeInvalid_ShouldLogErrorAndAbort()
    {
        // Act
        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: null,
            worktree: "bad?name",
            directory: null,
            dryRun: false);

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
        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: null,
            worktree: null,
            directory: null,
            dryRun: false);

        // Assert
        _gemini.Verify(g => g.LaunchInteractiveAsync("/repo/planner_context.md", null, null), Times.Once);
    }

    [Fact]
    public async Task WhenNonDryRunWithModel_ShouldLaunchGeminiWithModel()
    {
        // Arrange context creation
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
            .ReturnsAsync("/repo/planner_context.md");

        // Act
        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: "gemini-1.5-pro",
            worktree: null,
            directory: null,
            dryRun: false);

        // Assert
        _gemini.Verify(g => g.LaunchInteractiveAsync("/repo/planner_context.md", "gemini-1.5-pro", null), Times.Once);
    }

    // New edge case tests
    [Fact]
    public async Task WhenWorktreeAddFails_ShouldLogErrorAndAbort()
    {
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"), new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"), new ProcessResult(true, "/repo", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"), new ProcessResult(true, string.Empty, string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree add"), new ProcessResult(false, string.Empty, "fatal: permission denied", 1));

        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: null,
            worktree: "feat_fail",
            directory: null,
            dryRun: false);

        _logger.Errors.ShouldContain(e => e.Contains("Failed to create worktree"));
        _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _gemini.Verify(g => g.LaunchInteractiveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task WhenNotInGitRepo_ShouldLogErrorAndAbort()
    {
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"), new ProcessResult(false, string.Empty, "not a repo", 128));

        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: null,
            worktree: "feature_new",
            directory: null,
            dryRun: false);

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

        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: null,
            worktree: "feature_override",
            directory: "/some/other/dir",
            dryRun: false);

        _context.Verify(c => c.CreateContextFile("planner", It.Is<string>(p => p.Contains("repo-feature_override"))), Times.Once);
    }

    [Fact]
    public async Task WhenDryRunWithInvalidWorktree_ShouldStillReportPlannedInvalidName()
    {
        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: null,
            worktree: "bad?name",
            directory: null,
            dryRun: true);

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

        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: null,
            worktree: "feature_dup",
            directory: null,
            dryRun: false);

        _logger.Errors.ShouldContain(e => e.Contains("already exists"));
        _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _gemini.Verify(g => g.LaunchInteractiveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
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

        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: "gemini-1.5-pro",
            worktree: "feature_launch",
            directory: null,
            dryRun: false);

        _gemini.Verify(g => g.LaunchInteractiveAsync("/repo-feature_launch/planner_context.md", "gemini-1.5-pro", null), Times.Once);
    }

    [Fact]
    public async Task WhenDirectorySpecifiedWithoutWorktree_ShouldUseDirectoryForContext()
    {
        _context.Setup(c => c.CreateContextFile("planner", "/customdir"))
            .ReturnsAsync("/customdir/planner_context.md");

        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: null,
            worktree: null,
            directory: "/customdir",
            dryRun: false);

        _context.Verify(c => c.CreateContextFile("planner", "/customdir"), Times.Once);
    }

    [Fact]
    public async Task WhenDryRunWithModel_ShouldIncludeModelInManualCommand()
    {
        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: "gemini-2.0-ultra",
            worktree: null,
            directory: null,
            dryRun: true);

        _logger.Infos.ShouldContain(i => i.Contains("-m gemini-2.0-ultra"));
    }

    [Fact]
    public async Task WhenWorktreeWhitespace_ShouldIgnoreAndProceedWithoutWorktree()
    {
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
            .ReturnsAsync("/repo/planner_context.md");

        await SystemUnderTest.RunAsync(
            agentType: "planner",
            model: null,
            worktree: "  \t  ",
            directory: null,
            dryRun: false);

        _process.Invocations.ShouldNotContain(i => i.Arguments.StartsWith("worktree add"));
        _gemini.Verify(g => g.LaunchInteractiveAsync("/repo/planner_context.md", null, null), Times.Once);
    }

    [Fact]
    public async Task WhenGeminiLaunchThrows_ShouldSurfaceError()
    {
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>()))
            .ReturnsAsync("/repo/planner_context.md");
        _gemini.Setup(g => g.LaunchInteractiveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("Gemini CLI not installed"));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => SystemUnderTest.RunAsync(
            agentType: "planner",
            model: null,
            worktree: null,
            directory: null,
            dryRun: false));

        ex.Message.ShouldContain("Gemini CLI not installed");
        _logger.Errors.ShouldBeEmpty(); // handler does not catch Gemini errors currently
    }
}
