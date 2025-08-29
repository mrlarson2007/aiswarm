namespace AISwarm.Infrastructure.Eventing;

public record TaskEventEnvelope(
    TaskEventType Type,
    DateTimeOffset Timestamp,
    ITaskLifecyclePayload Payload) :
    EventEnvelope<TaskEventType, ITaskLifecyclePayload>(Type, Timestamp, Payload);

public enum TaskEventType
{
    Created,
    Claimed,
    Completed,
    Failed
}
