namespace AISwarm.Infrastructure.Eventing;

public record EventFilter<TType, TPayload>(
    IReadOnlyList<TType>? Types = null,
    Func<EventEnvelope<TType, TPayload>, bool>? Predicate = null)
    where TType : struct, Enum
    where TPayload : class, IEventPayload;

public record TaskEventFilter(
    IReadOnlyList<TaskEventType>? Types = null,
    Func<EventEnvelope<TaskEventType, ITaskLifecyclePayload>, bool>? Predicate = null) :
    EventFilter<TaskEventType, ITaskLifecyclePayload>(Types, Predicate);

public record AgentEventFilter(
    IReadOnlyList<AgentEventType>? Types = null,
    Func<EventEnvelope<AgentEventType, IAgentLifecyclePayload>, bool>? Predicate = null) :
    EventFilter<AgentEventType, IAgentLifecyclePayload>(Types, Predicate);
