using System.ComponentModel;
using AISwarm.Server.Entities;
using ModelContextProtocol.Server;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class SaveMemoryMcpTool()
{
    [Description("Save data to memory for agent communication and state persistence")]
    public Task<SaveMemoryResult> SaveMemory(
        [Description("Key for the memory entry")] string key,
        [Description("Value to store")] string value,
        [Description("Optional namespace for organization (default: 'default')")] string? @namespace = null)
    {

        if (string.IsNullOrEmpty(value))
        {
            return Task.FromResult(SaveMemoryResult.Failure("Error: value cannot be empty"));
        }

        return Task.FromResult(SaveMemoryResult.Failure("Error: key cannot be empty"));
    }
}
