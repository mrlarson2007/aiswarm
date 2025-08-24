using System.ComponentModel;
using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class AgentManagementMcpTool(
    IDatabaseScopeService scopeService, 
    ITimeService timeService,
    IContextService contextService,
    IGitService gitService,
    IGeminiService geminiService,
    IFileSystemService fileSystemService,
    ILocalAgentService localAgentService,
    IEnvironmentService environmentService,
    IAppLogger logger)
{
    [McpServerTool(Name = "list_agents")]
    [Description("List available agents with optional persona filter")]
    public async Task<ListAgentsResult> ListAgentsAsync(
        [Description("Optional persona filter (implementer, reviewer, planner, etc.)")] string? personaFilter = null)
    {
        using var scope = scopeService.CreateReadScope();
        
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
        [Description("Optional worktree name for the agent")] string? worktreeName = null)
    {
        if (string.IsNullOrWhiteSpace(persona))
        {
            return LaunchAgentResult.Failure("Persona is required");
        }

        // Validate agent type using context service
        if (!contextService.IsValidAgentType(persona))
        {
            return LaunchAgentResult.Failure($"Invalid agent type: {persona}");
        }

        // Check if we're in a git repository
        if (!await gitService.IsGitRepositoryAsync())
        {
            return LaunchAgentResult.Failure("Must be run from within a git repository");
        }

        try
        {
            // Get repository root
            var repositoryRoot = await gitService.GetRepositoryRootAsync();
            if (repositoryRoot == null)
            {
                return LaunchAgentResult.Failure("Could not determine repository root");
            }

            // Create worktree if specified
            string workingDirectory = repositoryRoot;
            if (!string.IsNullOrWhiteSpace(worktreeName))
            {
                workingDirectory = await gitService.CreateWorktreeAsync(worktreeName);
            }

            // Register agent in database
            var registrationRequest = new AgentRegistrationRequest
            {
                PersonaId = persona,
                AgentType = persona,
                WorkingDirectory = workingDirectory,
                Model = model,
                WorktreeName = worktreeName
            };

            var agentId = await localAgentService.RegisterAgentAsync(registrationRequest);

            // Create context file
            var contextPath = await contextService.CreateContextFile(persona, workingDirectory);

            // Launch Gemini with agent settings
            var agentSettings = new AgentSettings
            {
                AgentId = agentId,
                McpServerUrl = environmentService.GetEnvironmentVariable("MCP_SERVER_URL") ?? "http://localhost:5000/sse"
            };

            var success = await geminiService.LaunchInteractiveAsync(
                contextPath,
                model,
                workingDirectory,
                agentSettings);

            if (!success)
            {
                return LaunchAgentResult.Failure("Failed to launch Gemini interactive session");
            }

            // TODO: Get real process ID from Gemini service
            // Currently IGeminiService.LaunchInteractiveAsync only returns success boolean
            // Need to modify service to return process information or track it separately
            return LaunchAgentResult.SuccessWith(agentId, null);
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to launch agent: {ex.Message}");
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

        using var scope = scopeService.CreateWriteScope();
        var agent = await scope.Agents.FindAsync(agentId);
        
        if (agent == null)
        {
            return KillAgentResult.Failure($"Agent not found: {agentId}");
        }
                                                                     
        // Kill the agent and update the database
        agent.Kill(timeService.UtcNow);
        await scope.SaveChangesAsync();
        scope.Complete();

        return KillAgentResult.SuccessWith(agentId);
    }
}