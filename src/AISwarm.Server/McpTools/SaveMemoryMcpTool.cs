using System.ComponentModel;
using AISwarm.Infrastructure;
using ModelContextProtocol.Server;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class SaveMemoryMcpTool()
{
    [Description("Save data to memory for agent communication and state persistence")]
    public Task<string> SaveMemory(
        [Description("Key for the memory entry")] string key,
        [Description("Value to store")] string value,
        [Description("Optional namespace for organization (default: 'default')")] string? @namespace = null)
    {
        return Task.FromResult("Error: key cannot be empty");
    }
}
