using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace AgentLauncher.Services;

/// <summary>
/// Background service that monitors agent health and terminates unresponsive agents
/// </summary>
public class AgentMonitoringService(
    IDatabaseScopeService scopeService,
    ILocalAgentService localAgentService,
    ITimeService timeService,
    AgentMonitoringConfiguration config)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUnresponsiveAgentsAsync();
                await Task.Delay(TimeSpan.FromMinutes(config.CheckIntervalMinutes), stoppingToken);
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
                await Task.Delay(TimeSpan.FromMinutes(config.CheckIntervalMinutes), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Check for unresponsive agents and terminate them
    /// </summary>
    public async Task CheckForUnresponsiveAgentsAsync()
    {
        using var scope = scopeService.CreateReadScope();

        var timeoutThreshold = timeService.UtcNow.AddMinutes(-config.HeartbeatTimeoutMinutes);

        var unresponsiveAgents = await scope.Agents
            .Where(a => a.Status == AgentStatus.Running &&
                       a.LastHeartbeat < timeoutThreshold)
            .ToListAsync();

        foreach (var agent in unresponsiveAgents)
        {
            await localAgentService.KillAgentAsync(agent.Id);
        }
    }
}
