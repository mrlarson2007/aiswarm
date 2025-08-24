using System.ComponentModel;
using AISwarm.DataLayer;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class AgentManagementMcpTool(IDatabaseScopeService scopeService)
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
}