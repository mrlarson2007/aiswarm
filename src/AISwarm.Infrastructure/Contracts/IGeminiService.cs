namespace AISwarm.Infrastructure;

/// <summary>
///     Service responsible for interacting with the Gemini CLI: availability checks,
///     version retrieval, and launching interactive sessions.
/// </summary>
public interface IGeminiService
{
    /// <summary>
    ///     Check if the Gemini CLI executable/command is available.
    /// </summary>
    Task<bool> IsGeminiCliAvailableAsync();

    /// <summary>
    ///     Get the installed Gemini CLI version string, or null if unavailable.
    /// </summary>
    Task<string?> GetGeminiVersionAsync();

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

    /// <summary>
    ///     Launch an interactive Gemini CLI session with agent settings including MCP server configuration.
    /// </summary>
    /// <param name="contextFilePath">Path to context markdown file.</param>
    /// <param name="model">Optional model name override.</param>
    /// <param name="agentSettings">Agent configuration including ID and MCP server URL.</param>
    /// <param name="workingDirectory">Optional working directory.</param>
    /// <returns>True if launch initiated successfully.</returns>
    [Obsolete("Use LaunchInteractiveAsync with agentSettings parameter instead")]
    Task<bool> LaunchInteractiveWithSettingsAsync(string contextFilePath, string? model, AgentSettings agentSettings,
        string? workingDirectory = null);
}
