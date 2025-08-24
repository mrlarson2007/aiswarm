namespace AISwarm.Infrastructure.Eventing;

public interface IWorkItemNotificationService
{
    IAsyncEnumerable<EventEnvelope> SubscribeForAgent(string agentId, CancellationToken ct = default);
    IAsyncEnumerable<EventEnvelope> SubscribeForPersona(string persona, CancellationToken ct = default);
    ValueTask PublishTaskCreated(string taskId, string? agentId, string? persona, CancellationToken ct = default);
}
