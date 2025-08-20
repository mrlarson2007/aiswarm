using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace AgentLauncher.Services;

/// <summary>
/// Background service that monitors agent health and terminates unresponsive agents
/// </summary>
public class AgentMonitoringService : BackgroundService
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUnresponsiveAgentsAsync();
                await Task.Delay(TimeSpan.FromMinutes(_config.CheckIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception)
            {
                // Log error but continue monitoring
                // TODO: Add proper logging once logger infrastructure is available
                await Task.Delay(TimeSpan.FromMinutes(_config.CheckIntervalMinutes), stoppingToken);
            }
        }
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