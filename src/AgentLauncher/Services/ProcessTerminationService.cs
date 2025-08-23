using System.Diagnostics;

namespace AgentLauncher.Services;

/// <summary>
/// Service for terminating OS processes
/// </summary>
public class ProcessTerminationService : IProcessTerminationService
{
    /// <summary>
    /// Attempts to kill a process by its process ID
    /// </summary>
    /// <param name="processId">The process ID to terminate</param>
    /// <returns>True if the process was successfully terminated, false otherwise</returns>
    public async Task<bool> KillProcessAsync(string processId)
    {
        if (string.IsNullOrWhiteSpace(processId) || !int.TryParse(processId, out var pid))
        {
            return false;
        }

        try
        {
            var process = Process.GetProcessById(pid);

            // First try graceful termination
            if (!process.HasExited)
            {
                process.CloseMainWindow();

                // Give the process a chance to close gracefully
                await Task.Delay(TimeSpan.FromSeconds(5));

                // If still running, force kill
                if (!process.HasExited)
                {
                    process.Kill();
                    await process.WaitForExitAsync();
                }
            }

            return true;
        }
        catch (ArgumentException)
        {
            // Process with the given ID doesn't exist
            return false;
        }
        catch (InvalidOperationException)
        {
            // Process has already exited
            return true;
        }
        catch (Exception)
        {
            // Other exceptions (access denied, etc.)
            return false;
        }
    }
}
