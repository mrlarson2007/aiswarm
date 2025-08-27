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

    private static async Task WaitForCountAsync(List<EventEnvelope> list, int expected, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (list.Count < expected && sw.Elapsed < TimeSpan.FromMilliseconds(500))
        {
            await Task.Delay(5, ct);
        }
    }

    [Fact(Timeout = 5000)]
    public async Task SubscribeForAgentOrPersona_ShouldDeliverForEitherMatch()
    {
        // Arrange
        var agentId = "agent-combined";
        var persona = "planner";
        var service = SystemUnderTest;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var token = cts.Token;

        var received = new List<EventEnvelope>();

        // Act
        var readTask = Task.Run(async () =>
        {
            await foreach (var evt in service.SubscribeForAgentOrPersona(agentId, persona, token))
            {
                received.Add(evt);
                if (received.Count >= 2)
                    break; // expect 2 events
            }
        }, token);

        await Task.Delay(5, token);
        await service.PublishTaskCreated("t-agent", agentId, persona: null);
        await service.PublishTaskCreated("t-persona", agentId: null, persona: persona);

        await WaitForCountAsync(received, 2, token);
        cts.Cancel();
        try
        {
            await readTask;
        }
        catch (OperationCanceledException) { }

        // Assert
        received.Count.ShouldBe(2);
        var ids = received.Select(e => ((TaskCreatedPayload)e.Payload!).TaskId).OrderBy(x => x).ToArray();
        ids.ShouldBe(new[] { "t-agent", "t-persona" });
    }

    [Fact(Timeout = 5000)]
    public async Task TryConsumeTaskCreatedAsync_ShouldReturnNullWhenNoEventAndNotBlock()
    {
        // Arrange
        var agentId = "agent-peek";
        var persona = "reviewer";
        var service = SystemUnderTest;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var token = cts.Token;

        // Act
        var start = DateTime.UtcNow;
        var peek = await service.TryConsumeTaskCreatedAsync(agentId, persona, token);
        var elapsed = DateTime.UtcNow - start;

        // Assert: no event immediately available and should return quickly
        peek.ShouldBeNull();
        elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(150));

        // Note: publishing before subscribing won't be observed by TryConsume,
        // since subscriptions are not retroactive.
    }
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
    public async Task PublishAsync_WithSlowSubscriberAndBoundedChannel_AppliesBackpressure()
    {
        // Arrange: bounded capacity 1 and slow subscriber
        var options = new System.Threading.Channels.BoundedChannelOptions(1)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
        };

        var bus = new InMemoryEventBus(options);
        var service = new WorkItemNotificationService(bus);
        var agentId = "agent-backpressure";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var token = cts.Token;

        // Create subscription (channel exists) but do not consume yet
        var stream = service.SubscribeForAgent(agentId, token);

        // Act: publish first event to fill buffer
        await service.PublishTaskCreated("t1", agentId, persona: null, CancellationToken.None);

        // Second publish should block until we release the reader
        var secondPublish = service.PublishTaskCreated("t2", agentId, persona: null, CancellationToken.None).AsTask();

        // Assert: verify it hasn't completed quickly (indicating backpressure)
        await Task.Delay(100, token);
        secondPublish.IsCompleted.ShouldBeFalse();

        // Now drain one item to free capacity and ensure the second publish completes
        var e = stream.GetAsyncEnumerator(token);
        try
        {
            var moved = await e.MoveNextAsync();
            moved.ShouldBeTrue();
        }
        finally
        {
            await e.DisposeAsync();
        }
        await secondPublish;
    }
    [Fact(Timeout = 5000)]
    public async Task WhenBusIsDisposed_SubscriptionsComplete_AndFurtherPublishesFail()
    {
        // Arrange
        var agentId = "agent-dispose";
        var service = SystemUnderTest;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var token = cts.Token;

        var completed = false;

        // Act: subscribe and then dispose bus, expect enumeration to complete
        var readTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in service.SubscribeForAgent(agentId, token))
                {
                    // consume until completion
                }
                completed = true;
            }
            catch (OperationCanceledException) { }
        }, token);

        await Task.Delay(5, cts.Token);

        // Dispose bus via cast (test-only): InMemoryEventBus implements IDisposable in next step
        (_bus as IDisposable)?.Dispose();

        await Task.Delay(20);
        cts.Cancel();
        try
        {
            await readTask;
        }
        catch (OperationCanceledException) { }

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
        var token = cts.Token;

        // Act
        var received = new List<EventEnvelope>();
        var readTask = Task.Run(async () =>
        {
            await foreach (var evt in service.SubscribeForAgent(agentId, token))
            {
                received.Add(evt);
                break; // we only need the first event
            }
        }, token);

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
        var token = cts.Token;

        var received = new List<EventEnvelope>();

        // Act - start reading, then publish one event, cancel, then publish another
        var readTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in service.SubscribeForAgent(agentId, token))
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
        await WaitForCountAsync(received, 1, cts.Token);

        // Cancel subscription and then publish another event (should NOT be received)
        cts.Cancel();
        await Task.Delay(10, CancellationToken.None);
        await service.PublishTaskCreated(taskId: "t2", agentId: agentId, persona: "reviewer", CancellationToken.None);

        // Wait for reader to finish
        try
        {
            await readTask;
        }
        catch (OperationCanceledException) { }

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
        var token = cts.Token;

        var received = new List<EventEnvelope>();

        // Act: subscribe to persona, then publish one agent-assigned and one unassigned event
        var readTask = Task.Run(async () =>
        {
            await foreach (var evt in service.SubscribeForPersona(persona, token))
            {
                received.Add(evt);
                if (received.Count >= 2)
                    break; // safety
            }
        }, token);

        await Task.Delay(5, cts.Token);

        // Agent-assigned with same persona (should NOT be delivered to persona subscriber)
        await service.PublishTaskCreated(taskId: "t-agent", agentId: "agent-99", persona: persona);

        // Unassigned with same persona (should be delivered)
        await service.PublishTaskCreated(taskId: "t-unassigned", agentId: null, persona: persona);

        // Wait briefly for deliveries
        await WaitForCountAsync(received, 1, cts.Token);

        cts.Cancel();
        try
        {
            await readTask;
        }
        catch (OperationCanceledException) { }

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

    [Fact(Timeout = 5000)]
    public async Task WhenCancellingSubscription_ShouldCompleteEnumerationWithoutException()
    {
        // Arrange
        var agentId = "agent-cancel-graceful";
        var service = SystemUnderTest;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var token = cts.Token;

        var received = new List<EventEnvelope>();
        bool completed = false;
        Exception? captured = null;

        // Act
        var readTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in service.SubscribeForAgent(agentId, token))
                {
                    received.Add(evt);
                }
                completed = true;
            }
            catch (OperationCanceledException ex)
            {
                captured = ex;
            }
        });

        await Task.Delay(5, cts.Token);
        await service.PublishTaskCreated("t1", agentId, persona: null);

        await WaitForCountAsync(received, 1, cts.Token);

        // Cancel and ensure enumeration completes without OCE
        cts.Cancel();
        try
        {
            await readTask;
        }
        catch (OperationCanceledException) { }

        // Assert
        completed.ShouldBeTrue();
        captured.ShouldBeNull();
    }

    [Fact]
    public void WhenSubscribingForTaskIdsWithEmptyList_ShouldThrowArgumentException()
    {
        // Arrange
        var service = SystemUnderTest;
        var emptyTaskIds = Array.Empty<string>();

        // Act & Assert
        Should.Throw<ArgumentException>(() => 
            service.SubscribeForTaskIds(emptyTaskIds).GetAsyncEnumerator().MoveNextAsync().AsTask());
    }

    [Fact(Timeout = 5000)]
    public async Task WhenSubscribingForTaskIds_ShouldDeliverEventsForMatchingTaskIds()
    {
        // Arrange
        var service = SystemUnderTest;
        var targetTaskIds = new[] { "task-1", "task-3" };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var token = cts.Token;

        var received = new List<EventEnvelope>();

        // Act
        var readTask = Task.Run(async () =>
        {
            await foreach (var evt in service.SubscribeForTaskIds(targetTaskIds, token))
            {
                received.Add(evt);
                if (received.Count >= 4) // Expect 4 events: 2 created, 1 completed, 1 failed
                    break;
            }
        }, token);

        await Task.Delay(5, token); // Give subscription time to start

        // Publish events - some matching, some not
        await service.PublishTaskCreated("task-1", "agent-a", "reviewer"); // Should match
        await service.PublishTaskCreated("task-2", "agent-b", "planner");  // Should NOT match
        await service.PublishTaskCreated("task-3", "agent-c", "implementer"); // Should match
        await service.PublishTaskCompleted("task-1", "agent-a"); // Should match
        await service.PublishTaskFailed("task-3", "agent-c", "error"); // Should match
        await service.PublishTaskCompleted("task-2", "agent-b"); // Should NOT match

        await WaitForCountAsync(received, 4, token);
        cts.Cancel();
        
        try
        {
            await readTask;
        }
        catch (OperationCanceledException) { }

        // Assert
        received.Count.ShouldBe(4);
        
        var receivedTaskIds = received.Select(e => 
            e.Payload switch
            {
                TaskCreatedPayload p => p.TaskId,
                TaskCompletedPayload p => p.TaskId,
                TaskFailedPayload p => p.TaskId,
                _ => throw new InvalidOperationException("Unexpected payload type")
            }).ToArray();

        receivedTaskIds.ShouldAllBe(taskId => targetTaskIds.Contains(taskId));
        
        // Verify we got the expected event types
        var eventTypes = received.Select(e => e.Type).ToArray();
        eventTypes.Count(t => t == "TaskCreated").ShouldBe(2);
        eventTypes.Count(t => t == "TaskCompleted").ShouldBe(1);
        eventTypes.Count(t => t == "TaskFailed").ShouldBe(1);
    }

    [Fact(Timeout = 5000)]
    public async Task WhenSubscribingForAllTaskEvents_ShouldReceiveAllTaskLifecycleEventsWithoutFiltering()
    {
        // Arrange
        var service = SystemUnderTest;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var token = cts.Token;

        var received = new List<EventEnvelope>();

        // Act
        var readTask = Task.Run(async () =>
        {
            await foreach (var evt in service.SubscribeForAllTaskEvents(token))
            {
                received.Add(evt);
                if (received.Count >= 5) // Expect all 5 events
                    break;
            }
        }, token);

        await Task.Delay(5, token); // Give subscription time to start

        // Publish various task events - all should be received
        await service.PublishTaskCreated("task-alpha", "agent-1", "reviewer");
        await service.PublishTaskCreated("task-beta", "agent-2", "planner");
        await service.PublishTaskCompleted("task-alpha", "agent-1");
        await service.PublishTaskFailed("task-beta", "agent-2", "timeout");
        await service.PublishTaskCreated("task-gamma", null, "implementer"); // Unassigned task

        await WaitForCountAsync(received, 5, token);
        cts.Cancel();
        
        try
        {
            await readTask;
        }
        catch (OperationCanceledException) { }

        // Assert
        received.Count.ShouldBe(5);
        
        // Verify all event types are present
        var eventTypes = received.Select(e => e.Type).ToArray();
        eventTypes.Count(t => t == "TaskCreated").ShouldBe(3);
        eventTypes.Count(t => t == "TaskCompleted").ShouldBe(1);
        eventTypes.Count(t => t == "TaskFailed").ShouldBe(1);
        
        // Verify all task IDs are present (no filtering)
        var receivedTaskIds = received.Select(e => 
            e.Payload switch
            {
                TaskCreatedPayload p => p.TaskId,
                TaskCompletedPayload p => p.TaskId,
                TaskFailedPayload p => p.TaskId,
                _ => throw new InvalidOperationException("Unexpected payload type")
            }).ToArray();
            
        receivedTaskIds.ShouldContain("task-alpha");
        receivedTaskIds.ShouldContain("task-beta");
        receivedTaskIds.ShouldContain("task-gamma");
    }




}
