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
    private readonly Mock<IGitService> _git = new();
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
        // Arrange: expect git validation & creation
        _git.Setup(g => g.IsValidWorktreeName("feature_x")).Returns(true);
        _git.Setup(g => g.CreateWorktreeAsync("feature_x", null)).ReturnsAsync("/repo/feature_x");
        _context.Setup(c => c.CreateContextFile("planner", It.IsAny<string>() )).ReturnsAsync("/repo/feature_x/planner_context.md");

        var handler = new LaunchAgentCommandHandler(
            _context.Object,
            _logger,
            _env,
            _git.Object
        );

        // Act (non-dry-run)
        await handler.RunAsync(
            agentType: "planner",
            model: null,
            worktree: "feature_x",
            directory: null,
            dryRun: false);

        // Assert (desired future behavior) - these will fail until handler updated
        _git.Verify(g => g.IsValidWorktreeName("feature_x"), Times.Once);
        _git.Verify(g => g.CreateWorktreeAsync("feature_x", null), Times.Once);
        _context.Verify(c => c.CreateContextFile("planner", It.Is<string>(p => p.Contains("feature_x"))), Times.Once);
    }

    [Fact]
    public async Task WhenWorktreeInvalid_ShouldLogErrorAndAbort()
    {
        // Arrange invalid name
        _git.Setup(g => g.IsValidWorktreeName("bad?name")).Returns(false);
        var handler = new LaunchAgentCommandHandler(
            _context.Object,
            _logger,
            _env,
            _git.Object
        );

        // Act
        await handler.RunAsync(
            agentType: "planner",
            model: null,
            worktree: "bad?name",
            directory: null,
            dryRun: false);

        // Assert
        _git.Verify(g => g.IsValidWorktreeName("bad?name"), Times.Once);
        _git.Verify(g => g.CreateWorktreeAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        _context.Verify(c => c.CreateContextFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _logger.Errors.ShouldContain(e => e.Contains("Invalid worktree name"));
    }
}
