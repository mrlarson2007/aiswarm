using System.Threading.Channels;

namespace AISwarm.Infrastructure.Eventing;

public class InMemoryEventBus : IEventBus
{
    private readonly List<(EventFilter Filter, Channel<EventEnvelope> Channel)> _subs = new();
    private readonly object _gate = new();

    public IAsyncEnumerable<EventEnvelope> Subscribe(EventFilter filter, CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<EventEnvelope>();
        lock (_gate)
        {
            _subs.Add((filter, channel));
        }

        return ReadAll(channel, ct);
    }

    private static async IAsyncEnumerable<EventEnvelope> ReadAll(Channel<EventEnvelope> ch, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (await ch.Reader.WaitToReadAsync(ct))
        {
            while (ch.Reader.TryRead(out var item))
            {
                yield return item;
            }
        }
    }

    public async ValueTask PublishAsync(EventEnvelope evt, CancellationToken ct = default)
    {
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
        if (f.Types != null && f.Types.Count > 0 && !f.Types.Contains(e.Type)) return false;
        if (f.Predicate != null && !f.Predicate(e)) return false;
        return true;
    }
}
