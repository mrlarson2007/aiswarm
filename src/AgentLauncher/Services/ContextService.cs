using System.Reflection;

namespace AgentLauncher.Services;

/// <inheritdoc />
public class ContextService : IContextService
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

    /// <inheritdoc />
    public string GetAgentPrompt(string agentType)
    {
        var personaFiles = GetAllPersonaFiles();
        if (personaFiles.TryGetValue(agentType.ToLowerInvariant(), out var filePath) && File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }

        if (!AgentResources.TryGetValue(agentType.ToLowerInvariant(), out var resourceName))
            throw new ArgumentException($"Unknown agent type: {agentType}", nameof(agentType));

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <inheritdoc />
    public async Task<string> CreateContextFile(string agentType, string workingDirectory)
    {
        Directory.CreateDirectory(workingDirectory);
        var promptContent = GetAgentPrompt(agentType);
        var contextFileName = $"{agentType}_context.md";
        var contextFilePath = Path.Combine(workingDirectory, contextFileName);
        await File.WriteAllTextAsync(contextFilePath, promptContent);
        return contextFilePath;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailableAgentTypes()
    {
        var agentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in AgentResources.Keys) agentTypes.Add(key);
        foreach (var key in GetAllPersonaFiles().Keys) agentTypes.Add(key);
        return agentTypes.OrderBy(x => x);
    }

    /// <inheritdoc />
    public bool IsValidAgentType(string agentType)
    {
        if (AgentResources.ContainsKey(agentType.ToLowerInvariant())) return true;
        return GetAllPersonaFiles().ContainsKey(agentType.ToLowerInvariant());
    }

    /// <inheritdoc />
    public Dictionary<string, string> GetAgentTypeSources()
    {
        var sources = new Dictionary<string, string>();
        var personaFiles = GetAllPersonaFiles();
        foreach (var kvp in personaFiles) sources[kvp.Key] = $"External: {kvp.Value}";
        foreach (var kvp in AgentResources) if (!sources.ContainsKey(kvp.Key)) sources[kvp.Key] = "Embedded";
        return sources;
    }

    private static Dictionary<string, string> GetAllPersonaFiles()
    {
        var personaFiles = new Dictionary<string, string>();
        foreach (var directory in GetPersonaDirectories())
        {
            if (!Directory.Exists(directory)) continue;
            var files = Directory.GetFiles(directory, "*_prompt.md", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (!fileName.EndsWith("_prompt")) continue;
                var agentType = fileName[..^"_prompt".Length];
                if (!personaFiles.ContainsKey(agentType.ToLowerInvariant()))
                {
                    personaFiles[agentType.ToLowerInvariant()] = file;
                }
            }
        }
        return personaFiles;
    }

    private static List<string> GetPersonaDirectories()
    {
        var directories = new List<string>
        {
            Path.Combine(Environment.CurrentDirectory, DefaultPersonasDirectory)
        };
        var envPaths = Environment.GetEnvironmentVariable(PersonasEnvironmentVariable);
        if (!string.IsNullOrEmpty(envPaths))
        {
            var paths = envPaths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            directories.AddRange(paths);
        }
        return directories;
    }
}
