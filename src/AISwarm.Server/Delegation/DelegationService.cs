using AISwarm.Infrastructure.OpenCode;
using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace AISwarm.Server.Delegation;

public record DelegatedTask(
    string TaskId,
    string State,
    string Model,
    string WorkingDirectory,
    string OpenCodeVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt = null,
    int? ExitCode = null,
    string StandardOutput = "",
    string StandardError = "",
    string? HookError = null)
{
    public string ResultUri => $"aiswarm://tasks/{TaskId}";
}

public class DelegationService(
    IOpenCodeCli cli) : IHostedService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Execution> _tasks = new();
    private readonly object _gate = new();
    private bool _stopping;

    public async Task<DelegatedTask> DelegateAsync(
        string instructions,
        string model,
        string workingDirectory,
        Func<DelegatedTask, CancellationToken, Task>? hook = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (timeout.HasValue && (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromDays(1)))
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive and at most one day.");
        if (!Path.IsPathFullyQualified(workingDirectory) || !Directory.Exists(workingDirectory))
            throw new ArgumentException("Working directory must be an existing absolute path.", nameof(workingDirectory));

        using var discovery = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        discovery.CancelAfter(TimeSpan.FromSeconds(30));
        discovery.Token.ThrowIfCancellationRequested();
        var models = await cli.ListModelsAsync(workingDirectory, discovery.Token);
        if (!models.Models.Contains(model, StringComparer.Ordinal))
            throw new ArgumentException("Selected model is not available.", nameof(model));

        var snapshot = new DelegatedTask(
            Guid.NewGuid().ToString("N"), "Running", model, workingDirectory, models.Version, DateTimeOffset.UtcNow);
        lock (_gate)
        {
            discovery.Token.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_stopping, this);
            if (_tasks.Count >= 100)
                throw new InvalidOperationException("Task history is full (100 tasks). Retrieve results and restart the server.");
            if (_tasks.Values.Count(execution => !execution.Completion.IsCompleted) >= 4)
                throw new InvalidOperationException("At most four tasks may run concurrently.");
            var execution = new Execution(snapshot);
            _tasks[snapshot.TaskId] = execution;
            execution.Completion = Task.Run(async () =>
            {
                await NotifyAsync(execution, hook);
                using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(10));
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    execution.Cancellation.Token, deadline.Token);
                OpenCodeResult result;
                var state = "Failed";
                try
                {
                    result = await cli.RunAsync(instructions, model, workingDirectory, cancellation.Token);
                    var unsuccessfulOutput = HasUnsuccessfulOutput(result.StandardOutput);
                    if (result.ExitCode == 0 && unsuccessfulOutput)
                        result = result with
                        {
                            StandardError = result.StandardError +
                                "\nOpenCode emitted an error event or output was truncated; success cannot be confirmed."
                        };
                    state = result.ExitCode == 0 && !unsuccessfulOutput ? "Completed" : "Failed";
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    state = execution.Cancellation.IsCancellationRequested ? "Cancelled" : "Failed";
                    result = new OpenCodeResult(-1, "",
                        state == "Cancelled" ? "Task cancelled." : "Task timed out.");
                }
                catch (Exception ex)
                {
                    result = new OpenCodeResult(-1, "", ex.Message);
                }
                execution.Snapshot = execution.Snapshot with
                {
                    State = state,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ExitCode = result.ExitCode,
                    StandardOutput = result.StandardOutput,
                    StandardError = result.StandardError
                };
                await NotifyAsync(execution, hook);
            });
        }
        return snapshot;
    }

    public DelegatedTask GetTask(string taskId) => _tasks[taskId].Snapshot;

    public Task WaitForCompletionAsync(string taskId) => _tasks[taskId].Completion;

    public bool CancelTask(string taskId)
    {
        lock (_gate)
        {
            var execution = _tasks[taskId];
            if (execution.Snapshot.State != "Running" || execution.Cancellation.IsCancellationRequested)
                return false;
            execution.Cancellation.Cancel();
            return true;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Execution[] executions;
        lock (_gate)
        {
            _stopping = true;
            executions = _tasks.Values.ToArray();
            foreach (var execution in executions.Where(execution => !execution.Completion.IsCompleted))
                execution.Cancellation.Cancel();
        }
        await Task.WhenAll(executions.Select(execution => execution.Completion));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        foreach (var execution in _tasks.Values)
            execution.Cancellation.Dispose();
    }

    private static async Task NotifyAsync(
        Execution execution,
        Func<DelegatedTask, CancellationToken, Task>? hook)
    {
        if (hook == null)
            return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await hook(execution.Snapshot, timeout.Token).WaitAsync(timeout.Token);
        }

        catch (Exception ex)
        {
            execution.Snapshot = execution.Snapshot with
            {
                HookError = ex.Message
            };
        }
    }

    private static bool HasUnsuccessfulOutput(string output)
    {
        if (output.EndsWith("\n[output truncated]", StringComparison.Ordinal))
            return true;
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("type", out var type)
                    && type.ValueKind == JsonValueKind.String && type.GetString() == "error")
                    return true;
            }
            catch (JsonException)
            {
                // OpenCode diagnostics can appear alongside JSON events.
            }
        }
        return false;
    }

    private sealed class Execution(DelegatedTask snapshot)
    {
        public volatile DelegatedTask Snapshot = snapshot;
        public Task Completion = Task.CompletedTask;
        public CancellationTokenSource Cancellation = new();
    }
}
