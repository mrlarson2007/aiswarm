using System.ComponentModel;
using AISwarm.Infrastructure;
using AISwarm.Server.Entities;
using ModelContextProtocol.Server;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class SaveMemoryMcpTool(IMemoryService memoryService)
{
    [Description("Save data to memory for agent communication and state persistence")]
    public async Task<SaveMemoryResult> SaveMemory([Description("Key for the memory entry")] string key,
        [Description("Value to store")] string value,
        [Description("Content type (json, text, binary, etc.)")] string? type = null,
        [Description("Optional namespace for organization (default: 'default')")]
        string? @namespace = null)
    {
        if (string.IsNullOrEmpty(key))
        {
            return SaveMemoryResult.Failure("Error: key cannot be empty");
        }

        if (string.IsNullOrEmpty(value))
        {
            return SaveMemoryResult.Failure("Error: value cannot be empty");
        }

        await memoryService.SaveMemoryAsync(key, value, @namespace, type);
        return SaveMemoryResult.SuccessResult(key, @namespace);
    }
}
