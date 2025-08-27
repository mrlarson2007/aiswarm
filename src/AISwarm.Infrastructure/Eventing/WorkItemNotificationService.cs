namespace AISwarm.Infrastructure.Eventing;

public class WorkItemNotificationService : IWorkItemNotificationService
{
    private readonly IEventBus _bus;
    public const string TaskCreatedType = "TaskCreated";
    public const string TaskCompletedType = "TaskCompleted";
    public const string TaskFailedType = "TaskFailed";

    public WorkItemNotificationService(IEventBus bus)
    {
        _bus = bus;
    }

    public IAsyncEnumerable<EventEnvelope> SubscribeForAgent(string agentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId must be provided", nameof(agentId));
        var filter = new EventFilter(
            Types: new[] { TaskCreatedType },
            Predicate: e => e.Payload is TaskCreatedPayload p && p.AgentId == agentId);
        return _bus.Subscribe(filter, ct);
    }

    public IAsyncEnumerable<EventEnvelope> SubscribeForPersona(string persona, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(persona))
            throw new ArgumentException("persona must be provided", nameof(persona));
        var filter = new EventFilter(
            Types: [TaskCreatedType],
            Predicate: e => e.Payload is TaskCreatedPayload p
                && string.Equals(p.Persona, persona, StringComparison.OrdinalIgnoreCase)
                && p.AgentId == null);
        return _bus.Subscribe(filter, ct);
    }

    public IAsyncEnumerable<EventEnvelope> SubscribeForTaskLifecycle(string agentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId must be provided", nameof(agentId));
        var filter = new EventFilter(
            Types: new[] { TaskCreatedType, TaskCompletedType, TaskFailedType },
            Predicate: e => e.Payload is ITaskLifecyclePayload p &&
                string.Equals(p.AgentId, agentId, StringComparison.OrdinalIgnoreCase));
        return _bus.Subscribe(filter, ct);
    }

    public ValueTask PublishTaskCreated(string taskId, string? agentId, string? persona, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("taskId must be provided", nameof(taskId));
        var payload = new TaskCreatedPayload(taskId, agentId, persona);
        var evt = new EventEnvelope(TaskCreatedType, DateTimeOffset.UtcNow, null, agentId, payload);
        return _bus.PublishAsync(evt, ct);
    }

    public ValueTask PublishTaskCompleted(string taskId, string? agentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("taskId must be provided", nameof(taskId));
        var payload = new TaskCompletedPayload(taskId, agentId);
        var evt = new EventEnvelope(TaskCompletedType, DateTimeOffset.UtcNow, null, agentId, payload);
        return _bus.PublishAsync(evt, ct);
    }

    public ValueTask PublishTaskFailed(string taskId, string? agentId, string? reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("taskId must be provided", nameof(taskId));
        var payload = new TaskFailedPayload(taskId, agentId, reason);
        var evt = new EventEnvelope(TaskFailedType, DateTimeOffset.UtcNow, null, agentId, payload);
        return _bus.PublishAsync(evt, ct);
    }

    public IAsyncEnumerable<EventEnvelope> SubscribeForAgentOrPersona(string agentId, string persona, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId must be provided", nameof(agentId));
        if (string.IsNullOrWhiteSpace(persona))
            throw new ArgumentException("persona must be provided", nameof(persona));

        var filter = new EventFilter(
            Types: new[] { TaskCreatedType },
            Predicate: e => e.Payload is TaskCreatedPayload p &&
                (string.Equals(p.AgentId, agentId, StringComparison.OrdinalIgnoreCase) ||
                 (p.AgentId == null && string.Equals(p.Persona, persona, StringComparison.OrdinalIgnoreCase))));
        return _bus.Subscribe(filter, ct);
    }

    public async Task<string?> TryConsumeTaskCreatedAsync(string agentId, string persona, CancellationToken ct = default)
    {
        // Attempt a non-blocking single-event consume by racing a zero-time cancellation
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.Zero);

        await using var enumerator = SubscribeForAgentOrPersona(agentId, persona, cts.Token).GetAsyncEnumerator(cts.Token);
        try
        {
            var moved = await enumerator.MoveNextAsync();
            if (moved && enumerator.Current.Payload is TaskCreatedPayload payload)
                return payload.TaskId;
        }
        catch (OperationCanceledException)
        {
            // No event available immediately
        }
        return null;
    }
}
public interface ITaskLifecyclePayload { string TaskId { get; } string? AgentId { get; } }
public record TaskCreatedPayload(string TaskId, string? AgentId, string? Persona) : ITaskLifecyclePayload;
public record TaskCompletedPayload(string TaskId, string? AgentId) : ITaskLifecyclePayload;
public record TaskFailedPayload(string TaskId, string? AgentId, string? Reason) : ITaskLifecyclePayload;
