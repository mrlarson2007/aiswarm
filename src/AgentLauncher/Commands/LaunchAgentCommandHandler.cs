using AgentLauncher.Services;
using AgentLauncher.Services.External;
using AgentLauncher.Services.Logging;

namespace AgentLauncher.Commands;

/// <summary>
/// Handles launching an agent conversation (interactive) with optional worktree creation.
/// </summary>
public class LaunchAgentCommandHandler(
    IContextService contextService,
    IAppLogger logger,
    IEnvironmentService environment,
    IGitService gitService,
    IGeminiService geminiService
)
{
    public async Task<bool> RunAsync(
        string agentType,
        string? model,
        string? worktree,
        string? directory,
        bool dryRun
    )
    {
        if (dryRun)
        {
            var workingDirectory = directory ?? environment.CurrentDirectory;
            logger.Info("Dry run mode: skipping context generation and Gemini launch.");
            logger.Info($"Agent: {agentType}");
            logger.Info($"Model: {model ?? "Gemini CLI default"}");
            logger.Info("Workspace: Current branch");
            if (!string.IsNullOrWhiteSpace(worktree))
            {
                logger.Info($"Worktree (planned): {worktree}");
            }
            logger.Info($"Working directory: {workingDirectory}");
            var planned = Path.Combine(workingDirectory, agentType + "_context.md");
            logger.Info($"Planned context file: {planned}");
            var manual = $"gemini{(model != null ? $" -m {model}" : string.Empty)} -i \"{planned}\"";
            logger.Info("Manual launch: " + manual);
            return true;
        }

        var workDir = directory ?? environment.CurrentDirectory;
        if (!string.IsNullOrWhiteSpace(worktree))
        {
            try
            {
                logger.Info($"Creating worktree '{worktree}'...");
                workDir = await gitService.CreateWorktreeAsync(worktree);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to create worktree '{worktree}': {ex.Message}");
                return false;
            }
        }

        string contextPath;
        try
        {
            logger.Info($"Creating context file for '{agentType}' in '{workDir}'...");
            contextPath = await contextService.CreateContextFile(agentType, workDir);
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to create context file: {ex.Message}");
            return false;
        }

        try
        {
            logger.Info("Launching Gemini interactive session...");
            await geminiService.LaunchInteractiveAsync(contextPath, model, null);
        }
        catch (Exception ex)
        {
            logger.Error($"Gemini launch failed: {ex.Message}");
            return false;
        }
        return true;
    }
}
