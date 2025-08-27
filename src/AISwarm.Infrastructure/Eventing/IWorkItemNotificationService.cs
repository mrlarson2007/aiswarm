namespace AISwarm.Infrastructure.Eventing;

public interface IWorkItemNotificationService
{
    IAsyncEnumerable<TaskEventEnvelope> SubscribeForAgent(string agentId, CancellationToken ct = default);
    IAsyncEnumerable<TaskEventEnvelope> SubscribeForPersona(string persona, CancellationToken ct = default);
    IAsyncEnumerable<TaskEventEnvelope> SubscribeForAgentOrPersona(string agentId, string persona, CancellationToken ct = default);
    IAsyncEnumerable<TaskEventEnvelope> SubscibeForTaskCompletion(IReadOnlyList<string> taskIds, CancellationToken ct = default);
    IAsyncEnumerable<TaskEventEnvelope> SubscribeForAllTaskEvents(CancellationToken ct = default);
    Task<string?> TryConsumeTaskCreatedAsync(string agentId, string persona, CancellationToken ct = default);
    ValueTask PublishTaskCreated(string taskId, string? agentId, string? persona, CancellationToken ct = default);
    ValueTask PublishTaskCompleted(string taskId, string? agentId, CancellationToken ct = default);
    ValueTask PublishTaskFailed(string taskId, string? agentId, string? reason, CancellationToken ct = default);
}
