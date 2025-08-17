using AgentLauncher.Commands;
using AgentLauncher.Services;
using AgentLauncher.Services.Logging;
using AgentLauncher.Tests.TestDoubles;
using FluentAssertions;
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
        _logger.Infos.Should().Contain(s => s.Contains("Dry run mode"));
        _gemini.Verify(g => g.LaunchInteractiveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task DryRun_ShouldLogPlannedLaunchDetails()
    {
        // Arrange
        var handler = new LaunchAgentCommandHandler(
            _context.Object,
            _logger,
            _env);

        // Act
        await handler.RunAsync(
            agentType: "planner",
            model: "gemini-1.5-flash",
            worktree: null,
            directory: "/custom",
            dryRun: true);

        // Assert
        _logger.Infos.Should().Contain(s => s.Contains("Dry run mode"));
        _logger.Infos.Should().Contain(s => s.Contains("Agent: planner"));
        _logger.Infos.Should().Contain(s => s.Contains("Model: gemini-1.5-flash"));
        _logger.Infos.Should().Contain(s => s.Contains("Workspace: Current branch"));
        _logger.Infos.Should().Contain(s => s.Contains("Working directory: /custom"));
        var sep = System.IO.Path.DirectorySeparatorChar;
        var planned = $"Planned context file: /custom{sep}planner_context.md";
        _logger.Infos.Should().Contain(s => s.Contains(planned));
        _logger.Infos.Should().Contain(s => s.Contains("Manual launch:"));
    }
}
