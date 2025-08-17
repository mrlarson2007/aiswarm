namespace AgentLauncher.Services;

/// <summary>
/// Service responsible for interacting with the Gemini CLI: availability checks,
/// version retrieval, and launching interactive sessions.
/// </summary>
public interface IGeminiService
{
    /// <summary>
    /// Check if the Gemini CLI executable/command is available.
    /// </summary>
    Task<bool> IsGeminiCliAvailableAsync();

    /// <summary>
    /// Get the installed Gemini CLI version string, or null if unavailable.
    /// </summary>
    Task<string?> GetGeminiVersionAsync();

    /// <summary>
    /// Launch an interactive Gemini CLI session using the given context file.
    /// </summary>
    /// <param name="contextFilePath">Path to context markdown file.</param>
    /// <param name="model">Optional model name override.</param>
    /// <param name="workingDirectory">Optional working directory.</param>
    /// <returns>True if launch initiated successfully.</returns>
    Task<bool> LaunchInteractiveAsync(string contextFilePath, string? model = null, string? workingDirectory = null);
}
