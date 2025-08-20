using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentLauncher.Services;

/// <summary>
/// Background service that monitors agent health and terminates unresponsive agents
/// </summary>
public class AgentMonitoringService
{
    private readonly IDatabaseScopeService _scopeService;
    private readonly ILocalAgentService _localAgentService;
    private readonly ITimeService _timeService;
    private readonly AgentMonitoringConfiguration _config;

    public AgentMonitoringService(
        IDatabaseScopeService scopeService,
        ILocalAgentService localAgentService,
        ITimeService timeService,
        AgentMonitoringConfiguration config)
    {
        _scopeService = scopeService;
        _localAgentService = localAgentService;
        _timeService = timeService;
        _config = config;
    }

    /// <summary>
    /// Check for unresponsive agents and terminate them
    /// </summary>
    public async Task CheckForUnresponsiveAgentsAsync()
    {
        using var scope = _scopeService.CreateReadScope();
        
        var timeoutThreshold = _timeService.UtcNow.AddMinutes(-_config.HeartbeatTimeoutMinutes);
        
        var unresponsiveAgents = await scope.Agents
            .Where(a => a.Status == AgentStatus.Running && 
                       a.LastHeartbeat < timeoutThreshold)
            .ToListAsync();

        foreach (var agent in unresponsiveAgents)
        {
            await _localAgentService.KillAgentAsync(agent.Id);
        }
    }
}