using System.ComponentModel;
using AISwarm.DataLayer;

namespace AISwarm.Server.McpTools;

public class GetTaskStatusMcpTool(IDatabaseScopeService scopeService)
{
    [Description("Get the status of a task by ID")]
    public Task<GetTaskStatusResult> GetTaskStatusAsync(
        [Description("ID of the task to query")] string taskId)
    {
        return Task.FromResult(GetTaskStatusResult.SuccessWith(
                taskId: null,
                status: null,
                agentId: null,
                startedAt: null,
                completedAt: null));
    }
}
