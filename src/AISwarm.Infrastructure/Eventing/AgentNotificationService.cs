namespace AISwarm.Infrastructure.Eventing;

public class AgentNotificationService(IEventBus<AgentEventType, IAgentLifecyclePayload> bus)
    : IAgentNotificationService
{
    public ValueTask PublishAgentRegistered(string agentId, string? persona, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId must be provided", nameof(agentId));
        
        var payload = new AgentRegisteredPayload(agentId, persona);
        var evt = new AgentEventEnvelope(AgentEventType.Registered, DateTimeOffset.UtcNow, payload);
        return bus.PublishAsync(evt, ct);
    }

    public ValueTask PublishAgentKilled(string agentId, string? reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId must be provided", nameof(agentId));
        
        var payload = new AgentKilledPayload(agentId, reason);
        var evt = new AgentEventEnvelope(AgentEventType.Killed, DateTimeOffset.UtcNow, payload);
        return bus.PublishAsync(evt, ct);
    }

    public ValueTask PublishAgentStatusChanged(string agentId, string? oldStatus, string? newStatus, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("agentId must be provided", nameof(agentId));
        
        var payload = new AgentStatusChangedPayload(agentId, oldStatus, newStatus);
        var evt = new AgentEventEnvelope(AgentEventType.StatusChanged, DateTimeOffset.UtcNow, payload);
        return bus.PublishAsync(evt, ct);
    }

    public IAsyncEnumerable<AgentEventEnvelope> SubscribeForAllAgentEvents(CancellationToken ct = default)
    {
        var filter = new AgentEventFilter(
            Types: [AgentEventType.Registered, AgentEventType.Killed, AgentEventType.StatusChanged]);
        return ToAgentEventEnvelopeAsyncEnumerable(bus.Subscribe(filter, ct));
    }

    private async IAsyncEnumerable<AgentEventEnvelope> ToAgentEventEnvelopeAsyncEnumerable(
        IAsyncEnumerable<EventEnvelope<AgentEventType, IAgentLifecyclePayload>> asyncEnumerable)
    {
        await foreach (var e in asyncEnumerable)
        {
            yield return new AgentEventEnvelope(
                e.Type,
                e.Timestamp,
                e.Payload
            );
        }
    }
}

public record AgentRegisteredPayload(string AgentId, string? Persona) : IAgentLifecyclePayload;
public record AgentKilledPayload(string AgentId, string? Reason) : IAgentLifecyclePayload;
public record AgentStatusChangedPayload(string AgentId, string? OldStatus, string? NewStatus) : IAgentLifecyclePayload;