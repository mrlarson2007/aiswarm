namespace AISwarm.Infrastructure.Eventing;

public record EventFilter<TType, TPayload>(
    IReadOnlyList<TType>? Types = null,
    Func<EventEnvelope<TType, TPayload>, bool>? Predicate = null);

public record TaskEventFilter(
    IReadOnlyList<TaskEventType>? Types = null,
    Func<EventEnvelope<TaskEventType, ITaskLifecyclePayload>, bool>? Predicate = null) :
        EventFilter<TaskEventType, ITaskLifecyclePayload>(Types, Predicate);
