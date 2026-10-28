using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AISwarm.Server.Delegation;

public static class DelegationMcpExtensions
{
    public static IMcpServerBuilder WithDelegationTasks(
        this IMcpServerBuilder builder)
    {
        var store = new TaskNotifications { DefaultTimeToLive = TimeSpan.FromDays(2) };
        return builder.WithTasks(store, options => options.ExecutionModeSelector = request =>
        {
            // This server has one stdio connection. The SDK owns all task state and execution.
            store.Server = request.Server;
            store.Logger = request.Services?.GetService<ILoggerFactory>()?.CreateLogger<TaskNotifications>();
            return request.Params?.Name == "delegate_task"
                ? McpTaskExecutionMode.Required
                : McpTaskExecutionMode.Synchronous;
        });
    }
}

// SDK 2.3 stores transitions but does not publish them. Delivery is bounded and never gates SDK work.
internal sealed class TaskNotifications : InMemoryMcpTaskStore, IMcpTaskStore
{
    public McpServer? Server
    {
        get; set;
    }
    public ILogger? Logger
    {
        get; set;
    }

    async Task IMcpTaskStore.SetCompletedAsync(
        string taskId,
        JsonElement result,
        CancellationToken cancellationToken)
    {
        await SetCompletedAsync(taskId, result, cancellationToken);
        _ = NotifyAsync(taskId);
    }

    async Task IMcpTaskStore.SetFailedAsync(
        string taskId,
        JsonElement error,
        CancellationToken cancellationToken)
    {
        await SetFailedAsync(taskId, error, cancellationToken);
        _ = NotifyAsync(taskId);
    }

    async Task<bool> IMcpTaskStore.SetCancelledAsync(
        string taskId,
        CancellationToken cancellationToken)
    {
        var cancelled = await SetCancelledAsync(taskId, cancellationToken);
        if (cancelled)
            _ = NotifyAsync(taskId);
        return cancelled;
    }

    async Task IMcpTaskStore.SetInputRequestsAsync(
        string taskId,
        IDictionary<string, InputRequest> inputRequests,
        CancellationToken cancellationToken)
    {
        await SetInputRequestsAsync(taskId, inputRequests, cancellationToken);
        _ = NotifyAsync(taskId);
    }

    async Task IMcpTaskStore.ResolveInputRequestsAsync(
        string taskId,
        IDictionary<string, InputResponse> inputResponses,
        CancellationToken cancellationToken)
    {
        await ResolveInputRequestsAsync(taskId, inputResponses, cancellationToken);
        _ = NotifyAsync(taskId);
    }

    private async Task NotifyAsync(string taskId)
    {
        if (Server is null)
            return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            var task = await GetTaskAsync(taskId, CancellationToken.None);
            if (task is null)
                return;
            TaskStatusNotificationParams notification = task.Status switch
            {
                McpTaskStatus.Completed => new CompletedTaskNotificationParams
                {
                    TaskId = task.TaskId,
                    CreatedAt = task.CreatedAt,
                    LastUpdatedAt = task.LastUpdatedAt,
                    Result = task.Result!.Value
                },
                McpTaskStatus.Failed => new FailedTaskNotificationParams
                {
                    TaskId = task.TaskId,
                    CreatedAt = task.CreatedAt,
                    LastUpdatedAt = task.LastUpdatedAt,
                    Error = task.Error!.Value
                },
                McpTaskStatus.Cancelled => new CancelledTaskNotificationParams
                {
                    TaskId = task.TaskId,
                    CreatedAt = task.CreatedAt,
                    LastUpdatedAt = task.LastUpdatedAt
                },
                McpTaskStatus.InputRequired => new InputRequiredTaskNotificationParams
                {
                    TaskId = task.TaskId,
                    CreatedAt = task.CreatedAt,
                    LastUpdatedAt = task.LastUpdatedAt,
                    InputRequests = task.InputRequests!.ToDictionary(entry => entry.Key, entry => entry.Value)
                },
                _ => new WorkingTaskNotificationParams
                {
                    TaskId = task.TaskId,
                    CreatedAt = task.CreatedAt,
                    LastUpdatedAt = task.LastUpdatedAt
                }
            };
            notification.TimeToLive = task.TimeToLive;
            notification.PollIntervalMs = task.PollIntervalMs;
            notification.StatusMessage = task.StatusMessage;
            await Server.SendTaskStatusNotificationAsync(notification, timeout.Token).WaitAsync(timeout.Token);
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Unable to deliver task status notification for {TaskId}", taskId);
        }
    }
}
