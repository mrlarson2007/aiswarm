using System.ComponentModel;
using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;

namespace AISwarm.Server.McpTools;

public class GetTaskStatusMcpTool(IDatabaseScopeService scopeService)
{
    [Description("Get the status of a task by ID")]
    public async Task<GetTaskStatusResult> GetTaskStatusAsync(
        [Description("ID of the task to query")] string taskId)
    {
        using var scope = scopeService.CreateReadScope();
        var task = await scope.Tasks.FindAsync(taskId);

        if (task is null)
        {
            return GetTaskStatusResult.SuccessWith(
                taskId: null,
                status: null,
                agentId: null,
                startedAt: null,
                completedAt: null);
        }

        return GetTaskStatusResult.SuccessWith(
            taskId: task.Id,
            status: task.Status.ToString(),
            agentId: task.AgentId,
            startedAt: task.StartedAt,
            completedAt: task.CompletedAt);
    }
}
