using System.ComponentModel;
using AISwarm.DataLayer;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class GetTaskMcpTool(IDatabaseScopeService scopeService)
{
    [McpServerTool(Name = "get_tasks_by_status")]
    [Description("Get tasks by status")]
    public async Task<GetTasksByStatusResult> GetTasksByStatusAsync(
        [Description("Status of tasks to query (Pending, InProgress, Completed, Failed)")] string status)
    {
        if (!Enum.TryParse<DataLayer.Entities.TaskStatus>(
                status, ignoreCase: true, out var taskStatus))
        {
            return GetTasksByStatusResult.Failure(
                $"Invalid status: {status}. Valid values are: Pending, InProgress, Completed, Failed");
        }

        using var scope = scopeService.CreateReadScope();

        var tasks = await scope.Tasks
            .Where(t => t.Status == taskStatus)
            .Select(t => new TaskInfo
            {
                TaskId = t.Id,
                Status = t.Status.ToString(),
                AgentId = t.AgentId,
                StartedAt = t.StartedAt,
                CompletedAt = t.CompletedAt
            }).ToArrayAsync();

        return GetTasksByStatusResult.SuccessWith(tasks);
    }

    [McpServerTool(Name = "get_task_status")]
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

    [McpServerTool(Name = "get_tasks_by_agent_id")]
    [Description("Get tasks by agent ID")]
    public async Task<GetTasksByStatusResult> GetTasksByAgentIdAsync(
        [Description("ID of the agent to query tasks for")] string agentId)
    {
        using var scope = scopeService.CreateReadScope();
        var tasks = await scope.Tasks
            .Where(t => t.AgentId == agentId)
            .Select(t => new TaskInfo
            {
                TaskId = t.Id,
                Status = t.Status.ToString(),
                AgentId = t.AgentId,
                StartedAt = t.StartedAt,
                CompletedAt = t.CompletedAt
            }).ToArrayAsync();

        return GetTasksByStatusResult.SuccessWith(tasks);
    }

    [McpServerTool(Name = "get_tasks_by_agent_id_and_status")]
    [Description("Get tasks by agent ID and status")]
    public async Task<GetTasksByStatusResult> GetTasksByAgentIdAndStatusAsync(
        [Description("ID of the agent to query tasks for")] string agentId,
        [Description("Status of tasks to query (Pending, InProgress, Completed, Failed)")] string status)
    {
        if (!Enum.TryParse<DataLayer.Entities.TaskStatus>(status, ignoreCase: true, out var taskStatus))
        {
            return GetTasksByStatusResult.Failure($"Invalid status: {status}. Valid values are: Pending, InProgress, Completed, Failed");
        }

        using var scope = scopeService.CreateReadScope();
        var tasks = await scope.Tasks
            .Where(t => t.AgentId == agentId && t.Status == taskStatus)
            .ToListAsync();

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
