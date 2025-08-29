namespace AISwarm.Infrastructure.Eventing;

public record AgentEventEnvelope(
    AgentEventType Type,
    DateTimeOffset Timestamp,
    IAgentLifecyclePayload Payload) :
    EventEnvelope<AgentEventType, IAgentLifecyclePayload>(Type, Timestamp, Payload);
