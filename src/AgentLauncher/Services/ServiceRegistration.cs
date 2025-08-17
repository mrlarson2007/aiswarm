using Microsoft.Extensions.DependencyInjection;

namespace AgentLauncher.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddAgentLauncherServices(this IServiceCollection services)
    {
        // External / infrastructure
        services.AddSingleton<External.IProcessLauncher, External.ProcessLauncher>();
        services.AddSingleton<Logging.IAppLogger, Logging.ConsoleAppLogger>();

        // Core services (placeholders until refactor complete)
        services.AddSingleton<IContextService, ContextService>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IGeminiService, GeminiService>();

        return services;
    }
}
