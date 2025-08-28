namespace AISwarm.Infrastructure.Eventing;

public record EventEnvelope<TType, TPayload>(
    TType Type,
    DateTimeOffset Timestamp,
    TPayload Payload)
    where TType : struct, Enum
    where TPayload : class, IEventPayload;




