using AISwarm.Infrastructure.OpenCode;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AISwarm.Server.Delegation;

public static class ServiceRegistration
{
    public static IServiceCollection AddOpenCodeDelegation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IOpenCodeCli>(_ =>
            new OpenCodeCli(configuration["OpenCode:Executable"] ?? "opencode"));
        services.AddSingleton<DelegationService>();
        services.AddHostedService(provider => provider.GetRequiredService<DelegationService>());
        services.AddSingleton(_ => new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false
        }));
        services.AddSingleton(provider => new LocalLifecycleHook(
            provider.GetRequiredService<HttpClient>(), configuration["OpenCode:HookEndpoint"]));
        return services;
    }
}
