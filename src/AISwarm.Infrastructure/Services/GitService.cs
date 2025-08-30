using System.Text.RegularExpressions;

namespace AISwarm.Infrastructure;

/// <inheritdoc />
public partial class GitService(
    IProcessLauncher process,
    IFileSystemService fileSystem,
    IAppLogger logger) : IGitService
{
    /// <inheritdoc />
    public async Task<bool> IsGitRepositoryAsync()
    {
        var result = await process.RunAsync("git", "rev-parse --git-dir", Environment.CurrentDirectory, 5000);
        return result.IsSuccess;
    }

    /// <inheritdoc />
    public async Task<string?> GetRepositoryRootAsync()
    {
        var result = await process.RunAsync("git", "rev-parse --show-toplevel", Environment.CurrentDirectory, 5000);
        return result.IsSuccess ? result.StandardOutput.Trim() : null;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetExistingWorktreesAsync()
    {
        var dict = new Dictionary<string, string>();
        var result = await process.RunAsync("git", "worktree list --porcelain", Environment.CurrentDirectory, 10000);
        if (!result.IsSuccess)
            throw new InvalidOperationException($"Failed to list worktrees: {result.StandardError}");
        var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string? currentWorktreePath = null;
        foreach (var line in lines)
            if (line.StartsWith("worktree "))
                currentWorktreePath = line["worktree ".Length..].Trim();
            else if (line.StartsWith("branch "))
                if (currentWorktreePath != null)
                {
                    var worktreeName = Path.GetFileName(currentWorktreePath);
                    if (!string.IsNullOrEmpty(worktreeName))
                        dict[worktreeName] = currentWorktreePath;
                }

        return dict;
    }

    /// <inheritdoc />
    public async Task<string> CreateWorktreeAsync(
        string name,
        string? baseBranch = null)
    {
        if (!IsValidWorktreeName(name))
            throw new ArgumentException($"Invalid worktree name: {name}", nameof(name));

        if (!await IsGitRepositoryAsync())
            throw new InvalidOperationException("Not in a git repository");

        var repoRoot = await GetRepositoryRootAsync()
                       ?? throw new InvalidOperationException("Could not determine git repository root");

        var existing = await GetExistingWorktreesAsync();
        if (existing.TryGetValue(name, out var existingPath))
            return existingPath;

        var repoParent = Path.GetDirectoryName(repoRoot) ??
                         throw new InvalidOperationException("Could not determine repository parent directory");

        var repoName = Path.GetFileName(repoRoot);
        var worktreePath = Path.Combine(repoParent, $"{repoName}-{name}");
        if (fileSystem.DirectoryExists(worktreePath))
            throw new InvalidOperationException($"Directory already exists: {worktreePath}");

        var command = $"worktree add \"{worktreePath}\"" +
                      (string.IsNullOrEmpty(baseBranch) ? "" : $" \"{baseBranch}\"");
        var result = await process.RunAsync("git", command, Environment.CurrentDirectory, 60000);
        if (!result.IsSuccess)
            throw new InvalidOperationException($"Failed to create worktree: {result.StandardError}");

        logger.Info($"Created worktree '{name}' at: {worktreePath}");
        return worktreePath;
    }

    [GeneratedRegex("^[a-zA-Z0-9_-]+$")]
    private static partial Regex WorktreeNameRegex();

    private bool IsValidWorktreeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (!WorktreeNameRegex().IsMatch(name))
            return false;
        if (name.Length < 1 || name.Length > 50)
            return false;
        var reservedNames = new[] { "HEAD", "ORIG_HEAD", "FETCH_HEAD", "MERGE_HEAD", "refs", "objects", "hooks" };
        if (reservedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            return false;
        return true;
    }
}
