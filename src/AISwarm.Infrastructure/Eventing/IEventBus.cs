namespace AISwarm.Infrastructure.Eventing;

public interface IEventBus<TType, TPayload>
    where TType : struct, Enum
    where TPayload : class, IEventPayload
{
    IAsyncEnumerable<EventEnvelope<TType, TPayload>> Subscribe(EventFilter<TType, TPayload> filter,
        CancellationToken ct = default);

    ValueTask PublishAsync(EventEnvelope<TType, TPayload> evt, CancellationToken ct = default);
}
