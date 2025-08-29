using System.Threading.Channels;
using AISwarm.Infrastructure.Eventing;
using AISwarm.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AISwarm.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services)
    {
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IGeminiService, GeminiService>();
        services.AddSingleton<IAgentStateService, AgentStateService>();
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
        services.AddScoped<IMemoryService, MemoryService>();

        // Eventing defaults (unbounded) and high-level notification service
        services.AddSingleton<IEventBus<TaskEventType, ITaskLifecyclePayload>>(_ =>
            new InMemoryEventBus<TaskEventType, ITaskLifecyclePayload>());
        services.AddSingleton<IWorkItemNotificationService, WorkItemNotificationService>();
        services.AddSingleton<IEventBus<AgentEventType, IAgentLifecyclePayload>>(_ =>
            new InMemoryEventBus<AgentEventType, IAgentLifecyclePayload>());
        services.AddSingleton<IAgentNotificationService, AgentNotificationService>();

        // Event logging service
        services.AddSingleton<IEventLoggerService, DatabaseEventLoggerService>();

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
            parsedCap > 0) capacity = parsedCap;
        var fullModeString = configuration["EventBus:FullMode"]; // Wait | DropOldest | DropNewest | DropWrite

        if (capacity is > 0)
        {
            var mode = BoundedChannelFullMode.Wait;
            if (!string.IsNullOrWhiteSpace(fullModeString)
                && Enum.TryParse<BoundedChannelFullMode>(fullModeString, true, out var parsed))
                mode = parsed;

            var options = new BoundedChannelOptions(capacity.Value)
            {
                FullMode = mode, SingleReader = false, SingleWriter = false
            };

            // Replace the default IEventBus with a bounded instance
            services.AddSingleton<IEventBus<TaskEventType, ITaskLifecyclePayload>>(_ =>
                new InMemoryEventBus<TaskEventType, ITaskLifecyclePayload>(options));
            services.AddSingleton<IEventBus<AgentEventType, IAgentLifecyclePayload>>(_ =>
                new InMemoryEventBus<AgentEventType, IAgentLifecyclePayload>(options));
        }
        else
        {
            services.AddSingleton<IEventBus<TaskEventType, ITaskLifecyclePayload>>(_ =>
                new InMemoryEventBus<TaskEventType, ITaskLifecyclePayload>());
            services.AddSingleton<IEventBus<AgentEventType, IAgentLifecyclePayload>>(_ =>
                new InMemoryEventBus<AgentEventType, IAgentLifecyclePayload>());
        }

        // High-level notification services (after event bus configuration)
        services.AddSingleton<IWorkItemNotificationService, WorkItemNotificationService>();
        services.AddSingleton<IAgentNotificationService, AgentNotificationService>();

        // Event logging service
        services.AddSingleton<IEventLoggerService, DatabaseEventLoggerService>();

        return services;
    }
}
