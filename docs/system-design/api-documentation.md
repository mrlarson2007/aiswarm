# API Documentation

This document provides comprehensive API documentation for the AISwarm coordination system, covering new configuration options, service registrations, and architectural changes.

## Configuration Options

### GetNextTaskConfiguration

Configures polling behavior for task retrieval with environment-specific defaults.

```csharp
public class GetNextTaskConfiguration
{
    /// <summary>
    /// Maximum time to wait for a task before giving up (default: 100ms for testing)
    /// </summary>
    public TimeSpan TimeToWaitForTask { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Interval between polling attempts (default: 10ms for testing)
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Maximum number of retry attempts when race conditions occur during task claiming (default: 50)
    /// </summary>
    public int MaxRetries { get; set; } = 50;

    /// <summary>
    /// Production configuration with longer timeouts suitable for real agent use
    /// </summary>
    public static GetNextTaskConfiguration Production => new()
    {
        TimeToWaitForTask = TimeSpan.FromMinutes(5),
        PollingInterval = TimeSpan.FromSeconds(1),
        MaxRetries = 10
    };
}
```

**Usage:**

```csharp
// Use production configuration
var tool = new GetNextTaskMcpTool(/* dependencies */)
{
    Configuration = GetNextTaskConfiguration.Production
};

// Custom configuration
var customConfig = new GetNextTaskConfiguration
{
    TimeToWaitForTask = TimeSpan.FromMinutes(2),
    PollingInterval = TimeSpan.FromMilliseconds(500),
    MaxRetries = 5
};
```

## Service Registration

### Infrastructure Services

```csharp
public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Core infrastructure
        services.AddSingleton<ITimeService, SystemTimeService>();
        services.AddSingleton<IEnvironmentService, EnvironmentService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IOperatingSystemService, OperatingSystemService>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        
        // Logging
        services.AddSingleton<IAppLogger, ConsoleAppLogger>();
        
        // Business services
        services.AddSingleton<IContextService, ContextService>();
        services.AddSingleton<IGeminiService, GeminiService>();
        services.AddSingleton<IMemoryService, MemoryService>();
        services.AddSingleton<ILocalAgentService, LocalAgentService>();
        services.AddSingleton<AgentStateService>();
        services.AddSingleton<DatabaseEventLoggerService>();
        
        // Event system
        services.AddEventingServices();
        
        return services;
    }
}
```

### Event System Services

```csharp
public static IServiceCollection AddEventingServices(this IServiceCollection services)
{
    // Event buses
    services.AddSingleton<IEventBus<TaskEventType, IEventPayload>, 
                          InMemoryEventBus<TaskEventType, IEventPayload>>();
    services.AddSingleton<IEventBus<AgentEventType, IAgentLifecyclePayload>, 
                          InMemoryEventBus<AgentEventType, IAgentLifecyclePayload>>();
    
    // Notification services
    services.AddSingleton<IWorkItemNotificationService, WorkItemNotificationService>();
    services.AddSingleton<IAgentNotificationService, AgentNotificationService>();
    
    return services;
}
```

### Data Layer Services

```csharp
public static IServiceCollection AddDataLayerServices(this IServiceCollection services)
{
    services.AddDbContext<CoordinationDbContext>(options =>
        options.UseSqlite("Data Source=:memory:"));
    
    services.AddScoped<IDatabaseScopeService, DatabaseScopeService>();
    services.AddScoped<IReadOnlyDatabaseScopeService, DatabaseScopeService>();
    
    return services;
}
```

## Core Interfaces

### Event System Interfaces

#### IEventBus<TType, TPayload>

Generic event bus interface for type-safe event publishing and subscription.

```csharp
public interface IEventBus<TType, TPayload>
    where TType : struct, Enum
    where TPayload : class, IEventPayload
{
    Task PublishAsync(TType eventType, TPayload payload, CancellationToken ct = default);
    IAsyncEnumerable<EventEnvelope<TType, TPayload>> Subscribe(
        EventFilter<TType, TPayload> filter, 
        CancellationToken ct = default);
}
```

#### IWorkItemNotificationService

Task-related event coordination interface.

