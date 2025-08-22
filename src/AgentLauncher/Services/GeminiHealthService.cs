using System.Diagnostics;

namespace AgentLauncher.Services;

/// <summary>
/// Service for checking Gemini agent health via CLI interactions
/// </summary>
public class GeminiHealthService : IGeminiHealthService
{
    private readonly External.IProcessLauncher _processLauncher;

    public GeminiHealthService(External.IProcessLauncher processLauncher)
    {
        _processLauncher = processLauncher;
    }

    /// <summary>
    /// Sends a ping to the Gemini process by running a simple command
    /// </summary>
    /// <param name="processId">The process ID of the Gemini agent</param>
    /// <param name="timeout">Maximum time to wait for ping response (default 30 seconds)</param>
    /// <returns>True if the agent responded to ping within timeout, false otherwise</returns>
    public async Task<bool> PingAgentAsync(
        string processId, 
        TimeSpan timeout = default)
    {
        if (timeout == default)
            timeout = TimeSpan.FromSeconds(30);

        try
        {
            // Check if the process is still running
            if (!int.TryParse(processId, out var pid))
                return false;

            var process = Process.GetProcessById(pid);
            if (process.HasExited)
                return false;

            // TODO: Once we have MCP tools, we can send a more sophisticated ping
            // For now, we just verify the process exists and is running
            // In the future, this could send an actual MCP "ping" tool call
            
            return true;
        }
        catch (ArgumentException)
        {
            // Process doesn't exist
            return false;
        }
        catch (InvalidOperationException)
        {
            // Process has exited
            return false;
        }
        catch (Exception)
        {
            // Other errors (access denied, etc.)
            return false;
        }
    }
}