using AgentLauncher.Services.External;
using Microsoft.Extensions.DependencyInjection;
using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Services;

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
        services.AddSingleton<IOperatingSystemService, OperatingSystemService>();
        services.AddSingleton<Terminals.IInteractiveTerminalService>(sp =>
        {
            var os = sp.GetRequiredService<IOperatingSystemService>();
            var proc = sp.GetRequiredService<IProcessLauncher>();
            return os.IsWindows()
                ? new Terminals.WindowsTerminalService(proc)
                : new Terminals.UnixTerminalService(proc);
        });
        services.AddSingleton<IGeminiService, GeminiService>();

        // Data layer services
        services.AddSingleton<ITimeService, SystemTimeService>();
        services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();
        services.AddSingleton<ILocalAgentService, LocalAgentService>();

        // Command handlers
        services.AddTransient<Commands.LaunchAgentCommandHandler>();
        services.AddTransient<Commands.ListAgentsCommandHandler>();
        services.AddTransient<Commands.ListWorktreesCommandHandler>();
        services.AddTransient<Commands.InitCommandHandler>();

        return services;
    }
}
