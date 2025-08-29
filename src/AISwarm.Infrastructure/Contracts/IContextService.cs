namespace AISwarm.Infrastructure;

/// <summary>
///     Service responsible for discovering persona prompts and creating per-run context files.
///     Handles precedence of local, environment-provided, and embedded persona resources.
/// </summary>
public interface IContextService
{
    /// <summary>
    ///     Create a context markdown file for the given agent type inside the specified directory.
    ///     File name convention: <c>{agentType}_context.md</c>.
    /// </summary>
    /// <param name="agentType">Agent type to generate context for.</param>
    /// <param name="workingDirectory">Target directory (created if missing).</param>
    /// <returns>Full path to the created context file.</returns>
    Task<string> CreateContextFile(string agentType, string workingDirectory);

    /// <summary>
    ///     Create a context markdown file for the given agent type inside the specified directory,
    ///     optionally appending MCP tool instructions if an agent ID is provided.
    ///     File name convention: <c>{agentType}_context.md</c>.
    /// </summary>
    /// <param name="agentType">Agent type to generate context for.</param>
    /// <param name="workingDirectory">Target directory (created if missing).</param>
    /// <param name="agentId">Optional agent ID to append MCP tool instructions. If null, behaves like CreateContextFile.</param>
    /// <returns>Full path to the created context file.</returns>
    Task<string> CreateContextFileWithAgentId(string agentType, string workingDirectory, string? agentId);

    /// <summary>
    ///     Enumerate all available agent types combining embedded and external persona files.
    /// </summary>
    IEnumerable<string> GetAvailableAgentTypes();

    /// <summary>
    ///     Determine if the supplied agent type is recognized (external or embedded).
    /// </summary>
    /// <param name="agentType">Agent type identifier.</param>
    /// <returns><c>true</c> if valid; otherwise <c>false</c>.</returns>
    bool IsValidAgentType(string agentType);

    /// <summary>
    ///     Get mapping of agent type to its source origin description (External path or Embedded).
    /// </summary>
    /// <returns>Dictionary keyed by agent type.</returns>
    Dictionary<string, string> GetAgentTypeSources();
}
