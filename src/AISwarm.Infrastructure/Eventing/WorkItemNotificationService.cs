namespace AISwarm.Infrastructure.Eventing;

public class WorkItemNotificationService(IEventBus<TaskEventType, ITaskLifecyclePayload> bus)
    : BaseNotificationService<TaskEventType, ITaskLifecyclePayload, TaskEventEnvelope>(bus), IWorkItemNotificationService
{
    protected override TaskEventEnvelope CreateEventEnvelope(TaskEventType type, DateTimeOffset timestamp, ITaskLifecyclePayload payload)
    {
        return new TaskEventEnvelope(type, timestamp, payload);
    }

    private IAsyncEnumerable<TaskEventEnvelope> ToTaskEventEnvelopeAsyncEnumerable(
        IAsyncEnumerable<EventEnvelope<TaskEventType, ITaskLifecyclePayload>> asyncEnumerable)
    {
        return EventConversion.ConvertToConcreteEnvelope(
            asyncEnumerable,
            (type, timestamp, payload) => new TaskEventEnvelope(type, timestamp, payload));
    }

    public IAsyncEnumerable<TaskEventEnvelope> SubscribeForAgent(string agentId, CancellationToken ct = default)
    {
        EventValidation.ValidateRequiredId(agentId, nameof(agentId));

        var filter = new TaskEventFilter(
            Types: [TaskEventType.Created],
            Predicate: e => e.Payload is TaskCreatedPayload p && p.AgentId == agentId);
        return ToTaskEventEnvelopeAsyncEnumerable(Bus.Subscribe(filter, ct));
    }

    public IAsyncEnumerable<TaskEventEnvelope> SubscribeForPersona(string persona, CancellationToken ct = default)
    {
        EventValidation.ValidateRequiredId(persona, nameof(persona));

        var filter = new TaskEventFilter(
            Types: [TaskEventType.Created],
            Predicate: e => e.Payload is TaskCreatedPayload p
                && string.Equals(p.PersonaId, persona, StringComparison.OrdinalIgnoreCase)
                && p.AgentId == null);
        return ToTaskEventEnvelopeAsyncEnumerable(Bus.Subscribe(filter, ct));
    }

    public ValueTask PublishTaskCreated(
        string taskId,
        string? agentId,
        string? persona,
        CancellationToken ct = default)
    {
        EventValidation.ValidateRequiredId(taskId, nameof(taskId));

        var payload = new TaskCreatedPayload(taskId, agentId, persona);
        return PublishEventAsync(TaskEventType.Created, payload, ct);
    }

    public ValueTask PublishTaskClaimed(
        string taskId,
        string? agentId,
        CancellationToken ct = default)
    {
        EventValidation.ValidateRequiredId(taskId, nameof(taskId));

        var payload = new TaskClaimedPayload(taskId, agentId);
        return PublishEventAsync(TaskEventType.Claimed, payload, ct);
    }

    public ValueTask PublishTaskCompleted(
        string taskId,
        string? agentId,
        CancellationToken ct = default)
    {
        EventValidation.ValidateRequiredId(taskId, nameof(taskId));

        var payload = new TaskCompletedPayload(taskId, agentId);
        return PublishEventAsync(TaskEventType.Completed, payload, ct);
    }

    public ValueTask PublishTaskFailed(string taskId, string? agentId, string? reason, CancellationToken ct = default)
    {
        EventValidation.ValidateRequiredId(taskId, nameof(taskId));

        var payload = new TaskFailedPayload(taskId, agentId, reason);
        return PublishEventAsync(TaskEventType.Failed, payload, ct);
    }

    public IAsyncEnumerable<TaskEventEnvelope> SubscribeForAgentOrPersona(string agentId, string persona, CancellationToken ct = default)
    {
        EventValidation.ValidateRequiredId(agentId, nameof(agentId));
        EventValidation.ValidateRequiredId(persona, nameof(persona));

        var filter = new TaskEventFilter(
            Types: [TaskEventType.Created],
            Predicate: e => e.Payload is TaskCreatedPayload p &&
                (string.Equals(p.AgentId, agentId, StringComparison.OrdinalIgnoreCase) ||
                 (p.AgentId == null && string.Equals(p.PersonaId, persona, StringComparison.OrdinalIgnoreCase))));
        return ToTaskEventEnvelopeAsyncEnumerable(Bus.Subscribe(filter, ct));
    }

    public IAsyncEnumerable<TaskEventEnvelope> SubscibeForTaskCompletion(
        IReadOnlyList<string> taskIds,
        CancellationToken ct = default)
    {
        EventValidation.ValidateRequiredCollection(taskIds, nameof(taskIds));

        var filter = new TaskEventFilter(
            Types: [TaskEventType.Completed, TaskEventType.Failed],
            Predicate: e => taskIds.Contains(e.Payload.TaskId));
        return ToTaskEventEnvelopeAsyncEnumerable(Bus.Subscribe(filter, ct));
    }

    public IAsyncEnumerable<TaskEventEnvelope> SubscribeForAllTaskEvents(CancellationToken ct = default)
    {
        var filter = new TaskEventFilter(
            Types: [TaskEventType.Created, TaskEventType.Claimed, TaskEventType.Completed, TaskEventType.Failed]);
        return ToTaskEventEnvelopeAsyncEnumerable(Bus.Subscribe(filter, ct));
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
public interface ITaskLifecyclePayload : IEventPayload
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
public record TaskCreatedPayload(string TaskId, string? AgentId, string? PersonaId) : ITaskLifecyclePayload;
public record TaskClaimedPayload(string TaskId, string? AgentId) : ITaskLifecyclePayload;
public record TaskCompletedPayload(string TaskId, string? AgentId) : ITaskLifecyclePayload;
public record TaskFailedPayload(string TaskId, string? AgentId, string? Reason) : ITaskLifecyclePayload;
