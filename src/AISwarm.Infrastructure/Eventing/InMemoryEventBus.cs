using System.Threading.Channels;

namespace AISwarm.Infrastructure.Eventing;

public class InMemoryEventBus : IEventBus, IDisposable
{
    private readonly List<(EventFilter Filter, Channel<EventEnvelope> Channel)> _subs = new();
    private readonly object _gate = new();
    private bool _disposed;

    public IAsyncEnumerable<EventEnvelope> Subscribe(EventFilter filter, CancellationToken ct = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(InMemoryEventBus));

        var channel = Channel.CreateUnbounded<EventEnvelope>();
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

        return ReadAll(channel, ct);
    }

    private void ClearSubscriptions(Channel<EventEnvelope> channel)
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

    private static async IAsyncEnumerable<EventEnvelope> ReadAll(Channel<EventEnvelope> ch, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (await ch.Reader.WaitToReadAsync())
        {
            while (ch.Reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }

    public async ValueTask PublishAsync(EventEnvelope evt, CancellationToken ct = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(InMemoryEventBus));

        List<Channel<EventEnvelope>> targets;
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

    private static bool Matches(EventFilter f, EventEnvelope e)
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

        List<Channel<EventEnvelope>> channels;
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
