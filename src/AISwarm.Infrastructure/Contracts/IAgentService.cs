namespace AISwarm.Infrastructure;

public interface IAgentService
{
    Task<AgentModels> ListModelsAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);

    Task<AgentResult> RunAsync(
        string instructions,
        string model,
        string workingDirectory,
        CancellationToken cancellationToken = default);
}

public record AgentModels(string Version, IReadOnlyList<string> Models);

public record AgentResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool IsError
    {
        get; init;
    }
}
