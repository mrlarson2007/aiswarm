using AgentLauncher.Services;
using AgentLauncher.Services.Logging;

namespace AgentLauncher.Commands;

/// <summary>
/// Handles launching an agent conversation (interactive) with optional worktree creation.
/// </summary>
public class LaunchAgentCommandHandler(
    IContextService contextService,
    IAppLogger logger,
    IEnvironmentService environment,
    IGitService? gitService = null,
    IGeminiService? geminiService = null
)
{
    public async Task RunAsync(
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
            return;
        }

    var workDir = directory ?? environment.CurrentDirectory;
        if (!string.IsNullOrWhiteSpace(worktree))
        {
            if (gitService is null)
            {
                logger.Error("Git service not available; cannot create worktree.");
                return;
            }

            if (!gitService.IsValidWorktreeName(worktree))
            {
                logger.Error($"Invalid worktree name: {worktree}");
                return;
            }

            logger.Info($"Creating worktree '{worktree}'...");
            workDir = await gitService.CreateWorktreeAsync(worktree);
        }

        logger.Info($"Creating context file for '{agentType}' in '{workDir}'...");
        var contextPath = await contextService.CreateContextFile(agentType, workDir);

        if (geminiService is not null)
        {
            logger.Info("Launching Gemini interactive session...");
            await geminiService.LaunchInteractiveAsync(contextPath, model, null);
        }
    }
}
