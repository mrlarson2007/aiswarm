using AgentLauncher.Commands;
using AISwarm.Infrastructure;
using AISwarm.Tests.TestDoubles;
using Shouldly;

namespace AISwarm.Tests.Commands;

public class ListWorktreesCommandHandlerTests
{
    private readonly FakeFileSystemService _fs = new();
    private readonly TestLogger _logger = new();
    private readonly PassThroughProcessLauncher _process = new();

    private ListWorktreesCommandHandler SystemUnderTest => new(
        new GitService(_process, _fs, _logger),
        _logger);

    [Fact]
    public async Task WhenNotGitRepo_ShouldLogNotRepositoryAndReturnTrue()
    {
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
            new ProcessResult(false, string.Empty, "fatal: not a git repo", 128));
        var result = await SystemUnderTest.RunAsync();
        result.ShouldBeTrue();
        _logger.Infos.ShouldContain(i => i.Contains("Not in a git repository"));
    }

    [Fact]
    public async Task WhenNoWorktrees_ShouldIndicateNoneFound()
    {
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
            new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"),
            new ProcessResult(true, string.Empty, string.Empty, 0));
        var result = await SystemUnderTest.RunAsync();
        result.ShouldBeTrue();
        _logger.Infos.ShouldContain(i => i.Contains("No worktrees found"));
        _logger.Infos.ShouldContain(i => i.Contains("Use --worktree <name> to create"));
    }

    [Fact]
    public async Task WhenWorktreesExist_ShouldListEachAndReturnTrue()
    {
        var porcelain = string.Join('\n',
            new[]
            {
                "worktree /path/main", "branch refs/heads/main", "worktree /path/feature_x",
                "branch refs/heads/feature_x"
            });
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
            new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"),
            new ProcessResult(true, porcelain, string.Empty, 0));
        var result = await SystemUnderTest.RunAsync();
        result.ShouldBeTrue();
        _logger.Infos.ShouldContain(i => i.Contains("feature_x"));
        _logger.Infos.ShouldContain(i => i.Contains("main"));
        _logger.Infos.ShouldContain(i => i.Contains("To create a new worktree"));
        _logger.Infos.ShouldContain(i => i.Contains("To remove a worktree"));
    }

    [Fact]
    public async Task WhenExceptionThrown_ShouldLogErrorAndReturnFalse()
    {
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
            new ProcessResult(true, ".git", string.Empty, 0));
        // Force failure by making worktree list return failure
        _process.Enqueue("git", a => a.StartsWith("worktree list"),
            new ProcessResult(false, string.Empty, "unexpected failure", 1));
        var result = await SystemUnderTest.RunAsync();
        result.ShouldBeFalse();
        _logger.Errors.ShouldContain(e => e.Contains("Error listing worktrees"));
    }
}
