namespace AISwarm.Infrastructure.Eventing;

public interface IEventBus
{
    IAsyncEnumerable<EventEnvelope> Subscribe(EventFilter filter, CancellationToken ct = default);
    ValueTask PublishAsync(EventEnvelope evt, CancellationToken ct = default);
}
