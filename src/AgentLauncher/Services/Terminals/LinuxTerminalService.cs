using AgentLauncher.Services.External;

namespace AgentLauncher.Services.Terminals;

public class LinuxTerminalService(IProcessLauncher process) : IInteractiveTerminalService
{
    private static readonly string[] Candidates = ["x-terminal-emulator","gnome-terminal","konsole","xfce4-terminal","xterm"];
    public (string shell, string args) BuildVersionCheck() => ("/bin/sh", "-c 'gemini --version'");
    public bool LaunchGemini(string geminiArgs, string workingDirectory)
    {
        var escapedWd = workingDirectory.Replace("'", "'\\''");
        foreach (var term in Candidates)
        {
            if (process.StartInteractive(term, $"-e sh -c 'cd {escapedWd} ; gemini {geminiArgs}; exec sh'", workingDirectory))
                return true;
        }
        return process.StartInteractive("/bin/sh", $"-c 'cd {escapedWd} ; gemini {geminiArgs}'", workingDirectory);
    }
}
