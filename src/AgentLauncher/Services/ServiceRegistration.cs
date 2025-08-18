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
            var proc = sp.GetRequiredService<External.IProcessLauncher>();
            if (os.IsWindows()) return new Terminals.WindowsTerminalService(proc);
            if (os.IsMacOS()) return new Terminals.MacTerminalService(proc);
            return new Terminals.LinuxTerminalService(proc);
        });
        services.AddSingleton<IGeminiService, GeminiService>();

    // Command handlers
    services.AddTransient<Commands.LaunchAgentCommandHandler>();
    services.AddTransient<Commands.ListAgentsCommandHandler>();
    services.AddTransient<Commands.ListWorktreesCommandHandler>();

        return services;
    }
}
