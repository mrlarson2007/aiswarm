using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AgentLauncher;

public static partial class GitManager
{
    [GeneratedRegex("^[a-zA-Z0-9_-]+$")]
    private static partial Regex WorktreeNameRegex();

    /// <summary>
    /// Check if the current directory is within a git repository
    /// </summary>
    /// <returns>True if in a git repository, false otherwise</returns>
    public static async Task<bool> IsGitRepositoryAsync()
    {
        try
        {
            var result = await RunGitCommandAsync("rev-parse --git-dir");
            return result.IsSuccess;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the root directory of the git repository
    /// </summary>
    /// <returns>The git repository root path, or null if not in a git repo</returns>
    public static async Task<string?> GetRepositoryRootAsync()
    {
        try
        {
            var result = await RunGitCommandAsync("rev-parse --show-toplevel");
            return result.IsSuccess ? result.Output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Validate a worktree name
    /// </summary>
    /// <param name="name">The worktree name to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidWorktreeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Check for valid characters (alphanumeric, underscore, dash)
        if (!WorktreeNameRegex().IsMatch(name))
            return false;

        // Check length (reasonable limits)
        if (name.Length < 1 || name.Length > 50)
            return false;

        // Don't allow names that could conflict with git internals
        var reservedNames = new[] { "HEAD", "ORIG_HEAD", "FETCH_HEAD", "MERGE_HEAD", "refs", "objects", "hooks" };
        if (reservedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            return false;

        return true;
    }

    /// <summary>
    /// Get a list of existing worktrees
    /// </summary>
    /// <returns>Dictionary of worktree name to path</returns>
    public static async Task<Dictionary<string, string>> GetExistingWorktreesAsync()
    {
        var worktrees = new Dictionary<string, string>();

        try
        {
            var result = await RunGitCommandAsync("worktree list --porcelain");
            if (!result.IsSuccess)
                return worktrees;

            var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? currentWorktreePath = null;
            string? currentBranch = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("worktree "))
                {
                    currentWorktreePath = line["worktree ".Length..].Trim();
                }
                else if (line.StartsWith("branch "))
                {
                    currentBranch = line["branch ".Length..].Trim();

                    // Extract worktree name from path
                    if (currentWorktreePath != null)
                    {
                        var worktreeName = Path.GetFileName(currentWorktreePath);
                        if (!string.IsNullOrEmpty(worktreeName))
                        {
                            worktrees[worktreeName] = currentWorktreePath;
                        }
                    }
                }
                else if (string.IsNullOrWhiteSpace(line))
                {
                    // Reset for next worktree
                    currentWorktreePath = null;
                    currentBranch = null;
                }
            }
        }
        catch
        {
            // Return empty dictionary if there's an error
        }

        return worktrees;
    }

    /// <summary>
    /// Create a new git worktree
    /// </summary>
    /// <param name="name">The name for the worktree</param>
    /// <param name="baseBranch">The base branch to create from (defaults to current branch)</param>
    /// <returns>The path to the created worktree</returns>
    public static async Task<string> CreateWorktreeAsync(
        string name,
        string? baseBranch = null)
    {
        if (!IsValidWorktreeName(name))
            throw new ArgumentException($"Invalid worktree name: {name}", nameof(name));

        // Check if we're in a git repository
        if (!await IsGitRepositoryAsync())
            throw new InvalidOperationException("Not in a git repository");

        // Get repository root to create worktree alongside it
        var repoRoot = await GetRepositoryRootAsync() ?? throw new InvalidOperationException("Could not determine git repository root");

        // Check if worktree already exists
        var existingWorktrees = await GetExistingWorktreesAsync();
        if (existingWorktrees.TryGetValue(name, out var existingPath))
            throw new InvalidOperationException($"Worktree '{name}' already exists at: {existingPath}");

        // Create worktree path (sibling to main repo)
        var repoParent = Path.GetDirectoryName(repoRoot) ?? throw new InvalidOperationException("Could not determine repository parent directory");
        var repoName = Path.GetFileName(repoRoot);
        var worktreePath = Path.Combine(repoParent, $"{repoName}-{name}");

        // Ensure the worktree path doesn't already exist
        if (Directory.Exists(worktreePath))
            throw new InvalidOperationException($"Directory already exists: {worktreePath}");

        // Build git worktree add command
        var command = $"worktree add \"{worktreePath}\"";
        if (!string.IsNullOrEmpty(baseBranch))
        {
            command += $" \"{baseBranch}\"";
        }

        // Create the worktree
        var result = await RunGitCommandAsync(command);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to create worktree: {result.Error}");
        }

        Console.WriteLine($"Created worktree '{name}' at: {worktreePath}");
        return worktreePath;
    }

    /// <summary>
    /// Remove a git worktree
    /// </summary>
    /// <param name="name">The name of the worktree to remove</param>
    /// <returns>True if removed successfully, false if not found</returns>
    public static async Task<bool> RemoveWorktreeAsync(
        string name)
    {
        var existingWorktrees = await GetExistingWorktreesAsync();
        if (!existingWorktrees.TryGetValue(name, out var worktreePath))
            return false;

        try
        {
            var result = await RunGitCommandAsync($"worktree remove \"{worktreePath}\"");
            if (result.IsSuccess)
            {
                Console.WriteLine($"Removed worktree '{name}' from: {worktreePath}");
                return true;
            }
            else
            {
                Console.WriteLine($"Failed to remove worktree '{name}': {result.Error}");
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Run a git command and return the result
    /// </summary>
    /// <param name="arguments">The git command arguments</param>
    /// <returns>Command result with output and success status</returns>
    private static async Task<GitCommandResult> RunGitCommandAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;

            return new GitCommandResult
            {
                IsSuccess = process.ExitCode == 0,
                Output = output,
                Error = error,
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            return new GitCommandResult
            {
                IsSuccess = false,
                Output = "",
                Error = ex.Message,
                ExitCode = -1
            };
        }
    }

    /// <summary>
    /// Result of a git command execution
    /// </summary>
    private record GitCommandResult
    {
        public bool IsSuccess
        {
            get; init;
        }
        public string Output { get; init; } = "";
        public string Error { get; init; } = "";
        public int ExitCode
        {
            get; init;
        }
    }
}
