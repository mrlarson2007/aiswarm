namespace AISwarm.Infrastructure.Eventing;

public class WorkItemNotificationService : IWorkItemNotificationService
{
    private readonly IEventBus _bus;
    public const string TaskCreatedType = "TaskCreated";

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

    public ValueTask PublishTaskCreated(string taskId, string? agentId, string? persona, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("taskId must be provided", nameof(taskId));
        var payload = new TaskCreatedPayload(taskId, agentId, persona);
        var evt = new EventEnvelope(TaskCreatedType, DateTimeOffset.UtcNow, null, agentId, payload);
        return _bus.PublishAsync(evt, ct);
    }
}

public record TaskCreatedPayload(string TaskId, string? AgentId, string? Persona);
