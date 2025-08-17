using AgentLauncher.Commands;
using AgentLauncher.Services;
using AgentLauncher.Services.Logging;
using AgentLauncher.Tests.TestDoubles;
using Shouldly;
using Moq;

namespace AgentLauncher.Tests.Commands;

public class LaunchAgentCommandHandlerTests
{
    private readonly Mock<IContextService> _context = new();
    private readonly Mock<AgentLauncher.Services.External.IProcessLauncher> _process = new();
    private readonly Mock<IGeminiService> _gemini = new();
    private readonly TestLogger _logger = new();
    private readonly TestEnvironmentService _env = new() { CurrentDirectory = "/repo" };

    [Fact]
    public async Task WhenDryRun_ShouldNotCreateContextOrLaunch()
    {
        // Arrange
        var handler = new LaunchAgentCommandHandler(
            _context.Object,
            _logger,
            _env);

        // Act
        await handler.RunAsync(
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
        // Arrange
        var handler = new LaunchAgentCommandHandler(
            _context.Object,
            _logger,
            _env);

        // Act (no worktree)
        await handler.RunAsync(
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
        // Arrange
        var handler = new LaunchAgentCommandHandler(
            _context.Object,
            _logger,
            _env);

        // Act (with worktree; directory default current)
        await handler.RunAsync(
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
        // Arrange: simulate repository state and successful worktree creation
        _process.Setup(p => p.RunAsync("git", It.Is<string>(s => s.StartsWith("rev-parse --git-dir")), It.IsAny<string>(), 5000, true))
            .ReturnsAsync(new AgentLauncher.Services.External.ProcessResult(true, ".git", string.Empty, 0));
        _process.Setup(p => p.RunAsync("git", It.Is<string>(s => s.StartsWith("rev-parse --show-toplevel")), It.IsAny<string>(), 5000, true))
            .ReturnsAsync(new AgentLauncher.Services.External.ProcessResult(true, "/repo", string.Empty, 0));
        _process.Setup(p => p.RunAsync("git", It.Is<string>(s => s.StartsWith("worktree list")), It.IsAny<string>(), 10000, true))
            .ReturnsAsync(new AgentLauncher.Services.External.ProcessResult(true, string.Empty, string.Empty, 0));
        _process.Setup(p => p.RunAsync("git", It.Is<string>(s => s.StartsWith("worktree add")), It.IsAny<string>(), 60000, true))
            .ReturnsAsync(new AgentLauncher.Services.External.ProcessResult(true, "Created", string.Empty, 0));
        var git = new GitService(_process.Object);
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>() )).ReturnsAsync("/repo- feature_x/planner_context.md");

        var handler = new LaunchAgentCommandHandler(
            _context.Object,
            _logger,
            _env,
            git
        );

        // Act (non-dry-run)
        await handler.RunAsync(
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
        // Arrange invalid name (GitService will throw)
        var git = new GitService(_process.Object);
        var handler = new LaunchAgentCommandHandler(
            _context.Object,
            _logger,
            _env,
            git
        );

        // Act
        await handler.RunAsync(
            agentType: "planner",
            model: null,
            worktree: "bad?name",
            directory: null,
            dryRun: false);

        // Assert
        _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _logger.Errors.ShouldContain(e => e.Contains("Invalid worktree name"));
    }
}
