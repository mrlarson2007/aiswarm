namespace AISwarm.Infrastructure.Eventing;

public interface IWorkItemNotificationService
{
    IAsyncEnumerable<EventEnvelope> SubscribeForAgent(string agentId, CancellationToken ct = default);
    IAsyncEnumerable<EventEnvelope> SubscribeForPersona(string persona, CancellationToken ct = default);
    IAsyncEnumerable<EventEnvelope> SubscribeForAgentOrPersona(string agentId, string persona, CancellationToken ct = default);
    IAsyncEnumerable<EventEnvelope> SubscribeForTaskIds(IReadOnlyList<string> taskIds, CancellationToken ct = default);
    IAsyncEnumerable<EventEnvelope> SubscribeForAllTaskEvents(CancellationToken ct = default);
    Task<string?> TryConsumeTaskCreatedAsync(string agentId, string persona, CancellationToken ct = default);
    ValueTask PublishTaskCreated(string taskId, string? agentId, string? persona, CancellationToken ct = default);
    ValueTask PublishTaskCompleted(string taskId, string? agentId, CancellationToken ct = default);
    ValueTask PublishTaskFailed(string taskId, string? agentId, string? reason, CancellationToken ct = default);
}
