using AgentLauncher.Services;
using AISwarm.Infrastructure;
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
You have access to Model Context Protocol (MCP) tools that allow you to coordinate tasks:

#### mcp_aiswarm_get_next_task
- **Purpose**: Request your next pending task assigned specifically to you
- **Parameters**: 
  - `agentId`: Your agent ID ({agentId})
- **Usage**: Call this at the start of each work session to check for pending tasks

#### mcp_aiswarm_create_task  
- **Purpose**: Create new tasks for yourself or other agents
- **Parameters**:
  - `agentId`: Target agent ID (use your ID {agentId} for self-assignment, or leave empty for unassigned tasks)
  - `persona`: Full persona markdown content defining the agent's role and behavior
  - `description`: Description of what should be accomplished
  - `priority`: Task priority (Low, Normal, High, Critical) - defaults to Normal
- **Usage**: Break down work or create subtasks for coordination

#### mcp_aiswarm_report_task_completion
- **Purpose**: Report task completion and provide results
- **Parameters**:
  - `taskId`: The ID of the completed task
  - `result`: Summary of work completed and any important findings
- **Usage**: Call when you finish working on a task to update its status

### Task Management Workflow
1. **Start Work Session**: Call `mcp_aiswarm_get_next_task` with your agentId ({agentId}) to check for pending tasks
2. **Work on Task**: Complete the assigned work according to the task description and persona
3. **Report Completion**: Call `mcp_aiswarm_report_task_completion` with the taskId and your results
4. **Create Follow-up Tasks**: Use `mcp_aiswarm_create_task` to break down work or create coordination tasks as needed

### Best Practices
- Always include your agent ID ({agentId}) when calling get_next_task
- Provide detailed results when reporting task completion
- Create specific, actionable tasks when coordinating with other agents
- Use appropriate priority levels for time-sensitive work

";
        await fileSystemService.AppendAllTextAsync(contextPath, agentInformation);
    }
}
