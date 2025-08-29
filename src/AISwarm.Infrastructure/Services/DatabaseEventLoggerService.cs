using System.Text.Json;
using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure.Eventing;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.Infrastructure;

/// <summary>
///     Service that subscribes to system events and logs them to the database for audit and observability
/// </summary>
public interface IEventLoggerService
{
    /// <summary>
    ///     Starts subscribing to events and logging them to the database
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stops subscribing to events
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Implementation that logs both task and agent events to the EventLog table
/// </summary>
public class DatabaseEventLoggerService(
    IDatabaseScopeService scopeService,
    IWorkItemNotificationService taskNotifications,
    IAgentNotificationService agentNotifications,
    ITimeService timeService,
    IAppLogger logger) : IEventLoggerService, IDisposable
{
    private readonly IAgentNotificationService _agentNotifications = agentNotifications;
    private readonly IAppLogger _logger = logger;
    private readonly IDatabaseScopeService _scopeService = scopeService;
    private readonly IWorkItemNotificationService _taskNotifications = taskNotifications;
    private readonly ITimeService _timeService = timeService;
    private Task? _agentEventLoggingTask;

    private CancellationTokenSource? _cancellationTokenSource;
    private TaskCompletionSource? _readyTaskCompletionSource;
    private Task? _taskEventLoggingTask;

    public void Dispose()
    {
        StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cancellationTokenSource != null)
        {
            _logger.Warn("Event logger is already running");
            return;
        }

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cancellationTokenSource.Token;
        _readyTaskCompletionSource = new TaskCompletionSource();

        _logger.Info("Starting database event logger service");

        var listenerReadyCount = 0;
        var totalListeners = 2; // Task and Agent listeners

        // Start task event logging
        _taskEventLoggingTask = Task.Run(async () =>
        {
            try
            {
                var subscription = _taskNotifications.SubscribeForAllTaskEvents(token);

                // Signal that this listener is ready
                if (Interlocked.Increment(ref listenerReadyCount) == totalListeners)
                    _readyTaskCompletionSource.SetResult();

                await foreach (var taskEvent in subscription)
                    await LogTaskEventAsync(taskEvent, token);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Task event logging cancelled");
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error($"Invalid operation in task event logging: {ex.Message}");
            }
            catch (IOException ex)
            {
                _logger.Error($"IO error in task event logging: {ex.Message}");
            }
            catch (DbUpdateException ex)
            {
                _logger.Error($"Database update error in task event logging: {ex.Message}");
            }
        }, token);

        // Start agent event logging
        _agentEventLoggingTask = Task.Run(async () =>
        {
            try
            {
                var subscription = _agentNotifications.SubscribeForAllAgentEvents(token);

                // Signal that this listener is ready
                if (Interlocked.Increment(ref listenerReadyCount) == totalListeners)
                    _readyTaskCompletionSource.SetResult();

                await foreach (var agentEvent in subscription)
                    await LogAgentEventAsync(agentEvent, token);
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Agent event logging cancelled");
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error($"Invalid operation in agent event logging: {ex.Message}");
            }
            catch (IOException ex)
            {
                _logger.Error($"IO error in agent event logging: {ex.Message}");
            }
            catch (DbUpdateException ex)
            {
                _logger.Error($"Database update error in agent event logging: {ex.Message}");
            }
        }, token);

        // Wait for both listeners to be ready
        await _readyTaskCompletionSource.Task;

        _logger.Info("Database event logger service started successfully");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cancellationTokenSource == null)
            return;

        _logger.Info("Stopping database event logger service");

        await _cancellationTokenSource.CancelAsync();

        // Wait for logging tasks to complete
        if (_taskEventLoggingTask != null)
            try
            {
                await _taskEventLoggingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }

        if (_agentEventLoggingTask != null)
            try
            {
                await _agentEventLoggingTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _taskEventLoggingTask = null;
        _agentEventLoggingTask = null;
        _readyTaskCompletionSource = null;

        _logger.Info("Database event logger service stopped");
    }

