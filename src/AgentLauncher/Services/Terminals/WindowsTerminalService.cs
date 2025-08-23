using AISwarm.Infrastructure;

namespace AgentLauncher.Services.Terminals;

public class WindowsTerminalService(IProcessLauncher inner) : IInteractiveTerminalService
{
    public async Task<ProcessResult> RunAsync(
        string command,
        string workingDirectory,
        int? timeoutMs = null,
        bool captureOutput = true) =>
            await inner.RunAsync("pwsh.exe", command, workingDirectory, timeoutMs, captureOutput);

    public bool LaunchTerminalInteractive(
        string command,
        string workingDirectory) =>
        inner.StartInteractive("pwsh.exe", command, workingDirectory);
}
