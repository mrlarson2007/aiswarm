namespace AISwarm.Infrastructure.Eventing;

public interface IEventBus<TType, TPayload>
{
    IAsyncEnumerable<EventEnvelope<TType, TPayload>> Subscribe(EventFilter<TType, TPayload> filter, CancellationToken ct = default);
    ValueTask PublishAsync(EventEnvelope<TType, TPayload> evt, CancellationToken ct = default);
}
