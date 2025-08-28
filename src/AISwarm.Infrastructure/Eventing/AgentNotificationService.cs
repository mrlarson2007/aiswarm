namespace AISwarm.Infrastructure.Eventing;

public class AgentNotificationService(IEventBus<AgentEventType, IAgentLifecyclePayload> bus)
    : BaseNotificationService<AgentEventType, IAgentLifecyclePayload, AgentEventEnvelope>(bus), IAgentNotificationService
{
    protected override AgentEventEnvelope CreateEventEnvelope(AgentEventType type, DateTimeOffset timestamp, IAgentLifecyclePayload payload)
    {
        return new AgentEventEnvelope(type, timestamp, payload);
    }

    public ValueTask PublishAgentRegistered(string agentId, string? persona, CancellationToken ct = default)
    {
        EventValidation.ValidateRequiredId(agentId, nameof(agentId));
        
        var payload = new AgentRegisteredPayload(agentId, persona);
        return PublishEventAsync(AgentEventType.Registered, payload, ct);
    }

    public ValueTask PublishAgentKilled(string agentId, string? reason, CancellationToken ct = default)
    {
        EventValidation.ValidateRequiredId(agentId, nameof(agentId));
        
        var payload = new AgentKilledPayload(agentId, reason);
        return PublishEventAsync(AgentEventType.Killed, payload, ct);
    }

    public ValueTask PublishAgentStatusChanged(string agentId, string? oldStatus, string? newStatus, CancellationToken ct = default)
    {
        EventValidation.ValidateRequiredId(agentId, nameof(agentId));
        
        var payload = new AgentStatusChangedPayload(agentId, oldStatus, newStatus);
        return PublishEventAsync(AgentEventType.StatusChanged, payload, ct);
    }

    public IAsyncEnumerable<AgentEventEnvelope> SubscribeForAllAgentEvents(CancellationToken ct = default)
    {
        var filter = new AgentEventFilter(
            Types: [AgentEventType.Registered, AgentEventType.Killed, AgentEventType.StatusChanged]);
        return ToAgentEventEnvelopeAsyncEnumerable(Bus.Subscribe(filter, ct));
    }

    private IAsyncEnumerable<AgentEventEnvelope> ToAgentEventEnvelopeAsyncEnumerable(
        IAsyncEnumerable<EventEnvelope<AgentEventType, IAgentLifecyclePayload>> asyncEnumerable)
    {
        return EventConversion.ConvertToConcreteEnvelope(
            asyncEnumerable, 
            (type, timestamp, payload) => new AgentEventEnvelope(type, timestamp, payload));
    }
}

public record AgentRegisteredPayload(string AgentId, string? Persona) : IAgentLifecyclePayload;
public record AgentKilledPayload(string AgentId, string? Reason) : IAgentLifecyclePayload;
public record AgentStatusChangedPayload(string AgentId, string? OldStatus, string? NewStatus) : IAgentLifecyclePayload;