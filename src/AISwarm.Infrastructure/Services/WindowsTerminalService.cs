namespace AISwarm.Infrastructure;

public class WindowsTerminalService(IProcessLauncher inner) : IInteractiveTerminalService
{
    public async Task<ProcessResult> RunAsync(
        string command,
        string workingDirectory,
        int? timeoutMs = null,
        bool captureOutput = true)
    {
        return await inner.RunAsync("pwsh.exe", command, workingDirectory, timeoutMs, captureOutput);
    }

    public bool LaunchTerminalInteractive(
        string command,
        string workingDirectory)
    {
        return inner.StartInteractive("pwsh.exe", $"-Command \"{command}\"", workingDirectory);
    }
}
