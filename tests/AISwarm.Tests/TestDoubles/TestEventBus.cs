using System.Threading.Channels;
using AISwarm.Infrastructure.Eventing;
using System.Threading.Tasks;

namespace AISwarm.Tests.TestDoubles;

public class TestEventBus<TType, TPayload> : InMemoryEventBus<TType, TPayload>
    where TType : struct, Enum
    where TPayload : class, IEventPayload
{
    public TaskCompletionSource<bool> SubscriptionReadySignal { get; } = new TaskCompletionSource<bool>();

    public override IAsyncEnumerable<EventEnvelope<TType, TPayload>> Subscribe(EventFilter<TType, TPayload> filter, CancellationToken ct = default)
    {
        // Signal that a subscription has been initiated
        SubscriptionReadySignal.TrySetResult(true);
        return base.Subscribe(filter, ct);
    }
}
