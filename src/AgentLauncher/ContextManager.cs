using System.Reflection;

namespace AgentLauncher;

public static class ContextManager
{
    private static readonly Dictionary<string, string> AgentResources = new()
    {
        { "planner", "AgentLauncher.Resources.planner_prompt.md" },
        { "implementer", "AgentLauncher.Resources.implementer_prompt.md" },
        { "reviewer", "AgentLauncher.Resources.reviewer_prompt.md" },
        { "tester", "AgentLauncher.Resources.tester_prompt.md" }
    };

    private const string DefaultPersonasDirectory = ".aiswarm/personas";
    private const string PersonasEnvironmentVariable = "AISWARM_PERSONAS_PATH";

    /// <summary>
    /// Get all available persona files from various sources
    /// </summary>
    /// <returns>Dictionary of agent type to file path</returns>
    private static Dictionary<string, string> GetAllPersonaFiles()
    {
        var personaFiles = new Dictionary<string, string>();

        // Get persona directories to search
        var personaDirectories = GetPersonaDirectories();

        foreach (var directory in personaDirectories)
        {
            if (Directory.Exists(directory))
            {
                var files = Directory.GetFiles(directory, "*_prompt.md", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.EndsWith("_prompt"))
                    {
                        var agentType = fileName[..^"_prompt".Length];
                        // Use the first file found for each agent type (priority order)
                        if (!personaFiles.ContainsKey(agentType.ToLowerInvariant()))
                        {
                            personaFiles[agentType.ToLowerInvariant()] = file;
                        }
                    }
                }
            }
        }

        return personaFiles;
    }

    /// <summary>
    /// Get persona directories in priority order
    /// </summary>
    /// <returns>List of directories to search for persona files</returns>
    private static List<string> GetPersonaDirectories()
    {
        var directories = new List<string>();

        // 1. Current working directory .aiswarm/personas (highest priority)
        var localPersonasDir = Path.Combine(Environment.CurrentDirectory, DefaultPersonasDirectory);
        directories.Add(localPersonasDir);

        // 2. Environment variable paths
        var envPaths = Environment.GetEnvironmentVariable(PersonasEnvironmentVariable);
        if (!string.IsNullOrEmpty(envPaths))
        {
            var paths = envPaths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            directories.AddRange(paths);
        }

        return directories;
    }

    /// <summary>
    /// Get the prompt content for an agent type from embedded resources or external files
    /// </summary>
    /// <param name="agentType">The agent type</param>
    /// <returns>The content of the prompt</returns>
    public static string GetAgentPrompt(string agentType)
    {
        // First, check for external persona files
        var personaFiles = GetAllPersonaFiles();
        if (personaFiles.TryGetValue(agentType.ToLowerInvariant(), out var filePath))
        {
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
        }

        // Fall back to embedded resources
        if (!AgentResources.TryGetValue(agentType.ToLowerInvariant(), out var resourceName))
        {
            throw new ArgumentException($"Unknown agent type: {agentType}", nameof(agentType));
        }

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Resource not found: {resourceName}");
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
    /// Get all available agent types from embedded resources and external files
    /// </summary>
    /// <returns>List of available agent types</returns>
    public static IEnumerable<string> GetAvailableAgentTypes()
    {
        var agentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add embedded resource agent types
        foreach (var key in AgentResources.Keys)
        {
            agentTypes.Add(key);
        }

        // Add external persona file agent types
        var personaFiles = GetAllPersonaFiles();
        foreach (var key in personaFiles.Keys)
        {
            agentTypes.Add(key);
        }

        return agentTypes.OrderBy(x => x);
    }

    /// <summary>
    /// Check if an agent type is valid (either embedded or external)
    /// </summary>
    /// <param name="agentType">The agent type to check</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidAgentType(string agentType)
    {
        // Check embedded resources
        if (AgentResources.ContainsKey(agentType.ToLowerInvariant()))
        {
            return true;
        }

        // Check external persona files
        var personaFiles = GetAllPersonaFiles();
        return personaFiles.ContainsKey(agentType.ToLowerInvariant());
    }

    /// <summary>
    /// Get information about where persona files are being loaded from
    /// </summary>
    /// <returns>Dictionary of agent type to source location</returns>
    public static Dictionary<string, string> GetAgentTypeSources()
    {
        var sources = new Dictionary<string, string>();

        // Get external persona files first (higher priority)
        var personaFiles = GetAllPersonaFiles();
        foreach (var kvp in personaFiles)
        {
            sources[kvp.Key] = $"External: {kvp.Value}";
        }

        // Add embedded resources for any types not found externally
        foreach (var kvp in AgentResources)
        {
            if (!sources.ContainsKey(kvp.Key))
            {
                sources[kvp.Key] = "Embedded";
            }
        }

        return sources;
    }
}
