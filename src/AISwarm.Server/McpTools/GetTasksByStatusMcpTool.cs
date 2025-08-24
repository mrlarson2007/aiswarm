using System.ComponentModel;
using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.Server.McpTools;

public class GetTasksByStatusMcpTool(IDatabaseScopeService scopeService)
{
    [Description("Get tasks by status")]
    public async Task<GetTasksByStatusResult> GetTasksByStatusAsync(
        [Description("Status of tasks to query (Pending, InProgress, Completed, Failed)")] string status)
    {
        if (!Enum.TryParse<AISwarm.DataLayer.Entities.TaskStatus>(status, ignoreCase: true, out var taskStatus))
        {
            return GetTasksByStatusResult.Failure($"Invalid status: {status}. Valid values are: Pending, InProgress, Completed, Failed");
        }

        using var scope = scopeService.CreateReadScope();
        var tasks = await scope.Tasks
            .Where(t => t.Status == taskStatus)
            .ToArrayAsync();

        var taskInfos = tasks.Select(t => new TaskInfo
        {
            TaskId = t.Id,
            Status = t.Status.ToString(),
            AgentId = t.AgentId,
            StartedAt = t.StartedAt,
            CompletedAt = t.CompletedAt
        }).ToArray();

        return GetTasksByStatusResult.SuccessWith(taskInfos);
    }
}