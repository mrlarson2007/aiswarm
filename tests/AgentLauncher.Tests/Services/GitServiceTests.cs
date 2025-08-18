using AgentLauncher.Services;
using AgentLauncher.Services.External;
using AgentLauncher.Tests.TestDoubles;
using Shouldly;

namespace AgentLauncher.Tests.Services;

public class GitServiceTests
{
    private readonly PassThroughProcessLauncher _process = new();
    private readonly FakeFileSystemService _fs = new();

    private GitService Create() => new GitService(_process, _fs);

    [Fact]
    public async Task CreateWorktree_ShouldFail_WhenDirectoryAlreadyExists()
    {
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"), new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"), new ProcessResult(true, "/repo", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"), new ProcessResult(true, string.Empty, string.Empty, 0));
        _fs.AddDirectory("/repo-feature_dup");

        var sut = Create();
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => sut.CreateWorktreeAsync("feature_dup"));
        ex.Message.ShouldContain("Directory already exists");
    }

    [Fact]
    public async Task CreateWorktree_ShouldFail_WhenWorktreeAlreadyListed()
    {
        var porcelain = "worktree /repo/worktrees/feature_a\nbranch refs/heads/feature_a\n";
        _process.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"), new ProcessResult(true, ".git", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"), new ProcessResult(true, "/repo", string.Empty, 0));
        _process.Enqueue("git", a => a.StartsWith("worktree list"), new ProcessResult(true, porcelain, string.Empty, 0));

        var sut = Create();
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => sut.CreateWorktreeAsync("feature_a"));
        ex.Message.ShouldContain("already exists");
    }
}
