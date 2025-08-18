namespace AgentLauncher.Services.Terminals;

public interface IInteractiveTerminalService
{
    bool LaunchTerminalInteractive(string command, string workingDirectory);
    (string shell, string args) BuildVersionCheck();
}
