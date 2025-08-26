namespace AISwarm.Infrastructure;

/// <summary>
///     Service responsible for interacting with the Gemini CLI: availability checks,
///     version retrieval, and launching interactive sessions.
/// </summary>
public interface IGeminiService
{
    /// <summary>
    ///     Launch an interactive Gemini CLI session using the given context file.
    /// </summary>
    /// <param name="contextFilePath">Path to context markdown file.</param>
    /// <param name="model">Optional model name override.</param>
    /// <param name="workingDirectory">Optional working directory.</param>
    /// <param name="agentSettings">Optional agent configuration including ID and MCP server URL.</param>
    /// <param name="yolo">Optional flag to bypass permission prompts (use --yolo flag).</param>
    /// <returns>True if launch initiated successfully.</returns>
    Task<bool> LaunchInteractiveAsync(
        string contextFilePath,
        string? model = null,
        string? workingDirectory = null,
        AgentSettings? agentSettings = null,
        bool yolo = false);
}
