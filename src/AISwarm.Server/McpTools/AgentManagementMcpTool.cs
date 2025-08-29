using System.ComponentModel;
using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Server.Entities;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class AgentManagementMcpTool(
    IDatabaseScopeService scopeService,
    IContextService contextService,
    IGitService gitService,
    IGeminiService geminiService,
    ILocalAgentService localAgentService,
    IEnvironmentService environmentService,
    IAppLogger logger)
{
    [McpServerTool(Name = "list_agents")]
    [Description("List available agents with optional persona filter")]
    public async Task<ListAgentsResult> ListAgentsAsync(
        [Description("Optional persona filter (implementer, reviewer, planner, etc.)")] string? personaFilter = null)
    {
        using var scope = scopeService.GetReadScope();

        var query = scope.Agents.AsQueryable();

        if (!string.IsNullOrEmpty(personaFilter))
        {
            query = query.Where(a => a.PersonaId == personaFilter);
        }

        var agents = await query
            .Select(a => new AgentInfo
            {
                AgentId = a.Id,
                PersonaId = a.PersonaId,
                Status = a.Status.ToString(),
                ProcessId = a.ProcessId,
                RegisteredAt = a.RegisteredAt,
                LastHeartbeat = a.LastHeartbeat,
                WorkingDirectory = a.WorkingDirectory,
                Model = a.Model,
                WorktreeName = a.WorktreeName
            }).ToArrayAsync();

        return ListAgentsResult.SuccessWith(agents);
    }

    [McpServerTool(Name = "launch_agent")]
    [Description("Launch a new agent with specified persona")]
    public async Task<LaunchAgentResult> LaunchAgentAsync(
        [Description("Agent persona (implementer, reviewer, planner, etc.)")] string persona,
        [Description("Description of what the agent should accomplish")] string description,
        [Description("Optional model to use")] string? model = null,
        [Description("Optional worktree name for the agent")] string? worktreeName = null,
        [Description("Bypass permission prompts (use --yolo flag)")] bool yolo = false)
    {
        logger.Info($"[MCP] LaunchAgentAsync started - persona: {persona}, model: {model}, worktree: {worktreeName}");

        if (string.IsNullOrWhiteSpace(persona))
        {
            logger.Warn("[MCP] LaunchAgentAsync failed - persona is required");
            return LaunchAgentResult.Failure("Persona is required");
        }

        try
        {
            // Validate agent type using context service
            logger.Info("[MCP] Validating agent type...");
            if (!contextService.IsValidAgentType(persona))
            {
                logger.Warn($"[MCP] LaunchAgentAsync failed - invalid agent type: {persona}");
                return LaunchAgentResult.Failure($"Invalid agent type: {persona}");
            }
            logger.Info("[MCP] Agent type validation passed");

            // Check if we're in a git repository
            logger.Info("[MCP] Checking git repository...");
            if (!await gitService.IsGitRepositoryAsync())
            {
                logger.Warn("[MCP] LaunchAgentAsync failed - not in git repository");
                return LaunchAgentResult.Failure("Must be run from within a git repository");
            }
            logger.Info("[MCP] Git repository check passed");

            // Get repository root
            logger.Info("[MCP] Getting repository root...");
            var repositoryRoot = await gitService.GetRepositoryRootAsync();
            if (repositoryRoot == null)
            {
                logger.Warn("[MCP] LaunchAgentAsync failed - could not determine repository root");
                return LaunchAgentResult.Failure("Could not determine repository root");
            }
            logger.Info($"[MCP] Repository root: {repositoryRoot}");

            // Create worktree if specified
            string workingDirectory = repositoryRoot;
            if (!string.IsNullOrWhiteSpace(worktreeName))
            {
                logger.Info($"[MCP] Creating worktree: {worktreeName}...");
                workingDirectory = await gitService.CreateWorktreeAsync(worktreeName);
                logger.Info($"[MCP] Worktree created: {workingDirectory}");
            }
            else
            {
                logger.Info("[MCP] No worktree specified, using repository root");
            }

            // Register agent in database
            logger.Info("[MCP] Registering agent in database...");
            var registrationRequest = new AgentRegistrationRequest
            {
                PersonaId = persona,
                WorkingDirectory = workingDirectory,
                Model = model,
                WorktreeName = worktreeName
            };

            var agentId = await localAgentService.RegisterAgentAsync(registrationRequest);
            logger.Info($"[MCP] Agent registered with ID: {agentId}");

            // Create context file with MCP tool instructions
            logger.Info("[MCP] Creating context file with agent coordination instructions...");
            var contextPath = await contextService.CreateContextFileWithAgentId(persona, workingDirectory, agentId);
            logger.Info($"[MCP] Context file created with MCP tools: {contextPath}");

            // Launch Gemini with agent settings
            logger.Info("[MCP] Preparing to launch Gemini interactive session...");
            var agentSettings = new AgentSettings
            {
                AgentId = agentId,
                McpServerUrl = environmentService.GetEnvironmentVariable("MCP_SERVER_URL")
                               ?? "http://localhost:5000/sse"
            };
            logger.Info($"[MCP] Agent settings - ID: {agentSettings.AgentId}, MCP URL: {agentSettings.McpServerUrl}");

            logger.Info("[MCP] Launching Gemini interactive session...");
            var success = await geminiService.LaunchInteractiveAsync(
                contextPath,
                model,
                workingDirectory,
                agentSettings,
                yolo);

            if (!success)
            {
                logger.Error("[MCP] Failed to launch Gemini interactive session");
                return LaunchAgentResult.Failure("Failed to launch Gemini interactive session");
            }

            logger.Info($"[MCP] LaunchAgentAsync completed successfully - agent ID: {agentId}");
            return LaunchAgentResult.SuccessWith(agentId);
        }
        catch (Exception ex)
        {
            logger.Error($"[MCP] LaunchAgentAsync failed with exception: {ex.Message}");
            logger.Error($"[MCP] Exception details: {ex}");
            return LaunchAgentResult.Failure($"Failed to launch agent: {ex.Message}");
        }
    }

    [McpServerTool(Name = "kill_agent")]
    [Description("Stop and kill an agent by ID")]
    public async Task<KillAgentResult> KillAgentAsync(
        [Description("ID of the agent to kill")] string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return KillAgentResult.Failure("Agent ID is required");
        }

        try
        {
            // Check if agent exists first
            using var scope = scopeService.GetReadScope();
            var agent = await scope.Agents.FindAsync(agentId);
            if (agent == null)
            {
                return KillAgentResult.Failure($"Agent not found: {agentId}");
            }

            // Use the local agent service to handle process termination and database updates
            await localAgentService.KillAgentAsync(agentId);

            return KillAgentResult.SuccessWith(agentId);
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to kill agent {agentId}: {ex.Message}");
            return KillAgentResult.Failure($"Failed to kill agent: {ex.Message}");
        }
    }
}
