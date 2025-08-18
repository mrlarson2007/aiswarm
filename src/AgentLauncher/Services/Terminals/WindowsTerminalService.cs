using AgentLauncher.Services.External;

namespace AgentLauncher.Services.Terminals;

public class WindowsTerminalService(IProcessLauncher process) : IInteractiveTerminalService
{
    private static string EscapeSingleQuotes(string input) => input.Replace("'", "''");
    public (string shell, string args) BuildVersionCheck() => ("pwsh.exe", "-Command \"gemini --version\"");
    public bool LaunchTerminalInteractive(string command, string workingDirectory)
    {
        var cmd = $"-NoExit -Command \"& Set-Location '{EscapeSingleQuotes(workingDirectory)}' ; {command}\"";
        return process.StartInteractive("pwsh.exe", cmd, workingDirectory);
    }
}
