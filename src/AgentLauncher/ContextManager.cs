using System.Reflection;

namespace AgentLauncher;

public static class ContextManager
{
    private static readonly Dictionary<string, string> AgentResources = new()
    {
        { "planner", "AgentLauncher.Resources.planner_prompt.md" },
        { "implementer", "AgentLauncher.Resources.implementer_prompt.md" },
        { "reviewer", "AgentLauncher.Resources.reviewer_prompt.md" }
    };

    /// <summary>
    /// Get the embedded resource content for an agent type
    /// </summary>
    /// <param name="agentType">The agent type (planner, implementer, reviewer)</param>
    /// <returns>The content of the embedded resource</returns>
    public static string GetAgentPrompt(string agentType)
    {
        if (!AgentResources.TryGetValue(agentType.ToLowerInvariant(), out var resourceName))
        {
            throw new ArgumentException($"Unknown agent type: {agentType}", nameof(agentType));
        }

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        
        if (stream == null)
        {
            throw new InvalidOperationException($"Resource not found: {resourceName}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Create a context file in the specified directory
    /// </summary>
    /// <param name="agentType">The agent type</param>
    /// <param name="workingDirectory">The directory where to create the context file</param>
    /// <returns>The path to the created context file</returns>
    public static async Task<string> CreateContextFile(string agentType, string workingDirectory)
    {
        // Ensure the working directory exists
        Directory.CreateDirectory(workingDirectory);

        // Get the prompt content
        var promptContent = GetAgentPrompt(agentType);

        // Create the context file path
        var contextFileName = $"{agentType}_context.md";
        var contextFilePath = Path.Combine(workingDirectory, contextFileName);

        // Write the content to the file
        await File.WriteAllTextAsync(contextFilePath, promptContent);

        return contextFilePath;
    }

    /// <summary>
    /// Get all available agent types
    /// </summary>
    /// <returns>List of available agent types</returns>
    public static IEnumerable<string> GetAvailableAgentTypes()
    {
        return AgentResources.Keys;
    }

    /// <summary>
    /// Check if an agent type is valid
    /// </summary>
    /// <param name="agentType">The agent type to check</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidAgentType(string agentType)
    {
        return AgentResources.ContainsKey(agentType.ToLowerInvariant());
    }

    /// <summary>
    /// Check if an agent type needs its own worktree
    /// </summary>
    /// <param name="agentType">The agent type to check</param>
    /// <returns>True if the agent needs a worktree, false otherwise</returns>
    public static bool NeedsWorktree(string agentType)
    {
        return agentType.ToLowerInvariant() switch
        {
            "planner" => false,  // Planner stays on main/master
            "reviewer" => false, // Reviewer works in existing workspace
            "implementer" => true, // Implementer gets own worktree
            _ => true // Default to true for custom agent types
        };
    }
}
