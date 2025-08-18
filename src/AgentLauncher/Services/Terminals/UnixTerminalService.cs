using AgentLauncher.Services.External;

namespace AgentLauncher.Services.Terminals;

public class UnixTerminalService(IProcessLauncher inner) : IInteractiveTerminalService
{
    public async Task<ProcessResult> RunAsync(
        string command,
        string workingDirectory,
        int? timeoutMs = null,
        bool captureOutput = true) =>
            await inner.RunAsync("bash", command, workingDirectory, timeoutMs, captureOutput);

    public bool LaunchTerminalInteractive(
        string command,
        string workingDirectory) =>
            inner.StartInteractive("bash", $"-c '{command} && exec $SHELL'", workingDirectory);

}
