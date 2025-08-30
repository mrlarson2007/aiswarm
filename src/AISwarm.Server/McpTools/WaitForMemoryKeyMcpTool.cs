using System.Threading.Tasks;
using System;
using AISwarm.Infrastructure.Entities;
using AISwarm.Server.Entities;
using AISwarm.Infrastructure; // Corrected using directive
using AISwarm.Infrastructure.Eventing; // Added for IEventBus

namespace AISwarm.Server.McpTools
{
    public class WaitForMemoryKeyMcpTool
{
    private readonly IMemoryService _memoryService;
    private readonly IEventBus<MemoryEventType, IMemoryLifecyclePayload> _memoryEventBus; // Added

    public WaitForMemoryKeyMcpTool(IMemoryService memoryService, IEventBus<MemoryEventType, IMemoryLifecyclePayload> memoryEventBus)
    {
        _memoryService = memoryService;
        _memoryEventBus = memoryEventBus;
    }

        public async Task<WaitForMemoryKeyResult> WaitForMemoryKeyAsync(string key, string @namespace, TimeSpan timeout)
        {
            // 1. Check if the key already exists
            var existingMemory = await _memoryService.ReadMemoryAsync(key, @namespace);
            if (existingMemory != null)
            {
                return WaitForMemoryKeyResult.SuccessWithMemoryEntry(existingMemory);
            }

            // 2. If not found, subscribe to events and wait with a timeout
            var filter = new EventFilter<MemoryEventType, IMemoryLifecyclePayload>
            {
                Predicate = envelope =>
                    envelope.Payload.MemoryEntry.Key == key &&
                    envelope.Payload.MemoryEntry.Namespace == @namespace &&
                    (envelope.Type == MemoryEventType.Created || envelope.Type == MemoryEventType.Updated)
            };

            // Create a CancellationTokenSource for the overall timeout
            using var overallCts = new CancellationTokenSource(timeout);

            // Create a TaskCompletionSource to signal when the event is received
            var eventReceivedTcs = new TaskCompletionSource<MemoryEntryDto>();

            // Start a task to consume events from the bus
            var consumerTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var envelope in _memoryEventBus.Subscribe(filter, overallCts.Token))
                    {
                        eventReceivedTcs.TrySetResult(envelope.Payload.MemoryEntry);
                        break; // Found the event, stop consuming
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected if overallCts is cancelled
                    eventReceivedTcs.TrySetCanceled(overallCts.Token);
                }
                catch (Exception ex)
                {
                    eventReceivedTcs.TrySetException(ex);
                }
            }, overallCts.Token);

            // Wait for either the event to be received or the timeout to occur
            var completedTask = await Task.WhenAny(eventReceivedTcs.Task, Task.Delay(timeout, overallCts.Token));

            if (completedTask == eventReceivedTcs.Task)
            {
                // Event was received
                overallCts.Cancel(); // Cancel the consumer task
                return WaitForMemoryKeyResult.SuccessWithMemoryEntry(await eventReceivedTcs.Task);
            }
            else
            {
                // Timeout occurred
                overallCts.Cancel(); // Cancel the consumer task
                return WaitForMemoryKeyResult.Failure("Wait for memory key timed out.");
            }
        }
    }
}
