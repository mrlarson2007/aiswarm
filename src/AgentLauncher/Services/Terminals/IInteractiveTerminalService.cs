using AISwarm.Infrastructure;

namespace AgentLauncher.Services.Terminals;

public interface IInteractiveTerminalService
{
    Task<ProcessResult> RunAsync(
        string command,
        string workingDirectory,
        int? timeoutMs = null,
        bool captureOutput = true);

    bool LaunchTerminalInteractive(
        string command,
        string workingDirectory);
}
