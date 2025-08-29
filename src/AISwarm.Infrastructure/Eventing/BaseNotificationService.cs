using System.Diagnostics.CodeAnalysis;

namespace AISwarm.Infrastructure.Eventing;

/// <summary>
/// Base class for notification services providing common event publishing functionality
/// </summary>
public abstract class BaseNotificationService<TEventType, TPayload, TEventEnvelope>
    where TEventType : struct, Enum
    where TPayload : class, IEventPayload
    where TEventEnvelope : EventEnvelope<TEventType, TPayload>
{
    protected readonly IEventBus<TEventType, TPayload> Bus;

    protected BaseNotificationService(IEventBus<TEventType, TPayload> bus)
    {
        Bus = bus;
    }

    protected ValueTask PublishEventAsync<TConcretePayload>(
        TEventType eventType,
        TConcretePayload payload,
        CancellationToken ct = default)
        where TConcretePayload : class, TPayload
    {
        var evt = CreateEventEnvelope(eventType, DateTimeOffset.UtcNow, payload);
        return Bus.PublishAsync(evt, ct);
    }

    protected abstract TEventEnvelope CreateEventEnvelope(TEventType type, DateTimeOffset timestamp, TPayload payload);
}
