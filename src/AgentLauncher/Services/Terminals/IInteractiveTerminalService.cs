namespace AgentLauncher.Services.Terminals;

public interface IInteractiveTerminalService
{
    bool LaunchGemini(string geminiArgs, string workingDirectory);
    (string shell, string args) BuildVersionCheck();
}
