namespace AISwarm.Infrastructure;

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
