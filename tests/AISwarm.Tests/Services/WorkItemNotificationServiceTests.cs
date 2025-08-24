using System.Runtime.CompilerServices;
using AISwarm.Infrastructure.Eventing;
using Shouldly;
using Xunit;

namespace AISwarm.Tests.Services;

public class WorkItemNotificationServiceTests
{
    // Per-test-instance setup (xUnit creates a new class instance per test)
    private readonly IEventBus _bus = new InMemoryEventBus();
    private IWorkItemNotificationService SystemUnderTest => new WorkItemNotificationService(_bus);

    [Fact]
    public void WhenSubscribingWithNullPersona_ShouldThrowArgumentException()
    {
        // Arrange
        var service = SystemUnderTest;

        // Act
        var act = () => service.SubscribeForPersona(null!);

        // Assert
        var ex = Should.Throw<ArgumentException>(() => act().GetAsyncEnumerator().MoveNextAsync().AsTask());
        ex.Message.ShouldContain("persona");
    }

    [Fact]
    public void WhenSubscribingWithNullAgentId_ShouldThrowArgumentException()
    {
        // Arrange
        var service = SystemUnderTest;

        // Act
        var act = () => service.SubscribeForAgent(null!);

        // Assert
        var ex = Should.Throw<ArgumentException>(() => act().GetAsyncEnumerator().MoveNextAsync().AsTask());
        ex.Message.ShouldContain("agentId");
    }

    [Fact(Timeout = 5000)]
    public async Task WhenPublishingTaskCreatedForAgent_ShouldDeliverToAgentSubscription()
    {
        // Arrange
        var agentId = "agent-123";
        var taskId = "task-abc";
        var persona = "reviewer";
        var service = SystemUnderTest;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        var received = new List<EventEnvelope>();
        var readTask = Task.Run(async () =>
        {
            await foreach (var evt in service.SubscribeForAgent(agentId, cts.Token).WithCancellation(cts.Token))
            {
                received.Add(evt);
                break; // we only need the first event
            }
        }, cts.Token);

        await Task.Delay(5, cts.Token); // give subscription a moment
    await service.PublishTaskCreated(taskId, agentId, persona);

        await readTask;

        // Assert
        received.Count.ShouldBe(1);
        var payload = (TaskCreatedPayload)received[0].Payload!;
        payload.TaskId.ShouldBe(taskId);
        payload.AgentId.ShouldBe(agentId);
        payload.Persona.ShouldBe(persona);
    }
}
