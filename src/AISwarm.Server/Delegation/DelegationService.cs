using AISwarm.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace AISwarm.Server.Delegation;

public class DelegationService(
    IAgentService agent,
    IHostApplicationLifetime lifetime) : IHostedService
{
    private readonly SemaphoreSlim _processes = new(4, 4);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var acquired = 0;
        try
        {
            for (; acquired < 4; acquired++)
                await _processes.WaitAsync(cancellationToken);
        }
        finally
        {
            if (acquired > 0)
                _processes.Release(acquired);
        }
    }

    public async Task<AgentResult> ExecuteAsync(
        string instructions,
        string model,
        string workingDirectory,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instructions);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (timeout.HasValue && (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromDays(1)))
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive and at most one day.");
        if (!Path.IsPathFullyQualified(workingDirectory) || !Directory.Exists(workingDirectory))
            throw new ArgumentException("Working directory must be an existing absolute path.", nameof(workingDirectory));

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, lifetime.ApplicationStopping);
        cancellation.Token.ThrowIfCancellationRequested();
        if (!await _processes.WaitAsync(0, cancellation.Token))
            return new AgentResult(-1, "", "At most four tasks may run concurrently.");
        try
        {
            using var discovery = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            discovery.CancelAfter(TimeSpan.FromSeconds(30));
            var models = await agent.ListModelsAsync(workingDirectory, discovery.Token);
            if (!models.Models.Contains(model, StringComparer.Ordinal))
                throw new ArgumentException("Selected model is not available.", nameof(model));

            cancellation.CancelAfter(timeout ?? TimeSpan.FromMinutes(10));
            try
            {
                return await agent.RunAsync(instructions, model, workingDirectory, cancellation.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                && !lifetime.ApplicationStopping.IsCancellationRequested && cancellation.IsCancellationRequested)
            {
                return new AgentResult(-1, "", "Task timed out.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new AgentResult(-1, "", ex.Message);
            }
        }
        finally
        {
            _processes.Release();
        }
    }
}
