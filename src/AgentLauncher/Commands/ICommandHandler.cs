namespace AgentLauncher.Commands;

/// <summary>
/// Represents a command handler that can execute a CLI command's logic.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Executes the command's main logic and returns success flag.
    /// </summary>
    Task<bool> RunAsync();
}
