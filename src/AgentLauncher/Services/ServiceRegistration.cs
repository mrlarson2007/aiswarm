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

        // Command handlers
        services.AddTransient<Commands.LaunchAgentCommandHandler>();
        services.AddTransient<Commands.ListAgentsCommandHandler>();
        services.AddTransient<Commands.ListWorktreesCommandHandler>();

        return services;
    }
}
