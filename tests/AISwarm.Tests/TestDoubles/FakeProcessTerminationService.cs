using AISwarm.Infrastructure;

namespace AISwarm.Tests.TestDoubles;

/// <summary>
/// Fake process termination service that simulates process termination without actually doing it.
/// Used for testing scenarios where we need to verify termination behavior without affecting real processes.
/// </summary>
public class FakeProcessTerminationService : IProcessTerminationService
{
    private readonly List<string> _terminatedProcessIds = new();

    /// <summary>
    /// Gets the list of process IDs that were "terminated" (but not actually killed).
    /// </summary>
    public IReadOnlyList<string> TerminatedProcessIds => _terminatedProcessIds.AsReadOnly();

    /// <summary>
    /// Simulates killing a process by recording the process ID.
    /// </summary>
    /// <param name="processId">The process ID to terminate</param>
    /// <returns>Always returns true to simulate successful termination</returns>
    public async Task<bool> KillProcessAsync(string processId)
    {
        // Simulate a small delay
        await Task.Delay(10);
        
        // Record the termination attempt
        _terminatedProcessIds.Add(processId);
        
        // Always return true to simulate successful termination
        return true;
    }

    /// <summary>
    /// Clears the list of terminated process IDs. Useful for test cleanup.
    /// </summary>
    public void Reset()
    {
        _terminatedProcessIds.Clear();
    }
}