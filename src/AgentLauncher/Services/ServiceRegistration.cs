using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

namespace AgentLauncher.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddAgentLauncherServices(this IServiceCollection services)
    {
        // External / infrastructure
        services.AddInfrastructureServices();

        // Core services (placeholders until refactor complete)
        services.AddSingleton<IContextService, ContextService>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IGeminiService, GeminiService>();

        // Data layer services
        services.AddDataLayerServices();

        services.AddSingleton<ILocalAgentService, LocalAgentService>();
        services.AddSingleton<IProcessTerminationService, ProcessTerminationService>();

        // Background monitoring services
        services.AddSingleton<AgentMonitoringConfiguration>();
        services.AddHostedService<AgentMonitoringService>();

        // Command handlers
        services.AddTransient<Commands.LaunchAgentCommandHandler>();
        services.AddTransient<Commands.ListAgentsCommandHandler>();
        services.AddTransient<Commands.ListWorktreesCommandHandler>();
        services.AddTransient<Commands.InitCommandHandler>();

        return services;
    }
}
