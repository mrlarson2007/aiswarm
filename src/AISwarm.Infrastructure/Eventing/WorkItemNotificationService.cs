namespace AISwarm.Infrastructure.Eventing;

public class WorkItemNotificationService(IEventBus<TaskEventType, ITaskLifecyclePayload> bus)
    : IWorkItemNotificationService
{
    private async IAsyncEnumerable<TaskEventEnvelope> ToTaskEventEnvelopeAsyncEnumerable(
        IAsyncEnumerable<EventEnvelope<TaskEventType, ITaskLifecyclePayload>> asyncEnumerable)
    {
        await foreach (var e in asyncEnumerable)
        {
            yield return new TaskEventEnvelope(
                e.Type,
                e.Timestamp,
                e.Payload
            );
        }

    }
    public IAsyncEnumerable<TaskEventEnvelope> SubscribeForAgent(string agentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId must be provided", nameof(agentId));
        var filter = new TaskEventFilter(
            Types: [TaskEventType.Created],
            Predicate: e => e.Payload is TaskCreatedPayload p && p.AgentId == agentId);
        return ToTaskEventEnvelopeAsyncEnumerable(bus.Subscribe(filter, ct));
    }

    public IAsyncEnumerable<TaskEventEnvelope> SubscribeForPersona(string persona, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(persona))
            throw new ArgumentException("persona must be provided", nameof(persona));
        var filter = new TaskEventFilter(
            Types: [TaskEventType.Created],
            Predicate: e => e.Payload is TaskCreatedPayload p
                && string.Equals(p.Persona, persona, StringComparison.OrdinalIgnoreCase)
                && p.AgentId == null);
        return ToTaskEventEnvelopeAsyncEnumerable(bus.Subscribe(filter, ct));
    }

    public ValueTask PublishTaskCreated(
        string taskId,
        string? agentId,
        string? persona,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("taskId must be provided", nameof(taskId));
        var payload = new TaskCreatedPayload(taskId, agentId, persona);
        var evt = new TaskEventEnvelope(TaskEventType.Created, DateTimeOffset.UtcNow, payload);
        return bus.PublishAsync(evt, ct);
    }

    public ValueTask PublishTaskCompleted(
        string taskId,
        string? agentId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("taskId must be provided", nameof(taskId));
        var payload = new TaskCompletedPayload(taskId, agentId);
        var evt = new TaskEventEnvelope(
            TaskEventType.Completed,
            DateTimeOffset.UtcNow,
            payload);
        return bus.PublishAsync(evt, ct);
    }

    public ValueTask PublishTaskFailed(string taskId, string? agentId, string? reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("taskId must be provided", nameof(taskId));

        var payload = new TaskFailedPayload(taskId, agentId, reason);
        var evt = new TaskEventEnvelope(
            TaskEventType.Failed,
            DateTimeOffset.UtcNow,
            payload);
        return bus.PublishAsync(evt, ct);
    }

    public IAsyncEnumerable<TaskEventEnvelope> SubscribeForAgentOrPersona(string agentId, string persona, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId must be provided", nameof(agentId));
        if (string.IsNullOrWhiteSpace(persona))
            throw new ArgumentException("persona must be provided", nameof(persona));

        var filter = new TaskEventFilter(
            Types: [TaskEventType.Created],
            Predicate: e => e.Payload is TaskCreatedPayload p &&
                (string.Equals(p.AgentId, agentId, StringComparison.OrdinalIgnoreCase) ||
                 (p.AgentId == null && string.Equals(p.Persona, persona, StringComparison.OrdinalIgnoreCase))));
        return ToTaskEventEnvelopeAsyncEnumerable(bus.Subscribe(filter, ct));
    }

    public IAsyncEnumerable<TaskEventEnvelope> SubscibeForTaskCompletion(
        IReadOnlyList<string> taskIds,
        CancellationToken ct = default)
    {
        if (taskIds == null || taskIds.Count == 0)
            throw new ArgumentException("taskIds must be provided and not empty", nameof(taskIds));

        var filter = new TaskEventFilter(
            Types: [TaskEventType.Completed, TaskEventType.Failed],
            Predicate: e => taskIds.Contains(e.Payload.TaskId));
        return ToTaskEventEnvelopeAsyncEnumerable(bus.Subscribe(filter, ct));
    }

    public IAsyncEnumerable<TaskEventEnvelope> SubscribeForAllTaskEvents(CancellationToken ct = default)
    {
        var filter = new TaskEventFilter(
            Types: [TaskEventType.Created, TaskEventType.Completed, TaskEventType.Failed]);
        return ToTaskEventEnvelopeAsyncEnumerable(bus.Subscribe(filter, ct));
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
public interface ITaskLifecyclePayload
{
    string TaskId
    {
        get;
    }
    string? AgentId
    {
        get;
    }
}
public record TaskCreatedPayload(string TaskId, string? AgentId, string? Persona) : ITaskLifecyclePayload;
public record TaskCompletedPayload(string TaskId, string? AgentId) : ITaskLifecyclePayload;
public record TaskFailedPayload(string TaskId, string? AgentId, string? Reason) : ITaskLifecyclePayload;
