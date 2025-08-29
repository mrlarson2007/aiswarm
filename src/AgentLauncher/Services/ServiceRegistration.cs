using AgentLauncher.Commands;
using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentLauncher.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddAgentLauncherServices(this IServiceCollection services)
    {
        // External / infrastructure
        var configuration = new ConfigurationBuilder().Build();
        services.AddInfrastructureServices(configuration);

        // Use centralized data layer services with proper database initialization
        services.AddDataLayerServices(configuration);

        // Command handlers
        services.AddTransient<LaunchAgentCommandHandler>();
        services.AddTransient<ListAgentsCommandHandler>();
        services.AddTransient<ListWorktreesCommandHandler>();
        services.AddTransient<InitCommandHandler>();

        return services;
    }
}
