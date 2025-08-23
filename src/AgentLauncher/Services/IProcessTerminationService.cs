namespace AgentLauncher.Services;

/// <summary>
/// Service for terminating OS processes
/// </summary>
public interface IProcessTerminationService
{
    /// <summary>
    /// Kill a process by its process ID
    /// </summary>
    /// <param name="processId">The process ID to terminate</param>
    /// <returns>True if the process was successfully terminated</returns>
    Task<bool> KillProcessAsync(string processId);
}
