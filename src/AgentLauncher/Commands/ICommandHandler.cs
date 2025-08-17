namespace AgentLauncher.Commands;

/// <summary>
/// Represents a command handler that can execute a CLI command's logic.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Executes the command's main logic.
    /// </summary>
    void Run();
}
