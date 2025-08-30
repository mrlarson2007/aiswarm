using AISwarm.Infrastructure;
using AISwarm.Tests.TestDoubles;
using Shouldly;

namespace AISwarm.Tests.Services;

public class CreateWorkTreeTests : ISystemUnderTest<GitService>
{
    private readonly FakeFileSystemService _fs = new();
    private readonly TestLogger _logger = new();
    private readonly PassThroughProcessLauncher _process = new();

    public GitService SystemUnderTest => new(_process, _fs, _logger);

    [Fact]
    public async Task ShouldReturnExistingPath_WhenWorktreeAlreadyListed()
    {
        var worktreeListOutput =
@"worktree D:/dev/projects/aiswarm
HEAD 1bd36db7340b7e680f1e3037c83e592bd24971a4
branch refs/heads/master

worktree D:/dev/projects/aiswarm-code-review-event-subscription
HEAD b7715788c0a2df79fa7c5cab30593747937420c7
branch refs/heads/aiswarm-code-review-event-subscription

worktree D:/dev/projects/aiswarm-event-subscription-dev
HEAD 56e5eb6e818e2ac4b4e12ade3ff1a7e627bf9179
branch refs/heads/aiswarm-event-subscription-dev

worktree D:/dev/projects/aiswarm-memory-events-dev
HEAD 0a9287421e19943816850d177b44b14ed954a0d4
branch refs/heads/aiswarm-memory-events-dev

worktree D:/dev/projects/aiswarm-review-event-subscription
HEAD b7715788c0a2df79fa7c5cab30593747937420c7
branch refs/heads/aiswarm-review-event-subscription";


        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
            new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"),
            new ProcessResult(true, "/repo", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"),
            new ProcessResult(true, worktreeListOutput, string.Empty, 0));

        var result = await SystemUnderTest.CreateWorktreeAsync("aiswarm-event-subscription-dev");
        result.ShouldBe("D:/dev/projects/aiswarm-event-subscription-dev");
    }

    [Fact]
    public async Task ShouldFail_WhenWorktreeNameIsInvalid()
    {
        var ex = await Should.ThrowAsync<ArgumentException>(() =>
            SystemUnderTest.CreateWorktreeAsync("invalid|name"));
        ex.Message.ShouldContain("Invalid worktree name");
    }

    [Fact]
    public async Task ShouldCreateNewWorktree_WhenNotAlreadyExisting()
    {
        var worktreeListOutput =
@"worktree /repo/aiswarm-new-feature2
HEAD 1bd36db7340b7e680f1e3037c83e592bd24971a4
branch refs/heads/master

worktree /repo/aiswarm-new-feature
HEAD b7715788c0a2df79fa7c5cab30593747937420c7
branch refs/heads/aiswarm-code-review-event-subscription";

        var existingPath = "/repo/aiswarm-new-feature";

        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
            new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"),
            new ProcessResult(true, "/repo", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"),
            new ProcessResult(true, worktreeListOutput, string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith($"worktree add \"{existingPath}\""),
            new ProcessResult(true, string.Empty, string.Empty, 0));

        var result = await SystemUnderTest.CreateWorktreeAsync("aiswarm-new-feature");
        result.ShouldBe(existingPath);
        var normalizedResult = result.Replace('\\', '/');
        var normalizedExpected = existingPath.Replace('\\', '/');
        normalizedResult.ShouldBe(normalizedExpected);
    }

    [Fact]
    public async Task ShouldThrow_WhenNotInGitRepository()
    {
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
            new ProcessResult(false, string.Empty, "fatal: not a git repository (or any of the parent directories): .git", 128));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            SystemUnderTest.CreateWorktreeAsync("aiswarm-new-feature"));
        ex.Message.ShouldBe("Not in a git repository");
    }

    [Fact]
    public async Task ShouldThrow_WhenCannotDetermineRepositoryRoot()
    {
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
            new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"),
            new ProcessResult(false, string.Empty, "fatal: not a git repository (or any of the parent directories): .git", 128));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            SystemUnderTest.CreateWorktreeAsync("aiswarm-new-feature"));
        ex.Message.ShouldBe("Could not determine git repository root");
    }

    [Fact]
    public async Task ShouldThrow_WhenWorktreeAddFails()
    {
        var worktreeListOutput =
@"worktree D:/dev/projects/aiswarm
HEAD 1bd36db7340b7e680f1e3037c83e592bd24971a4
branch refs/heads/master";

        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
            new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"),
            new ProcessResult(true, "/repo", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"),
            new ProcessResult(true, worktreeListOutput, string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree add"),
            new ProcessResult(false, string.Empty, "fatal: A branch named 'aiswarm-new-feature' already exists.", 128));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            SystemUnderTest.CreateWorktreeAsync("aiswarm-new-feature"));
        ex.Message.ShouldBe("Failed to create worktree: fatal: A branch named 'aiswarm-new-feature' already exists.");
    }
}
