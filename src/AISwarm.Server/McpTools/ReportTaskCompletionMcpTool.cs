using System.ComponentModel;
using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Eventing;
using AISwarm.Server.Entities;
using ModelContextProtocol.Server;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class ReportTaskCompletionMcpTool(
    IDatabaseScopeService databaseScopeService,
    ITimeService timeService,
    IWorkItemNotificationService workItemNotifications)
{
    [McpServerTool(Name = "report_task_completion")]
    [Description("Reports completion of a task with results")]
    public async Task<ReportTaskCompletionResult> ReportTaskCompletionAsync(
        [Description("ID of the task to mark as completed")]
        string taskId,
        [Description("Result of the completed task")]
        string result)
    {
        using var scope = databaseScopeService.CreateWriteScope();

        var task = await scope.Tasks.FindAsync(taskId);
        if (task == null)
            return ReportTaskCompletionResult
                .Failure($"Task not found: {taskId}");

        if (task.Status == TaskStatus.Completed)
            return ReportTaskCompletionResult
                .Failure($"Task {taskId} is already completed");

        task.Status = TaskStatus.Completed;
        task.Result = result;
        task.CompletedAt = timeService.UtcNow;

        await scope.SaveChangesAsync();
        scope.Complete();

        await workItemNotifications.PublishTaskCompleted(taskId, task.AgentId);

        return ReportTaskCompletionResult.Success(taskId);
    }

    [McpServerTool(Name = "report_task_failure")]
    [Description("Reports failure of a task with error message")]
    public async Task<ReportTaskCompletionResult> ReportTaskFailureAsync(
        [Description("ID of the task to mark as failed")]
        string taskId,
        [Description("Error message for the failed task")]
        string errorMessage)
    {
        using var scope = databaseScopeService.CreateWriteScope();

        var task = await scope.Tasks.FindAsync(taskId);
        if (task == null)
            return ReportTaskCompletionResult
                .Failure($"Task not found: {taskId}");

        task.Status = TaskStatus.Failed;
        task.Result = errorMessage;
        task.CompletedAt = timeService.UtcNow;

        await scope.SaveChangesAsync();
        scope.Complete();

        await workItemNotifications.PublishTaskFailed(taskId, task.AgentId, errorMessage);

        return ReportTaskCompletionResult.Success(taskId);
    }
}
