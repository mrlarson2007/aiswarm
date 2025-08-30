using System.ComponentModel;
using AISwarm.Infrastructure;
using AISwarm.Server.Entities;
using ModelContextProtocol.Server;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class ListMemoryMcpTool(IMemoryService memoryService)
{
    [McpServerTool(Name = "list_memory")]
    [Description("Lists all memory entries in a specified namespace")]
    public async Task<ListMemoryResult> ListMemoryAsync(
        [Description("The namespace of the memory (defaults to empty string)")]
        string @namespace = "")
    {
        var entries = await memoryService.ListMemoryAsync(@namespace);
        return ListMemoryResult.SuccessResult(entries.ToList());
    }
}
