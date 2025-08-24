using AISwarm.Infrastructure;

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
            var fileName = Path.GetFileName(planned);
            var manual = $"gemini{(model != null ? $" -m {model}" : string.Empty)} -i \\\"I've just created {fileName}. Please read it for your instructions.\\\"";
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
                    McpServerUrl = "http://localhost:5000/sse" // Updated to use correct port
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
                logger.Error("Gemini interactive session failed to start.");
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
        var mcpToolInstructionsPrompt = $@"## Your Agent ID

Your unique agent ID is: `{agentId}`
**You MUST use this ID for all MCP tool interactions.**

## IMMEDIATE ACTION REQUIRED

**1. Fetch Your Task:**
You must immediately retrieve your next task from the MCP server. Use the following tool call:

`mcp_aiswarm_get_next_task(agentId='{agentId}')`

**2. Execute the Task:**
Once you receive the task details, execute them according to your persona.

**3. Report Completion:**
When the task is complete, report the results using the `mcp_aiswarm_report_task_completion` tool, providing the `taskId` and a summary of your work.

---

## Reference: Available Tools

You have access to the following MCP tools for task management:

### `mcp_aiswarm_get_next_task`

- **Purpose**: Fetches your next assigned task.
- **Parameters**:
  - `agentId`: Your agent ID.

### `mcp_aiswarm_create_task`

- **Purpose**: Creates new tasks for other agents.
- **Parameters**:
  - `agentId`: Target agent ID (or empty for unassigned).
  - `persona`: Full persona markdown for the new agent.
  - `description`: A clear description of the task.
  - `priority`: `Low`, `Normal`, `High`, `Critical`.

### `mcp_aiswarm_report_task_completion`

- **Purpose**: Reports that a task is finished.
- **Parameters**:
  - `taskId`: The ID of the completed task.
  - `result`: A summary of the results.
### Task Management Workflow
1. **Start Work Session**: Call `mcp_aiswarm_get_next_task` with your agentId ({agentId}) to check for pending tasks.
2. **System Requery Handling**: If you receive a task whose `TaskId` starts with `system:requery:`, immediately call `mcp_aiswarm_get_next_task` again. Do not produce other output before re-querying.
3. **Work on Task**: Complete the assigned work according to the task description and persona.
4. **Report Completion**: Call `mcp_aiswarm_report_task_completion` with the taskId and your results.
5. **Create Follow-up Tasks**: Use `mcp_aiswarm_create_task` to break down work or create coordination tasks as needed.

### Best Practices
- Always include your agent ID ({agentId}) when calling get_next_task.
- Provide detailed results when reporting task completion.
- Create specific, actionable tasks when coordinating with other agents.
- Use appropriate priority levels for time-sensitive work.";

        await fileSystemService.AppendAllTextAsync(contextPath, mcpToolInstructionsPrompt);
    }
}
