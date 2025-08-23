using AgentLauncher.Services;
using AISwarm.Infrastructure;

namespace AgentLauncher.Commands;

/// <summary>
/// Lists git worktrees in the current repository. Returns true on success. On failure logs error and returns false.
/// </summary>
public class ListWorktreesCommandHandler(
    IGitService git,
    IAppLogger logger)
{
    public async Task<bool> RunAsync()
    {
        try
        {
            var isRepo = await git.IsGitRepositoryAsync();
            if (!isRepo)
            {
                logger.Info("Not in a git repository.\n");
                return true; // Not an error condition for listing.
            }

            var worktrees = await git.GetExistingWorktreesAsync();
            if (worktrees.Count == 0)
            {
                logger.Info("No worktrees found.\nUse --worktree <name> to create a new worktree when launching an agent.\n\nTo create a new worktree:\n  aiswarm --agent <type> --worktree <name>\n\nTo remove a worktree:\n  git worktree remove <path>\n");
                return true;
            }

            logger.Info("Worktrees:\n");
            foreach (var wt in worktrees.OrderBy(w => w.Key))
            {
                logger.Info($" - {wt.Key} => {wt.Value}\n");
            }
            logger.Info("\nTo create a new worktree:\n  aiswarm --agent <type> --worktree <name>\n\nTo remove a worktree:\n  git worktree remove <path>\n");
            return true;
        }
        catch (Exception ex)
        {
            logger.Error($"Error listing worktrees: {ex.Message}\n");
            return false;
        }
    }
}
