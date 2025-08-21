namespace AgentLauncher.Services;

/// <summary>
/// Service for checking Gemini agent health and responsiveness
/// </summary>
public interface IGeminiHealthService
{
    /// <summary>
    /// Sends a ping to the Gemini process to verify it's responsive
    /// </summary>
    /// <param name="processId">The process ID of the Gemini agent</param>
    /// <param name="timeout">Maximum time to wait for ping response</param>
    /// <returns>True if the agent responded to ping within timeout, false otherwise</returns>
    Task<bool> PingAgentAsync(string processId, TimeSpan timeout = default);
}