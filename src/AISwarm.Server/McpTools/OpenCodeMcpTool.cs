using System.ComponentModel;
using System.Text.Json;
using AISwarm.Infrastructure.OpenCode;
using AISwarm.Server.Delegation;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

#pragma warning disable MCP9005 // Desktop lifecycle diagnostics are not an automatic task-resumption protocol.

namespace AISwarm.Server.McpTools;

[McpServerToolType]
[McpServerResourceType]
public class OpenCodeMcpTool(
    IOpenCodeCli cli,
    DelegationService tasks,
    LocalLifecycleHook localHook)
{
    [McpServerTool(Name = "list_models", ReadOnly = true)]
    [Description("Discover OpenCode provider/model IDs in a working directory. Listing does not prove a model is local or reachable.")]
    public async Task<OpenCodeModels> ListModelsAsync(
        [Description("Existing absolute working directory used to resolve OpenCode configuration.")]
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!Path.IsPathFullyQualified(workingDirectory) || !Directory.Exists(workingDirectory))
            throw new ArgumentException("Working directory must be an existing absolute path.", nameof(workingDirectory));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        return await cli.ListModelsAsync(workingDirectory, timeout.Token);
    }

    [McpServerTool(Name = "delegate_task")]
    [Description("Run instructions in a background OpenCode CLI process using an explicit discovered provider/model ID. Returns immediately after validation. Lifecycle diagnostics are pushed, but Claude Desktop may not resume automatically; retrieve the result once requested, rather than polling.")]
    public Task<DelegatedTask> DelegateTaskAsync(
        McpServer server,
        [Description("Instructions for this task; no named personas.")]
        string instructions,
        [Description("Exact provider/model ID from list_models. No default or fallback model is selected by AISwarm.")]
        string model,
        [Description("Existing absolute working directory. Use a separate worktree for concurrent edits.")]
        string workingDirectory,
        [Description("Execution timeout in seconds, from 1 to 86400.")]
        int timeoutSeconds = 600,
        CancellationToken cancellationToken = default) =>
        tasks.DelegateAsync(
            instructions, model, workingDirectory,
            (snapshot, token) => NotifyAsync(server, snapshot, token),
            TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);

    [McpServerTool(Name = "get_task_result", ReadOnly = true)]
    [Description("Retrieve retained task state, raw OpenCode JSON event output, stderr, exit code and hook errors. Use on completion or explicit user request, not in a polling loop.")]
    public DelegatedTask GetTaskResult(string taskId) => tasks.GetTask(taskId);

    [McpServerTool(Name = "cancel_task", Destructive = true)]
    [Description("Request cancellation of a background task and terminate its process tree.")]
    public bool CancelTask(string taskId) => tasks.CancelTask(taskId);

    [McpServerResource(UriTemplate = "aiswarm://tasks/{taskId}", MimeType = "application/json")]
    [Description("Retained background task state and result, available for this server process lifetime.")]
    public string ReadTask(string taskId) =>
        JsonSerializer.Serialize(tasks.GetTask(taskId), JsonSerializerOptions.Web);

    private Task NotifyAsync(
        McpServer server,
        DelegatedTask snapshot,
        CancellationToken cancellationToken) =>
        localHook.NotifyAsync(snapshot, cancellationToken, SendHostAsync(server, snapshot, cancellationToken));

    private static async Task SendHostAsync(
        McpServer server,
        DelegatedTask snapshot,
        CancellationToken cancellationToken)
    {
        await server.SendNotificationAsync(NotificationMethods.LoggingMessageNotification,
                new LoggingMessageNotificationParams
                {
                    Level = snapshot.State == "Failed" ? LoggingLevel.Error : LoggingLevel.Info,
                    Logger = "aiswarm.tasks",
                    Data = JsonSerializer.SerializeToElement(new
                    {
                        snapshot.TaskId,
                        snapshot.State,
                        snapshot.ResultUri,
                        snapshot.ExitCode
                    }, JsonSerializerOptions.Web)
                }, cancellationToken: cancellationToken);
    }
}