```csharp
public interface IWorkItemNotificationService
{
    Task NotifyTaskCreatedAsync(WorkItem task);
    Task NotifyTaskClaimedAsync(WorkItem task, string agentId);
    Task NotifyTaskCompletedAsync(WorkItem task, string result);
    Task NotifyTaskFailedAsync(WorkItem task, string errorMessage);
    
    Task SubscribeForTaskCompletion(params string[] taskIds);
    Task<string?> TryConsumeTaskCreatedAsync(
        string agentId, 
        string personaId, 
        CancellationToken cancellationToken);
}
```

#### IAgentNotificationService

Agent lifecycle event coordination interface.

```csharp
public interface IAgentNotificationService
{
    Task NotifyAgentStartedAsync(string agentId, string personaId);
    Task NotifyAgentStoppedAsync(string agentId, string personaId);
    
    IAsyncEnumerable<AgentEventEnvelope> SubscribeToAgentEvents(
        EventFilter<AgentEventType, IAgentLifecyclePayload> filter, 
        CancellationToken cancellationToken = default);
}
```

### Business Service Interfaces

#### IMemoryService

Persistent memory management with namespace support.

```csharp
public interface IMemoryService
{
    Task SaveMemoryAsync(
        string key, 
        string value, 
        string? @namespace = null, 
        string? type = null, 
        string? metadata = null);
    
    Task<MemoryEntryDto?> ReadMemoryAsync(string key, string? @namespace = null);
    Task UpdateMemoryAccessAsync(string key, string? @namespace = null);
    Task<bool> DeleteMemoryAsync(string key, string? @namespace = null);
}
```

#### ILocalAgentService

Agent lifecycle and state management.

```csharp
public interface ILocalAgentService
{
    Task<Agent?> RegisterAgentAsync(
        string agentId, 
        string personaId, 
        int? processId, 
        string? workingDirectory = null,
        string? model = null, 
        string? worktreeName = null);
    
    Task UpdateHeartbeatAsync(string agentId);
    Task UpdateAgentStatusAsync(string agentId, AgentStatus status);
    Task<Agent?> GetAgentAsync(string agentId);
    Task<Agent[]> GetAgentsAsync(string? personaFilter = null);
    Task<bool> StopAgentAsync(string agentId);
}
```

### Database Access Interfaces

#### IDatabaseScopeService

Provides scoped database access with transaction support.

```csharp
public interface IDatabaseScopeService
{
    DatabaseScope GetWriteScope();
    IReadOnlyDatabaseScopeService GetReadScope();
}

public interface IReadOnlyDatabaseScopeService
{
    DatabaseScope GetReadScope();
}
```

#### DatabaseScope

Scoped database context wrapper with transaction management.

```csharp
public class DatabaseScope : IDisposable
{
    public CoordinationDbContext Context { get; }
    public DbSet<WorkItem> Tasks => Context.Tasks;
    public DbSet<Agent> Agents => Context.Agents;
    public DbSet<MemoryEntry> MemoryEntries => Context.MemoryEntries;
    public DbSet<EventLog> EventLogs => Context.EventLogs;
    
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    public void Dispose();
}
```

## Entity Models

### WorkItem

Represents a task in the coordination system.

```csharp
public class WorkItem
{
    public string Id { get; set; } = null!;
    public string? AgentId { get; set; }
    public string PersonaId { get; set; } = null!;
    public string Description { get; set; } = null!;
    public TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### Agent

Represents an agent in the coordination system.

```csharp
public class Agent
{
    public string Id { get; set; } = null!;
    public string PersonaId { get; set; } = null!;
    public AgentStatus Status { get; set; }
    public int? ProcessId { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? Model { get; set; }
    public string? WorktreeName { get; set; }
}
```

### MemoryEntry

Represents persistent memory storage.

```csharp
public class MemoryEntry
{
    public string Key { get; set; } = null!;
    public string Namespace { get; set; } = "";
    public string Value { get; set; } = null!;
    public string Type { get; set; } = MemoryService.DefaultContentType;
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
}
```

### EventLog

Represents persisted event audit trail.

```csharp
public class EventLog
{
    public int Id { get; set; }
    public string EventId { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string? SourceId { get; set; }
    public string? TargetId { get; set; }
}
```

## Enumerations

### TaskStatus

```csharp
public enum TaskStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3
}
```

### TaskPriority

```csharp
public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}
```

### AgentStatus

```csharp
public enum AgentStatus
{
    Starting = 0,
    Running = 1,
    Stopped = 2,
    Failed = 3
}
```

### TaskEventType

```csharp
public enum TaskEventType
{
    TaskCreated = 0,
    TaskClaimed = 1,
    TaskCompleted = 2,
    TaskFailed = 3
}
```

### AgentEventType

```csharp
public enum AgentEventType
{
    AgentStarted = 0,
    AgentStopped = 1
}
```

## Configuration Constants

### MemoryService Configuration

```csharp
public static class MemoryService
{
    public const string DefaultContentType = "text";
    public const string DefaultNamespace = "";
}
```

### Database Configuration

```csharp
public static class DatabaseConfiguration
{
    public const string DefaultConnectionString = "Data Source=:memory:";
    public const string ProductionConnectionString = "Data Source=aiswarm.db";
}
```

## Error Handling

### Result Types

The system uses result types for error handling in MCP tools:

```csharp
public abstract class BaseResult
{
    public bool Success { get; protected init; }
    public string? ErrorMessage { get; protected init; }
    
