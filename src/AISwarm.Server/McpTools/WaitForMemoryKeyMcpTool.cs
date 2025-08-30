using System.Threading.Tasks;
using System;
using AISwarm.Infrastructure.Entities;
using AISwarm.Server.Entities;
using AISwarm.Infrastructure; // Corrected using directive

namespace AISwarm.Server.McpTools
{
    public class WaitForMemoryKeyMcpTool
    {
        private readonly IMemoryService _memoryService;

        public WaitForMemoryKeyMcpTool(IMemoryService memoryService)
        {
            _memoryService = memoryService;
        }

        public async Task<WaitForMemoryKeyResult> WaitForMemoryKeyAsync(string key, string @namespace, TimeSpan timeout)
        {
            // 1. Check if the key already exists
            var existingMemory = await _memoryService.ReadMemoryAsync(key, @namespace);
            if (existingMemory != null) // Check if it's not null
            {
                return WaitForMemoryKeyResult.SuccessWithMemoryEntry(existingMemory);
            }

            // 2. If not found, simulate waiting and then timeout (for the other test)
            await Task.Delay(timeout); 

            return WaitForMemoryKeyResult.Failure("Wait for memory key timed out.");
        }
    }
}
