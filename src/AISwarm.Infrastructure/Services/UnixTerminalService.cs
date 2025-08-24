namespace AISwarm.Infrastructure;

public class UnixTerminalService(IProcessLauncher inner) : IInteractiveTerminalService
{
    public async Task<ProcessResult> RunAsync(
        string command,
        string workingDirectory,
        int? timeoutMs = null,
        bool captureOutput = true)
    {
        return await inner.RunAsync("bash", $"-c '{EscapeShellArgument(command)}'", workingDirectory, timeoutMs,
            captureOutput);
    }

    public bool LaunchTerminalInteractive(
        string command,
        string workingDirectory)
    {
        return inner.StartInteractive("bash", $"-c '{EscapeShellArgument(command)} && exec $SHELL'", workingDirectory);
    }

    private static string EscapeShellArgument(string argument)
    {
        // Escape single quotes by replacing each ' with '\''
        return argument.Replace("'", "'\\''");
    }
}
