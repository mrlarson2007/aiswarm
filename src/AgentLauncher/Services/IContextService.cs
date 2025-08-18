namespace AgentLauncher.Services;

/// <summary>
/// Service responsible for discovering persona prompts and creating per-run context files.
/// Handles precedence of local, environment-provided, and embedded persona resources.
/// </summary>
public interface IContextService
{
    /// <summary>
    /// Retrieve the raw prompt text for an agent type from external or embedded sources.
    /// </summary>
    /// <param name="agentType">Logical agent (persona) type identifier.</param>
    /// <returns>Prompt content.</returns>
    string GetAgentPrompt(string agentType);

    /// <summary>
    /// Create a context markdown file for the given agent type inside the specified directory.
    /// File name convention: <c>{agentType}_context.md</c>.
    /// </summary>
    /// <param name="agentType">Agent type to generate context for.</param>
    /// <param name="workingDirectory">Target directory (created if missing).</param>
    /// <returns>Full path to the created context file.</returns>
    Task<string> CreateContextFile(string agentType, string workingDirectory);

    /// <summary>
    /// Enumerate all available agent types combining embedded and external persona files.
    /// </summary>
    IEnumerable<string> GetAvailableAgentTypes();

    /// <summary>
    /// Determine if the supplied agent type is recognized (external or embedded).
    /// </summary>
    /// <param name="agentType">Agent type identifier.</param>
    /// <returns><c>true</c> if valid; otherwise <c>false</c>.</returns>
    bool IsValidAgentType(string agentType);

    /// <summary>
    /// Get mapping of agent type to its source origin description (External path or Embedded).
    /// </summary>
    /// <returns>Dictionary keyed by agent type.</returns>
    Dictionary<string, string> GetAgentTypeSources();
}
