using AgentLauncher.Services;
using AgentLauncher.Services.Logging;

namespace AgentLauncher.Commands;

/// <summary>
/// Handles launching an agent conversation (interactive) with optional worktree creation.
/// </summary>
public class LaunchAgentCommandHandler(
    IContextService contextService,
    IAppLogger logger,
    IEnvironmentService environment
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
            logger.Info($"Working directory: {workingDirectory}");
            var planned = Path.Combine(workingDirectory, agentType + "_context.md");
            logger.Info($"Planned context file: {planned}");
            var manual = $"gemini{(model != null ? $" -m {model}" : string.Empty)} -i \"{planned}\"";
            logger.Info("Manual launch: " + manual);
            return;
        }

        logger.Info($"Creating context file for '{agentType}'...");
        _ = await contextService.CreateContextFile(agentType, directory ?? environment.CurrentDirectory);
    }
}
