using System.ComponentModel;
using AISwarm.Infrastructure;
using AISwarm.Server.Entities;
using ModelContextProtocol.Server;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class ReadMemoryMcpTool(IMemoryService memoryService)
{
    [McpServerTool(Name = "read_memory")]
    [Description("Reads a stored memory entry from the memory system")]
    public async Task<ReadMemoryResult> ReadMemoryAsync(
        [Description("The key of the memory to read")]
        string key,
        [Description("The namespace of the memory (defaults to empty string)")]
        string @namespace = "")
    {
        if (string.IsNullOrEmpty(key)) return ReadMemoryResult.Failure("key required");

        var entry = await memoryService.ReadMemoryAsync(key, @namespace);

        if (entry == null) return ReadMemoryResult.Failure("memory not found");

        // Update access tracking when memory is successfully read
        await memoryService.UpdateMemoryAccessAsync(key, @namespace);

        return ReadMemoryResult.SuccessResult(entry);
    }
}
