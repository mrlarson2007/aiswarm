using AISwarm.Infrastructure.OpenCode;
using AISwarm.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AISwarm.Server.Delegation;

public static class ServiceRegistration
{
    public static IServiceCollection AddOpenCodeDelegation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IAgentService>(_ =>
            new OpenCodeCli(configuration["OpenCode:Executable"] ?? "opencode"));
        services.AddSingleton<DelegationService>();
        services.AddHostedService(provider => provider.GetRequiredService<DelegationService>());
        return services;
    }
}
