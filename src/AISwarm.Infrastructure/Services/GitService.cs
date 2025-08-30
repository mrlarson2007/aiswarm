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
                    var fullWorktreeName = Path.GetFileName(currentWorktreePath);
                    if (!string.IsNullOrEmpty(fullWorktreeName))
                    {
                        // Handle both full name and suffix after repo name
                        // For "aiswarm-event-subscription-dev", try both:
                        // 1. Full name: "aiswarm-event-subscription-dev"
                        // 2. Suffix: "event-subscription-dev"
                        dict[fullWorktreeName] = currentWorktreePath;

                        var repoNamePrefix = "aiswarm-";
                        if (fullWorktreeName.StartsWith(repoNamePrefix))
                        {
                            var suffixName = fullWorktreeName.Substring(repoNamePrefix.Length);
                            dict[suffixName] = currentWorktreePath;
                        }
                    }
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

        // If directory exists, check if it's already listed as a worktree
        if (fileSystem.DirectoryExists(worktreePath))
        {
            if (existing.ContainsValue(worktreePath))
            {
                logger.Info($"Worktree '{name}' already exists at: {worktreePath}");
                return worktreePath;
            }

            // Directory exists but is not a worktree - this should be allowed for multiple agents
            logger.Info($"Directory exists but is not a git worktree, will create worktree: {worktreePath}");
        }

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
