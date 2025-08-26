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

    [Fact(Timeout = 5000)]
    public async Task WhenBusIsDisposed_SubscriptionsComplete_AndFurtherPublishesFail()
    {
        // Arrange
        var agentId = "agent-dispose";
        var service = SystemUnderTest;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var completed = false;

        // Act: subscribe and then dispose bus, expect enumeration to complete
        var readTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in service.SubscribeForAgent(agentId, cts.Token).WithCancellation(cts.Token))
                {
                    // consume until completion
                }
                completed = true;
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(5, cts.Token);

        // Dispose bus via cast (test-only): InMemoryEventBus implements IDisposable in next step
        ( _bus as IDisposable )?.Dispose();

        await Task.Delay(20);
        cts.Cancel();
        try { await readTask; } catch (OperationCanceledException) { }

        completed.ShouldBeTrue();

        // Publishing after disposal should fail
        await Should.ThrowAsync<ObjectDisposedException>(async () =>
        {
            await service.PublishTaskCreated("post-dispose", agentId, persona: null);
        });
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

    [Fact(Timeout = 5000)]
    public async Task WhenCancellingSubscription_ShouldStopReceivingEvents()
    {
        // Arrange
        var agentId = "agent-cancel";
        var service = SystemUnderTest;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var received = new List<EventEnvelope>();

        // Act - start reading, then publish one event, cancel, then publish another
        var readTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in service.SubscribeForAgent(agentId, cts.Token))
                {
                    received.Add(evt);
                }
            }
            catch (OperationCanceledException)
            {
                // expected when the token is cancelled
            }
        });

        // Give the subscription a moment to start
        await Task.Delay(5, cts.Token);

        // Publish first event (should be received)
        await service.PublishTaskCreated(taskId: "t1", agentId: agentId, persona: "reviewer", CancellationToken.None);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (received.Count < 1 && sw.Elapsed < TimeSpan.FromMilliseconds(500))
        {
            await Task.Delay(5, cts.Token);
        }

        // Cancel subscription and then publish another event (should NOT be received)
        cts.Cancel();
        await Task.Delay(10, CancellationToken.None);
        await service.PublishTaskCreated(taskId: "t2", agentId: agentId, persona: "reviewer", CancellationToken.None);

        // Wait for reader to finish
        try { await readTask; } catch (OperationCanceledException) { }

        // Assert
        received.Count.ShouldBe(1);
        var payload = (TaskCreatedPayload)received[0].Payload!;
        payload.TaskId.ShouldBe("t1");
    }

    [Fact(Timeout = 5000)]
    public async Task WhenPublishingTaskForSpecificAgent_PersonaSubscriberShouldNotReceiveIt()
    {
        // Arrange
        var persona = "reviewer";
        var service = SystemUnderTest;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var received = new List<EventEnvelope>();

        // Act: subscribe to persona, then publish one agent-assigned and one unassigned event
        var readTask = Task.Run(async () =>
        {
            await foreach (var evt in service.SubscribeForPersona(persona, cts.Token).WithCancellation(cts.Token))
            {
                received.Add(evt);
                if (received.Count >= 2) break; // safety
            }
        }, cts.Token);

        await Task.Delay(5, cts.Token);

        // Agent-assigned with same persona (should NOT be delivered to persona subscriber)
        await service.PublishTaskCreated(taskId: "t-agent", agentId: "agent-99", persona: persona);

        // Unassigned with same persona (should be delivered)
        await service.PublishTaskCreated(taskId: "t-unassigned", agentId: null, persona: persona);

        // Wait briefly for deliveries
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (received.Count < 1 && sw.Elapsed < TimeSpan.FromMilliseconds(500))
        {
            await Task.Delay(5, cts.Token);
        }

        cts.Cancel();
        try { await readTask; } catch (OperationCanceledException) { }

        // Assert: only the unassigned task should be received
        received.Count.ShouldBe(1);
        var payload = (TaskCreatedPayload)received[0].Payload!;
        payload.TaskId.ShouldBe("t-unassigned");
        payload.AgentId.ShouldBeNull();
        payload.Persona.ShouldBe(persona);
    }

    [Fact]
    public void WhenPublishingWithNullTaskId_ShouldThrowArgumentException()
    {
        // Arrange
        var service = SystemUnderTest;

        // Act
        var act = () => service.PublishTaskCreated(taskId: null!, agentId: null, persona: null).AsTask();

        // Assert
        var ex = Should.Throw<ArgumentException>(() => act());
        ex.Message.ShouldContain("taskId");
    }

}
