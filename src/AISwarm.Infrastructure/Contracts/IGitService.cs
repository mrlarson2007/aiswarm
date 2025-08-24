namespace AISwarm.Infrastructure;

/// <summary>
/// Service abstraction over git operations needed for the launcher (repository detection,
/// worktree management, and validation). Relies on an injected process runner.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Determine whether the current working directory resides within a git repository.
    /// </summary>
    Task<bool> IsGitRepositoryAsync();

    /// <summary>
    /// Get the absolute path to the repository root directory.
    /// </summary>
    /// <returns>Root path or <c>null</c> if not in a repository.</returns>
    Task<string?> GetRepositoryRootAsync();

    /// <summary>
    /// Validate a proposed worktree name against naming and reserved word rules.
    /// </summary>
    /// <param name="name">Candidate worktree name.</param>
    /// <returns><c>true</c> if valid.</returns>
    bool IsValidWorktreeName(string name);

    /// <summary>
    /// Retrieve existing worktrees keyed by their directory name.
    /// </summary>
    /// <returns>Dictionary of worktree name to absolute path.</returns>
    Task<Dictionary<string, string>> GetExistingWorktreesAsync();

    /// <summary>
    /// Create a new worktree from the current (or specified base) branch.
    /// </summary>
    /// <param name="name">Name component for the new worktree.</param>
    /// <param name="baseBranch">Optional branch to base the worktree on (defaults to current HEAD).</param>
    /// <returns>Full path to the created worktree.</returns>
    Task<string> CreateWorktreeAsync(string name, string? baseBranch = null);

    /// <summary>
    /// Remove an existing worktree by name.
    /// </summary>
    /// <param name="name">Worktree name.</param>
    /// <returns><c>true</c> if removal succeeded.</returns>
    Task<bool> RemoveWorktreeAsync(string name);
}
