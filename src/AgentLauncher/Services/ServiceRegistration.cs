using AgentLauncher.Services.External;
using Microsoft.Extensions.DependencyInjection;

namespace AgentLauncher.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddAgentLauncherServices(this IServiceCollection services)
    {
        // External / infrastructure
        services.AddSingleton<External.IProcessLauncher, External.ProcessLauncher>();
        services.AddSingleton<Logging.IAppLogger, Logging.ConsoleAppLogger>();
        services.AddSingleton<IEnvironmentService, EnvironmentService>();

        // Core services (placeholders until refactor complete)
        services.AddSingleton<IContextService, ContextService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IGeminiService, GeminiService>();

        // Command handlers
        services.AddTransient<Commands.LaunchAgentCommandHandler>();

        return services;
    }
}
