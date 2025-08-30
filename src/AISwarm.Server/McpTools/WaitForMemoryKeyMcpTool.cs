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

        /// <summary>
        /// Waits for a memory key to be created. If the key already exists, returns immediately.
        /// </summary>
        public async Task<WaitForMemoryKeyResult> WaitForMemoryKeyCreationAsync(string key, string @namespace, TimeSpan timeout)
        {
            // 1. Check if the key already exists
            var existingMemory = await _memoryService.ReadMemoryAsync(key, @namespace);
            if (existingMemory != null)
            {
                return WaitForMemoryKeyResult.SuccessWithMemoryEntry(existingMemory);
            }

            // since memory does not exist, we need to wait for creation event
            var filter = new EventFilter<MemoryEventType, IMemoryLifecyclePayload>
            {
                Predicate = envelope =>
                    envelope.Payload.MemoryEntry.Key == key &&
                    envelope.Payload.MemoryEntry.Namespace == @namespace &&
                    envelope.Type == MemoryEventType.Created
            };

            // Create a CancellationTokenSource for the overall timeout
            using var overallCts = new CancellationTokenSource(timeout);

            try
            {
                var createdValue = await _memoryEventBus.Subscribe(filter, overallCts.Token).FirstOrDefaultAsync(overallCts.Token);
                if (createdValue == null)
                {
                    return WaitForMemoryKeyResult.Failure("Wait for memory key timed out.");
                }

                return WaitForMemoryKeyResult.SuccessWithMemoryEntry(new MemoryEntryDto(
                    createdValue.Payload.MemoryEntry.Key,
                    createdValue.Payload.MemoryEntry.Value,
                    createdValue.Payload.MemoryEntry.Namespace,
                    createdValue.Payload.MemoryEntry.Type,
                    createdValue.Payload.MemoryEntry.Size,
                    createdValue.Payload.MemoryEntry.Metadata));
            }
            catch (OperationCanceledException)
            {
                return WaitForMemoryKeyResult.Failure("Wait for memory key timed out.");
            }
        }

        /// <summary>
        /// Waits for a memory key to be updated. Always waits for the next update event, even if the key already exists.
        /// </summary>
        public async Task<WaitForMemoryKeyResult> WaitForMemoryKeyUpdateAsync(string key, string @namespace, TimeSpan timeout)
        {
            // Always subscribe to Update events and wait for changes
            var filter = new EventFilter<MemoryEventType, IMemoryLifecyclePayload>
            {
                Predicate = envelope =>
                    envelope.Payload.MemoryEntry.Key == key &&
                    envelope.Payload.MemoryEntry.Namespace == @namespace &&
                    envelope.Type == MemoryEventType.Updated
            };

            // Create a CancellationTokenSource for the overall timeout
            using var overallCts = new CancellationTokenSource(timeout);

            try
            {
                var updatedValue = await _memoryEventBus.Subscribe(filter, overallCts.Token).FirstOrDefaultAsync(overallCts.Token);
                if (updatedValue == null)
                {
                    return WaitForMemoryKeyResult.Failure("Wait for memory key timed out.");
                }

                return WaitForMemoryKeyResult.SuccessWithMemoryEntry(new MemoryEntryDto(
                    updatedValue.Payload.MemoryEntry.Key,
                    updatedValue.Payload.MemoryEntry.Value,
                    updatedValue.Payload.MemoryEntry.Namespace,
                    updatedValue.Payload.MemoryEntry.Type,
                    updatedValue.Payload.MemoryEntry.Size,
                    updatedValue.Payload.MemoryEntry.Metadata));
            }
            catch (OperationCanceledException)
            {
                return WaitForMemoryKeyResult.Failure("Wait for memory key timed out.");
            }
        }
    }
}
