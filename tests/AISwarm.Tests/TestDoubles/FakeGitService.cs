using AISwarm.Infrastructure;

namespace AISwarm.Tests.TestDoubles;

public class FakeGitService : IGitService
{
    public string FailureMessage { get; set; } = string.Empty;
    public bool ShouldFail => !string.IsNullOrEmpty(FailureMessage);
    public bool IsRepository { get; set; } = true;
    public string? RepositoryRoot { get; set; } = "/test/repo";
    public Dictionary<string, string> ExistingWorktrees { get; set; } = new();
    public string CreatedWorktreePath { get; set; } = "/test/repo/worktree";

    public Task<bool> IsGitRepositoryAsync()
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.FromResult(IsRepository);
    }

    public Task<string?> GetRepositoryRootAsync()
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.FromResult(RepositoryRoot);
    }

    public bool IsValidWorktreeName(string name)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return !string.IsNullOrWhiteSpace(name) && !name.Contains(' ');
    }

    public Task<Dictionary<string, string>> GetExistingWorktreesAsync()
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.FromResult(ExistingWorktrees);
    }

    public Task<string> CreateWorktreeAsync(string name, string? baseBranch = null)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.FromResult(CreatedWorktreePath);
    }

    public Task<bool> RemoveWorktreeAsync(string name)
    {
        if (ShouldFail)
            throw new InvalidOperationException(FailureMessage);

        return Task.FromResult(true);
    }
}
