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
    public async Task ShouldFail_WhenDirectoryAlreadyExists()
    {
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
            new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"),
            new ProcessResult(true, "/repo", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"),
            new ProcessResult(true, string.Empty, string.Empty, 0));
        _fs.AddDirectory("/repo-feature_dup");

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            SystemUnderTest.CreateWorktreeAsync("feature_dup"));
        ex.Message.ShouldContain("Directory already exists");
    }

    [Fact]
    public async Task ShouldFail_WhenWorktreeAlreadyListed()
    {
        var worktreeListOutput = "worktree /repo/worktrees/feature_a\nbranch refs/heads/feature_a\n";
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
            new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"),
            new ProcessResult(true, "/repo", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"),
            new ProcessResult(true, worktreeListOutput, string.Empty, 0));

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            SystemUnderTest.CreateWorktreeAsync("feature_a"));
        ex.Message.ShouldContain("already exists");
    }
}
