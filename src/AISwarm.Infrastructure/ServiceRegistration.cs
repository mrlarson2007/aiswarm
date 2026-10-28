using System.Threading.Channels;
using AISwarm.Infrastructure.Eventing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AISwarm.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services)
    {
        services.AddSingleton<IGitService, GitService>();
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
        services.AddScoped<IMemoryService, MemoryService>();

        // Add Memory Event Bus
        services.AddSingleton<IEventBus<MemoryEventType, IMemoryLifecyclePayload>>(_ =>
            new InMemoryEventBus<MemoryEventType, IMemoryLifecyclePayload>());

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
        if (!string.IsNullOrWhiteSpace(capacityString) && int.TryParse(capacityString, out var parsedCap) &&
            parsedCap > 0)
            capacity = parsedCap;
        var fullModeString = configuration["EventBus:FullMode"]; // Wait | DropOldest | DropNewest | DropWrite

        if (capacity is > 0)
        {
            var mode = BoundedChannelFullMode.Wait;
            if (!string.IsNullOrWhiteSpace(fullModeString)
                && Enum.TryParse<BoundedChannelFullMode>(fullModeString, true, out var parsed))
                mode = parsed;

            var options = new BoundedChannelOptions(capacity.Value)
            {
                FullMode = mode,
                SingleReader = false,
                SingleWriter = false
            };

            // Replace the default IEventBus with a bounded instance
            services.AddSingleton<IEventBus<MemoryEventType, IMemoryLifecyclePayload>>(_ =>
                new InMemoryEventBus<MemoryEventType, IMemoryLifecyclePayload>(options));
        }
        else
        {
            services.AddSingleton<IEventBus<MemoryEventType, IMemoryLifecyclePayload>>(_ =>
                new InMemoryEventBus<MemoryEventType, IMemoryLifecyclePayload>());
        }

        return services;
    }
}
