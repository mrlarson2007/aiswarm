namespace AISwarm.Infrastructure.Eventing;

public record EventFilter<TType, TPayload>(
    IReadOnlyList<TType>? Types = null,
    Func<EventEnvelope<TType, TPayload>, bool>? Predicate = null)
    where TType : struct, Enum
    where TPayload : class, IEventPayload;
