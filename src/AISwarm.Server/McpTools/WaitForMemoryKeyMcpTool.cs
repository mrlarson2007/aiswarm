using AISwarm.Infrastructure.Entities;
using AISwarm.Server.Entities;
using AISwarm.Infrastructure; // Corrected using directive
using AISwarm.Infrastructure.Eventing;
using ModelContextProtocol.Server;
using System.ComponentModel; // Added for IEventBus

namespace AISwarm.Server.McpTools
{
    [McpServerToolType]
    [Description("Tools to wait for memory key creation or updates")]
    public class WaitForMemoryKeyMcpTool(
        IMemoryService memoryService,
        IEventBus<MemoryEventType, IMemoryLifecyclePayload> memoryEventBus)
    {


        /// <summary>
        /// Waits for a memory key to be created. If the key already exists, returns immediately.
        /// </summary>
        [McpServerTool(Name = "wait_for_memory_key_creation")]
        [Description("Tool to wait for a memory key to be created")]
        public async Task<WaitForMemoryKeyResult> WaitForMemoryKeyCreationAsync(
            [Description("Key for the memory entry")] string key,
            [Description("Namespace for the memory entry")] string @namespace,
            [Description("Timeout duration to wait for the memory key creation in milliseconds, optional parameter")] long timeoutMs = 30000)
        {
            // 1. Check if the key already exists
            var existingMemory = await memoryService.ReadMemoryAsync(key, @namespace);
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
            using var overallCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

            try
            {
                var createdValue = await memoryEventBus.Subscribe(filter, overallCts.Token).FirstOrDefaultAsync(overallCts.Token);
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
        [McpServerTool(Name = "wait_for_memory_key_update")]
        [Description("Tool to wait for a memory key to be updated")]
        public async Task<WaitForMemoryKeyResult> WaitForMemoryKeyUpdateAsync(
            [Description("Key for the memory entry")] string key,
            [Description("Namespace for the memory entry")] string @namespace,
            [Description("Timeout duration to wait for the memory key creation in milliseconds, optional parameter")] long timeoutMs = 30000)
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
            using var overallCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));

            try
            {
                var updatedValue = await memoryEventBus.Subscribe(filter, overallCts.Token).FirstOrDefaultAsync(overallCts.Token);
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
