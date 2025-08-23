using Microsoft.Extensions.DependencyInjection;

namespace AISwarm.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services)
    {
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<IAppLogger, ConsoleAppLogger>();
        services.AddSingleton<IEnvironmentService, EnvironmentService>();
        services.AddSingleton<IOperatingSystemService, OperatingSystemService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IInteractiveTerminalService>(sp =>
        {
            var os = sp.GetRequiredService<IOperatingSystemService>();
            var proc = sp.GetRequiredService<IProcessLauncher>();
            return os.IsWindows()
                ? new WindowsTerminalService(proc)
                : new UnixTerminalService(proc);
        });
        services.AddSingleton<ITimeService, SystemTimeService>();
        return services;
    }
}
