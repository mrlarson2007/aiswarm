using AISwarm.Infrastructure.Eventing;
using Shouldly;

namespace AISwarm.Tests.Eventing;

public class AgentNotificationServiceTests
{
    private readonly IEventBus<AgentEventType, IAgentLifecyclePayload> _bus =
        new InMemoryEventBus<AgentEventType, IAgentLifecyclePayload>();

    private IAgentNotificationService SystemUnderTest => new AgentNotificationService(_bus);

    [Fact(Timeout = 5000)]
    public async Task WhenPublishAgentRegistered_ShouldPublishEvent()
    {
        // Arrange
        var service = SystemUnderTest;
        var agentId = "agent-123";
        var persona = "implementer";

        // Subscribe BEFORE publishing the event
        var events = service.SubscribeForAllAgentEvents();
        await using var enumerator = events.GetAsyncEnumerator();

        // Act
        await service.PublishAgentRegistered(agentId, persona);

        // Assert - event should be received after publishing
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var hasEvent = await enumerator.MoveNextAsync().AsTask().WaitAsync(cts.Token);
        hasEvent.ShouldBeTrue();

        var evt = enumerator.Current;
        evt.Type.ShouldBe(AgentEventType.Registered);
        evt.Payload.AgentId.ShouldBe(agentId);
    }

    [Fact(Timeout = 5000)]
    public async Task WhenPublishAgentKilled_ShouldPublishEvent()
    {
        // Arrange
        var service = SystemUnderTest;
        var agentId = "agent-456";
        var reason = "User requested termination";

        // Subscribe BEFORE publishing the event
        var events = service.SubscribeForAllAgentEvents();
        await using var enumerator = events.GetAsyncEnumerator();

        // Act
        await service.PublishAgentKilled(agentId, reason);

        // Assert - event should be received after publishing
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var hasEvent = await enumerator.MoveNextAsync().AsTask().WaitAsync(cts.Token);
        hasEvent.ShouldBeTrue();

        var evt = enumerator.Current;
        evt.Type.ShouldBe(AgentEventType.Killed);
        evt.Payload.AgentId.ShouldBe(agentId);
    }

    [Fact(Timeout = 5000)]
    public async Task WhenPublishAgentStatusChanged_ShouldPublishEvent()
    {
        // Arrange
        var service = SystemUnderTest;
        var agentId = "agent-789";
        var oldStatus = "Running";
        var newStatus = "Idle";

        // Subscribe BEFORE publishing the event
        var events = service.SubscribeForAllAgentEvents();
        await using var enumerator = events.GetAsyncEnumerator();

        // Act
        await service.PublishAgentStatusChanged(agentId, oldStatus, newStatus);

        // Assert - event should be received after publishing
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var hasEvent = await enumerator.MoveNextAsync().AsTask().WaitAsync(cts.Token);
        hasEvent.ShouldBeTrue();

        var evt = enumerator.Current;
        evt.Type.ShouldBe(AgentEventType.StatusChanged);
        evt.Payload.AgentId.ShouldBe(agentId);
    }
}