    public static T Failure<T>(string errorMessage) where T : BaseResult, new()
        => new() { Success = false, ErrorMessage = errorMessage };
}

public class CreateTaskResult : BaseResult
{
    public string? TaskId { get; init; }
    
    public static CreateTaskResult SuccessWith(string taskId)
        => new() { Success = true, TaskId = taskId };
}

public class GetNextTaskResult : BaseResult
{
    public WorkItem? Task { get; init; }
    
    public static GetNextTaskResult SuccessWith(WorkItem task)
        => new() { Success = true, Task = task };
    
    public static GetNextTaskResult NoTaskAvailable()
        => new() { Success = true, Task = null };
}
```

### Exception Types

```csharp
public class AgentNotFoundException : Exception
{
    public AgentNotFoundException(string agentId) 
        : base($"Agent not found: {agentId}") { }
}

public class TaskNotFoundException : Exception
{
    public TaskNotFoundException(string taskId) 
        : base($"Task not found: {taskId}") { }
}

public class InvalidTaskStateException : Exception
{
    public InvalidTaskStateException(string taskId, TaskStatus currentStatus, TaskStatus expectedStatus)
        : base($"Task {taskId} is in {currentStatus} state, expected {expectedStatus}") { }
}
```

## Performance Considerations

### Database Optimization

- **Connection Pooling**: Entity Framework automatically pools connections
- **Transaction Scoping**: Use `DatabaseScope` for transaction management
- **Read/Write Separation**: Use appropriate scope types for operations
- **Async Operations**: All database operations are async

### Event System Optimization

- **Channel Capacity**: Configure bounded channels to prevent memory leaks
- **Subscription Filtering**: Use specific filters to reduce event processing
- **Async Processing**: Event handlers should be async and non-blocking
- **Resource Cleanup**: Properly dispose subscriptions and event buses

### Memory Management

- **Scoped Services**: Use appropriate service lifetimes
- **Disposal**: Implement `IDisposable` for resource cleanup
- **Event Cleanup**: Events are cleaned up after delivery
- **Database Connections**: Connections are automatically managed by EF Core

## Testing Utilities

### Test Configuration

```csharp
public static class TestConfiguration
{
    public static GetNextTaskConfiguration Fast => new()
    {
        TimeToWaitForTask = TimeSpan.FromMilliseconds(100),
        PollingInterval = TimeSpan.FromMilliseconds(10),
        MaxRetries = 50
    };
}
```

### Test Doubles

```csharp
public class FakeTimeService : ITimeService
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;
}

public class TestLogger : IAppLogger
{
    public List<string> LoggedMessages { get; } = [];
    
    public void LogInformation(string message) => LoggedMessages.Add($"INFO: {message}");
    public void LogError(string message) => LoggedMessages.Add($"ERROR: {message}");
    public void LogWarning(string message) => LoggedMessages.Add($"WARN: {message}");
}
```

### In-Memory Database Setup

```csharp
public static class TestDatabaseSetup
{
    public static CoordinationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        var context = new CoordinationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
```

This API documentation provides comprehensive coverage of the AISwarm coordination system's configuration options, service registrations, and architectural changes, enabling effective development and integration with the new event-driven features.