using System.Threading.Channels;

namespace AISwarm.Infrastructure.Eventing;

public class InMemoryEventBus<TType, TPayload> : IEventBus<TType, TPayload>, IDisposable
    where TType : struct, Enum
    where TPayload : class, IEventPayload
{
    private readonly List<(EventFilter<TType, TPayload> Filter, Channel<EventEnvelope<TType, TPayload>> Channel)> _subs =
        [];
    private readonly Lock _gate = new();
    private bool _disposed;
    private readonly BoundedChannelOptions? _boundedOptions;

    public InMemoryEventBus()
    {
    }

    public InMemoryEventBus(BoundedChannelOptions options)
    {
        _boundedOptions = options;
    }

    public IAsyncEnumerable<EventEnvelope<TType, TPayload>> Subscribe(EventFilter<TType, TPayload> filter, CancellationToken ct = default)
    {
        if (_disposed)
        {
            return Empty<EventEnvelope<TType, TPayload>>();
        }

        var channel = _boundedOptions is null
            ? Channel.CreateUnbounded<EventEnvelope<TType, TPayload>>()
            : Channel.CreateBounded<EventEnvelope<TType, TPayload>>(_boundedOptions);
        lock (_gate)
        {
            _subs.Add((filter, channel));
        }

        if (ct.CanBeCanceled)
        {
            ct.Register(() =>
            {
                lock (_gate)
                {
                    ClearSubscriptions(channel);
                }
                channel.Writer.TryComplete();
            });
        }

        return ReadAll(channel);
    }

    private static async IAsyncEnumerable<T> Empty<T>()
    {
        await Task.CompletedTask;
        yield break;
    }

    private void ClearSubscriptions(Channel<EventEnvelope<TType, TPayload>> channel)
    {
        for (int i = _subs.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_subs[i].Channel, channel))
            {
                _subs.RemoveAt(i);
                break;
            }
        }
    }

    private static async IAsyncEnumerable<EventEnvelope<TType, TPayload>> ReadAll(Channel<EventEnvelope<TType, TPayload>> ch)
    {
        while (await ch.Reader.WaitToReadAsync())
        {
            while (ch.Reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }

    public async ValueTask PublishAsync(EventEnvelope<TType, TPayload> evt, CancellationToken ct = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(InMemoryEventBus<TType, TPayload>));

        List<Channel<EventEnvelope<TType, TPayload>>> targets;
        lock (_gate)
        {
            targets = _subs
                .Where(s => Matches(s.Filter, evt))
                .Select(s => s.Channel)
                .ToList();
        }

        foreach (var channel in targets)
        {
            await channel.Writer.WriteAsync(evt, ct);
        }
    }

    private static bool Matches(EventFilter<TType, TPayload> f, EventEnvelope<TType, TPayload> e)
    {
        if (f.Types != null && f.Types.Count > 0 && !f.Types.Contains(e.Type))
            return false;
        if (f.Predicate != null && !f.Predicate(e))
            return false;
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        List<Channel<EventEnvelope<TType, TPayload>>> channels;
        lock (_gate)
        {
            _disposed = true;
            channels = _subs.Select(s => s.Channel).ToList();
            _subs.Clear();
        }

        foreach (var ch in channels)
        {
            ch.Writer.TryComplete();
        }
    }
}
