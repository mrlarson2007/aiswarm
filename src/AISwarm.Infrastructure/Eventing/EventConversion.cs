namespace AISwarm.Infrastructure.Eventing;

/// <summary>
///     Utility class for common event conversion patterns
/// </summary>
public static class EventConversion
{
    public static async IAsyncEnumerable<TConcreteEnvelope> ConvertToConcreteEnvelope<TEventType, TPayload,
        TConcreteEnvelope>(
        IAsyncEnumerable<EventEnvelope<TEventType, TPayload>> source,
        Func<TEventType, DateTimeOffset, TPayload, TConcreteEnvelope> factory)
        where TEventType : struct, Enum
        where TPayload : class, IEventPayload
    {
        await foreach (var e in source)
            yield return factory(e.Type, e.Timestamp, e.Payload);
    }
}
