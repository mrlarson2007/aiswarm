using AgentLauncher.Services.External;

namespace AgentLauncher.Services.Terminals;

public class MacTerminalService(IProcessLauncher process) : IInteractiveTerminalService
{
    public (string shell, string args) BuildVersionCheck() => ("/bin/sh", "-c 'gemini --version'");
    public bool LaunchTerminalInteractive(string command, string workingDirectory)
    {
        var escapedCmd = $"cd '{workingDirectory.Replace("'", "'\\''")}' ; {command}".Replace("\"", "\\\"");
        var appleScript = $"osascript -e \"tell application 'Terminal' to do script \"\"{escapedCmd}\"\"\"";
        if (process.StartInteractive("/bin/sh", $"-c \"{appleScript}\"", workingDirectory))
            return true;
        return process.StartInteractive("/bin/sh", $"-c 'cd {workingDirectory.Replace("'", "'\\''")} ; {command}'", workingDirectory);
    }
}
