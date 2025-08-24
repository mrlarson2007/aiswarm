using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using ModelContextProtocol.Server;
using System.ComponentModel;
using AISwarm.Server.Entities;

namespace AISwarm.Server.McpTools;

[McpServerToolType]
public class ReportTaskCompletionMcpTool(
    IDatabaseScopeService databaseScopeService,
    ITimeService timeService)
{
    [McpServerTool(Name = "report_task_completion")]
    [Description("Reports completion of a task with results")]
    public async Task<ReportTaskCompletionResult> ReportTaskCompletionAsync(
        [Description("ID of the task to mark as completed")] string taskId,
        [Description("Result of the completed task")] string result)
    {
        using var scope = databaseScopeService.CreateWriteScope();

        var task = await scope.Tasks.FindAsync(taskId);
        if (task == null)
        {
            return ReportTaskCompletionResult
                .Failure($"Task not found: {taskId}");
        }

        if (task.Status == DataLayer.Entities.TaskStatus.Completed)
        {
            return ReportTaskCompletionResult
                .Failure($"Task {taskId} is already completed");
        }

        task.Status = DataLayer.Entities.TaskStatus.Completed;
        task.Result = result;
        task.CompletedAt = timeService.UtcNow;

        await scope.SaveChangesAsync();
        scope.Complete();

        return ReportTaskCompletionResult.Success(taskId);
    }

    [McpServerTool(Name = "report_task_failure")]
    [Description("Reports failure of a task with error message")]
    public async Task<ReportTaskCompletionResult> ReportTaskFailureAsync(
        [Description("ID of the task to mark as failed")] string taskId,
        [Description("Error message for the failed task")] string errorMessage)
    {
        using var scope = databaseScopeService.CreateWriteScope();

        var task = await scope.Tasks.FindAsync(taskId);
        if (task == null)
        {
            return ReportTaskCompletionResult
                .Failure($"Task not found: {taskId}");
        }

        task.Status = DataLayer.Entities.TaskStatus.Failed;
        task.Result = errorMessage;
        task.CompletedAt = timeService.UtcNow;

        await scope.SaveChangesAsync();
        scope.Complete();

        return ReportTaskCompletionResult.Success(taskId);
    }
}
