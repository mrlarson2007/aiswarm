using Microsoft.Extensions.DependencyInjection;
using AISwarm.DataLayer;
using Microsoft.Extensions.Configuration;
using AISwarm.Infrastructure;

namespace AgentLauncher.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddAgentLauncherServices(this IServiceCollection services)
    {
        // External / infrastructure
        services.AddInfrastructureServices();

        // Use centralized data layer services with proper database initialization
        var configuration = new ConfigurationBuilder().Build();
        services.AddDataLayerServices(configuration);

        // Command handlers
        services.AddTransient<Commands.LaunchAgentCommandHandler>();
        services.AddTransient<Commands.ListAgentsCommandHandler>();
        services.AddTransient<Commands.ListWorktreesCommandHandler>();
        services.AddTransient<Commands.InitCommandHandler>();

        return services;
    }
}
