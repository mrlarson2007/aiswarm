using AgentLauncher.Services;
using AISwarm.Infrastructure;
using AgentLauncher.Services.Logging;
using AgentLauncher.Models;

namespace AgentLauncher.Commands;

/// <summary>
/// Handles launching an agent conversation (interactive) with optional worktree creation.
/// </summary>
public class LaunchAgentCommandHandler(
    IContextService contextService,
    IAppLogger logger,
    IEnvironmentService environment,
    IGitService gitService,
    IGeminiService geminiService,
    IFileSystemService fileSystemService,
    ILocalAgentService? localAgentService = null
)
{
    public async Task<bool> RunAsync(
        string agentType,
        string? model,
        string? worktree,
        string? directory,
        bool dryRun,
        bool monitor = false
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
            logger.Info($"Monitor: {monitor}");
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

        string? agentId = null;
        if (monitor && localAgentService != null)
        {
            try
            {
                logger.Info("Registering agent in database for monitoring...");
                var request = new AgentRegistrationRequest
                {
                    PersonaId = agentType, // Using agentType as PersonaId for now
                    AgentType = agentType,
                    WorkingDirectory = workDir,
                    Model = model,
                    WorktreeName = worktree
                };
                
                agentId = await localAgentService.RegisterAgentAsync(request);
                logger.Info($"Registered agent with ID: {agentId}");
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to register agent for monitoring: {ex.Message}");
                logger.Info("Continuing without monitoring...");
            }
        }

        string contextPath;
        try
        {
            logger.Info($"Creating context file for '{agentType}' in '{workDir}'...");
            contextPath = await contextService.CreateContextFile(agentType, workDir);
            
            // If monitoring is enabled, append agent ID information to context
            if (monitor && agentId != null)
            {
                await AppendAgentIdToContextAsync(contextPath, agentId);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to create context file: {ex.Message}");
            return false;
        }

        try
        {
            logger.Info("Launching Gemini interactive session...");
            
            bool success;
            if (monitor && agentId != null)
            {
                // Configure Gemini with agent settings for MCP communication
                logger.Info("Configuring Gemini with agent settings");
                var agentSettings = new AgentSettings
                {
                    AgentId = agentId,
                    McpServerUrl = "http://localhost:8080" // TODO: Make configurable
                };
                success = await geminiService.LaunchInteractiveAsync(
                    contextPath, 
                    model, 
                    workDir, 
                    agentSettings);
            }
            else
            {
                success = await geminiService.LaunchInteractiveAsync(
                    contextPath, 
                    model, 
                    workDir);
            }
            
            if (!success)
            {
                logger.Error("Failed to launch Gemini interactive session");
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Gemini launch failed: {ex.Message}");
            return false;
        }
        return true;
    }

    private async Task AppendAgentIdToContextAsync(string contextPath, string agentId)
    {
        var agentInformation = $@"

## Agent Configuration

You are Agent ID: {agentId}

### Available MCP Tools
You have access to Model Context Protocol (MCP) tools that allow you to:
- Request your next pending task: Use the 'get_next_task' MCP tool to retrieve tasks assigned specifically to you
- Create new tasks: Use the 'create_task' MCP tool to break down work or create subtasks

### Task Management Workflow
1. Start each work session by calling 'get_next_task' to see if you have pending work
2. The tool will return reinforcing prompts to check again - follow these suggestions
3. When you complete work, you can create follow-up tasks using 'create_task'
4. Always include your agent ID ({agentId}) when working with tasks

";
        await fileSystemService.AppendAllTextAsync(contextPath, agentInformation);
    }
}