    private async Task LogTaskEventAsync(TaskEventEnvelope taskEvent, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeService.GetWriteScope();

            var eventLog = new EventLog
            {
                Id = Guid.NewGuid().ToString(),
                EventType = $"Task{taskEvent.Type}",
                Timestamp = _timeService.UtcNow,
                Actor = ExtractActorFromTaskEvent(taskEvent),
                CorrelationId = null, // Event envelopes don't have correlation IDs
                Payload =
                    JsonSerializer.Serialize((object?)taskEvent.Payload,
                        new JsonSerializerOptions { WriteIndented = false }),
                EntityId = ExtractEntityIdFromTaskEvent(taskEvent),
                EntityType = "Task",
                Severity = GetSeverityFromTaskEvent(taskEvent),
                Tags = BuildTagsFromTaskEvent(taskEvent)
            };

            scope.EventLogs.Add(eventLog);
            await scope.SaveChangesAsync();
            scope.Complete();

            _logger.Info($"Logged task event: {taskEvent.Type} for task {eventLog.EntityId}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to log task event {taskEvent.Type}: {ex.Message}");
        }
    }

    private async Task LogAgentEventAsync(AgentEventEnvelope agentEvent, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeService.GetWriteScope();

            var eventLog = new EventLog
            {
                Id = Guid.NewGuid().ToString(),
                EventType = $"Agent{agentEvent.Type}",
                Timestamp = _timeService.UtcNow,
                Actor = ExtractActorFromAgentEvent(agentEvent),
                CorrelationId = null, // Event envelopes don't have correlation IDs
                Payload =
                    JsonSerializer.Serialize(agentEvent.Payload, new JsonSerializerOptions { WriteIndented = false }),
                EntityId = agentEvent.Payload.AgentId,
                EntityType = "Agent",
                Severity = GetSeverityFromAgentEvent(agentEvent),
                Tags = BuildTagsFromAgentEvent(agentEvent)
            };

            scope.EventLogs.Add(eventLog);
            await scope.SaveChangesAsync();
            scope.Complete();

            _logger.Info($"Logged agent event: {agentEvent.Type} for agent {eventLog.EntityId}");
        }
        catch (DbUpdateException ex)
        {
            _logger.Error($"Database update failed when logging agent event {agentEvent.Type}: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.Error($"JSON serialization failed when logging agent event {agentEvent.Type}: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Unexpected error when logging agent event {agentEvent.Type}: {ex.Message}");
            throw; // Rethrow to allow upper-level handlers to take appropriate action
        }
    }

    private static string? ExtractActorFromTaskEvent(TaskEventEnvelope taskEvent)
    {
        return taskEvent.Payload switch
        {
            TaskCreatedPayload created => created.AgentId,
            TaskClaimedPayload claimed => claimed.AgentId,
            TaskCompletedPayload completed => completed.AgentId,
            TaskFailedPayload failed => failed.AgentId,
            _ => null
        };
    }

    private static string? ExtractEntityIdFromTaskEvent(TaskEventEnvelope taskEvent)
    {
        return taskEvent.Payload switch
        {
            TaskCreatedPayload created => created.TaskId,
            TaskClaimedPayload claimed => claimed.TaskId,
            TaskCompletedPayload completed => completed.TaskId,
            TaskFailedPayload failed => failed.TaskId,
            _ => null
        };
    }

    private static string? ExtractActorFromAgentEvent(AgentEventEnvelope agentEvent)
    {
        // For agent events, the actor is typically the system or the agent itself
        return agentEvent.Payload.AgentId;
    }

    private static string GetSeverityFromTaskEvent(TaskEventEnvelope taskEvent)
    {
        return taskEvent.Type switch
        {
            TaskEventType.Failed => "Warning",
            TaskEventType.Created => "Information",
            TaskEventType.Claimed => "Information",
            TaskEventType.Completed => "Information",
            _ => "Information"
        };
    }

    private static string GetSeverityFromAgentEvent(AgentEventEnvelope agentEvent)
    {
        return agentEvent.Type switch
        {
            AgentEventType.Killed => "Warning",
            AgentEventType.Registered => "Information",
            AgentEventType.StatusChanged => "Information",
            _ => "Information"
        };
    }

    private static string? BuildTagsFromTaskEvent(TaskEventEnvelope taskEvent)
    {
        var tags = new List<string>();

        if (taskEvent.Payload is TaskCreatedPayload created && !string.IsNullOrEmpty(created.PersonaId))
            tags.Add($"persona:{created.PersonaId}");

        return tags.Count > 0 ? string.Join(",", tags) : null;
    }

    private static string? BuildTagsFromAgentEvent(AgentEventEnvelope agentEvent)
    {
        var tags = new List<string>();

        // Add relevant tags based on agent event type
        tags.Add($"event:{agentEvent.Type}");

        return tags.Count > 0 ? string.Join(",", tags) : null;
    }
}
