# TDD Plan: Event Bus Concurrency and Backpressure

This document outlines a Test-Driven Development plan to enhance the `InMemoryEventBus` with robust concurrency handling and backpressure management.

## 1. Failing Test: Per-Subscriber FIFO Under Concurrent Publishes

### Test Specification

- **Name:** `PublishAsync_WithConcurrentPublishes_MaintainsFifoOrderPerSubscriber`
- **Objective:** Verify that if multiple events are published concurrently, each subscriber receives them in the order they were published.

### Test Outline

1. **Arrange:**
    - Create an instance of `InMemoryEventBus`.
    - Subscribe a single subscriber to a specific event type.
    - Create a list of 1,000 events to be published.
    - Create a list to store the events received by the subscriber.

2. **Act:**
    - Start a `Task` to consume all events from the subscription and add them to the `receivedEvents` list.
    - Use `Task.WhenAll` to publish all 1,000 events concurrently from multiple threads.
    - Wait for the consumer task to finish processing all events.

3. **Assert:**
    - Assert that the `receivedEvents` list contains all 1,000 events.
    - Assert that the events in the `receivedEvents` list are in the same order as they were in the original `eventsToPublish` list.

## 2. Failing Test: Bounded Channel and Backpressure Behavior

### Test Specification

- **Name:** `PublishAsync_WithSlowSubscriberAndBoundedChannel_AppliesBackpressure`
- **Objective:** Verify that the `PublishAsync` operation blocks when a subscriber's channel is full, demonstrating backpressure.

### Test Outline

1. **Arrange:**
    - Modify `InMemoryEventBus` to accept a `BoundedChannelOptions` in its constructor, defaulting to unbounded.
    - Create an instance of `InMemoryEventBus` with a channel capacity of 1.
    - Subscribe a single subscriber.
    - Publish one event to fill the channel.

2. **Act:**
    - Start a `Task` for a second `PublishAsync` call.
    - Wait for a short period (e.g., 100ms) to see if the task completes.

3. **Assert:**
    - Assert that the second publish `Task` has not completed (i.e., it is waiting due to backpressure).
    - Read the event from the channel.
    - Assert that the second publish `Task` completes after the event is read.

## 3. Code Change Sketch for Implementation

### `InMemoryEventBus.cs`

```csharp
public class InMemoryEventBus : IEventBus, IDisposable
{
    // 1. Add a field for channel options
    private readonly BoundedChannelOptions _channelOptions;

    // 2. Update constructor to accept options
    public InMemoryEventBus(BoundedChannelOptions channelOptions = null)
    {
        _channelOptions = channelOptions ?? new BoundedChannelOptions(int.MaxValue) 
        { 
            FullMode = BoundedChannelFullMode.Wait 
        };
    }

    public IAsyncEnumerable<EventEnvelope> Subscribe(EventFilter filter, CancellationToken ct = default)
    {
        // ...
        // 3. Use options to create the channel
        var channel = Channel.CreateBounded<EventEnvelope>(_channelOptions);
        // ...
    }

    public async ValueTask PublishAsync(EventEnvelope evt, CancellationToken ct = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(InMemoryEventBus));

        List<Channel<EventEnvelope>> targets;
        lock (_gate)
        {
            targets = _subs
                .Where(s => Matches(s.Filter, evt))
                .Select(s => s.Channel)
                .ToList();
        }

        // 4. Use Task.WhenAll for concurrent publishing to subscribers
        var publishTasks = targets.Select(channel => channel.Writer.WriteAsync(evt, ct).AsTask());
        await Task.WhenAll(publishTasks);
    }

    // ...
}
```

## 4. Risks and Rollbacks

### Risks

1. **Breaking Changes:** Introducing `BoundedChannelOptions` could be a breaking change for consumers who construct `InMemoryEventBus` directly.
    - **Mitigation:** Provide a default constructor or default `null` value to maintain backward compatibility. The sketch above uses a default `null` value.
2. **Deadlocks:** Incorrect handling of concurrent publishing and channel writing could lead to deadlocks.
    - **Mitigation:** The use of `Task.WhenAll` is generally safe, but thorough testing with high concurrency is required. The TDD approach is designed to mitigate this.
3. **Performance Degradation:** `Task.WhenAll` introduces some overhead.
    - **Mitigation:** For the expected scale of this system, the overhead is negligible compared to the benefit of concurrent delivery. Performance testing can validate this.

### Rollback Plan

- If the new implementation introduces critical issues, we can revert the changes by:
    1. Reverting the `PublishAsync` method to its original sequential `foreach` loop.
    2. Removing the `BoundedChannelOptions` and reverting to `Channel.CreateUnbounded()`.
- Since the changes are localized to `InMemoryEventBus`, a rollback is straightforward and has a low impact on other parts of the system.
