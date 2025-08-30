using AISwarm.Infrastructure;
using AISwarm.Tests.TestDoubles;
using Shouldly;
using Xunit;

namespace AISwarm.Tests.Services;

public class GitServiceTests : IDisposable
{
    private readonly PassThroughProcessLauncher _processLauncher;
    private readonly FakeFileSystemService _fileSystemService;
    private readonly TestLogger _logger;
    private readonly GitService _gitService;

    public GitServiceTests()
    {
        _processLauncher = new PassThroughProcessLauncher();
        _fileSystemService = new FakeFileSystemService();
        _logger = new TestLogger();
        _gitService = new GitService(_processLauncher, _fileSystemService, _logger);
    }

    public class CreateWorktreeTests : GitServiceTests
    {
        [Fact]
        public async Task WhenDirectoryExistsButNotInWorktreeList_ShouldAllowMultipleAgentsInSameWorktree()
        {
            // Arrange - Set up git repository state
            _processLauncher.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
                new ProcessResult(true, ".git", string.Empty, 0));
            _processLauncher.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"),
                new ProcessResult(true, "/repo/root", string.Empty, 0));
            
            // No existing worktrees initially
            _processLauncher.Enqueue("git", a => a.StartsWith("worktree list"),
                new ProcessResult(true, "worktree /repo/root\nHEAD 1234567\n\n", string.Empty, 0));
            
            // Directory exists (simulating another agent already created it)
            var expectedPath = Path.Combine("/repo", "root-test-worktree");
            _fileSystemService.AddDirectory(expectedPath);
            
            // Git should succeed when trying to add worktree to existing directory
            _processLauncher.Enqueue("git", a => a.Contains($"worktree add \"{expectedPath}\""),
                new ProcessResult(true, "Preparing worktree", string.Empty, 0));

            // Act - This should succeed in the fixed version
            var result = await _gitService.CreateWorktreeAsync("test-worktree");

            // Assert - Normalize paths for cross-platform comparison
            var normalizedResult = result.Replace('\\', '/');
            var normalizedExpected = expectedPath.Replace('\\', '/');
            normalizedResult.ShouldBe(normalizedExpected);
        }

        [Fact]
        public async Task WhenWorktreeAlreadyExistsInGitList_ShouldReturnExistingPath()
        {
            // Arrange - Set up git repository state
            _processLauncher.Enqueue("git", a => a.StartsWith("rev-parse --git-dir"),
                new ProcessResult(true, ".git", string.Empty, 0));
            _processLauncher.Enqueue("git", a => a.StartsWith("rev-parse --show-toplevel"),
                new ProcessResult(true, "/repo/root", string.Empty, 0));
            
            // Existing worktree already in git list
            var existingPath = "/repo/root-test-worktree";
            _processLauncher.Enqueue("git", a => a.StartsWith("worktree list"),
                new ProcessResult(true, $"worktree /repo/root\nHEAD 1234567\n\nworktree {existingPath}\nbranch refs/heads/test-branch\nHEAD abcdef\n\n", string.Empty, 0));

            // Act
            var result = await _gitService.CreateWorktreeAsync("test-worktree");

            // Assert - Normalize paths for cross-platform comparison
            var normalizedResult = result.Replace('\\', '/');
            var normalizedExpected = existingPath.Replace('\\', '/');
            normalizedResult.ShouldBe(normalizedExpected);
        }
    }

    public void Dispose()
    {
        // Clean up if needed
    }
}