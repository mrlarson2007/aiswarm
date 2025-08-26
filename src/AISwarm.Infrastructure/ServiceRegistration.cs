using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Threading.Channels;
using AISwarm.Infrastructure.Eventing;

namespace AISwarm.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services)
    {
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IGeminiService, GeminiService>();
        services.AddSingleton<ILocalAgentService, LocalAgentService>();
        services.AddSingleton<IContextService, ContextService>();
        services.AddSingleton<IProcessTerminationService, ProcessTerminationService>();
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

        // Eventing defaults (unbounded) and high-level notification service
        services.AddSingleton<IEventBus>(_ => new InMemoryEventBus());
        services.AddSingleton<IWorkItemNotificationService, WorkItemNotificationService>();
        return services;
    }

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Base services
        services.AddInfrastructureServices();

        // Optional EventBus configuration
        int? capacity = null;
        var capacityString = configuration["EventBus:Capacity"];
        if (!string.IsNullOrWhiteSpace(capacityString) && int.TryParse(capacityString, out var parsedCap) && parsedCap > 0)
        {
            capacity = parsedCap;
        }
        var fullModeString = configuration["EventBus:FullMode"]; // Wait | DropOldest | DropNewest | DropWrite

        if (capacity is > 0)
        {
            var mode = BoundedChannelFullMode.Wait;
            if (!string.IsNullOrWhiteSpace(fullModeString)
                && Enum.TryParse<BoundedChannelFullMode>(fullModeString, ignoreCase: true, out var parsed))
            {
                mode = parsed;
            }

            var options = new BoundedChannelOptions(capacity.Value)
            {
                FullMode = mode,
                SingleReader = false,
                SingleWriter = false
            };

            // Replace the default IEventBus with a bounded instance
            services.AddSingleton<IEventBus>(_ => new InMemoryEventBus(options));
        }

        return services;
    }
}
