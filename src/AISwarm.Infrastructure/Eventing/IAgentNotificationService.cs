namespace AISwarm.Infrastructure.Eventing;

public interface IAgentNotificationService
{
    ValueTask PublishAgentRegistered(string agentId, string? persona, CancellationToken ct = default);
    ValueTask PublishAgentKilled(string agentId, string? reason, CancellationToken ct = default);
    ValueTask PublishAgentStatusChanged(string agentId, string? oldStatus, string? newStatus, CancellationToken ct = default);
    IAsyncEnumerable<AgentEventEnvelope> SubscribeForAllAgentEvents(CancellationToken ct = default);
}
