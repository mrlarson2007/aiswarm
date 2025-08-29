using System.Reflection;

namespace AISwarm.Infrastructure;

/// <inheritdoc />
public class ContextService : IContextService
{
    private static readonly Dictionary<string, string> AgentResources = new()
    {
        { "planner", "AISwarm.Infrastructure.Resources.planner_prompt.md" },
        { "implementer", "AISwarm.Infrastructure.Resources.implementer_prompt.md" },
        { "reviewer", "AISwarm.Infrastructure.Resources.reviewer_prompt.md" },
        { "tester", "AISwarm.Infrastructure.Resources.tester_prompt.md" }
    };

    private const string McpInstructionsResource = "AISwarm.Infrastructure.Resources.mcp_instructions.md";

    private const string DefaultPersonasDirectory = ".aiswarm|personas"; // '|' placeholder to be split
    private const string PersonasEnvironmentVariable = "AISWARM_PERSONAS_PATH";

    private string GetAgentPrompt(string agentType)
    {
        var personaFiles = GetAllPersonaFiles();
        if (personaFiles.TryGetValue(agentType.ToLowerInvariant(), out var filePath) && File.Exists(filePath))
        {
            return File.ReadAllText(filePath);
        }

        if (!AgentResources.TryGetValue(agentType.ToLowerInvariant(), out var resourceName))
            throw new ArgumentException($"Unknown agent type: {agentType}", nameof(agentType));

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException($"Resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private string GetMcpInstructions(string agentId)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(McpInstructionsResource) ??
            throw new InvalidOperationException($"Resource not found: {McpInstructionsResource}");
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        // Replace placeholders with actual agent ID
        return content.Replace("{0}", agentId);
    }

    /// <inheritdoc />
    public async Task<string> CreateContextFile(
        string agentType,
        string workingDirectory)
    {
        Directory.CreateDirectory(workingDirectory);
        var promptContent = GetAgentPrompt(agentType);
        var contextFileName = $"{agentType}_context.md";
        var contextFilePath = Path.Combine(workingDirectory, contextFileName);
        await File.WriteAllTextAsync(contextFilePath, promptContent);
        return contextFilePath;
    }

    /// <inheritdoc />
    public async Task<string> CreateContextFileWithAgentId(
        string agentType,
        string workingDirectory,
        string? agentId)
    {
        // Create the base context file first
        var contextFilePath = await CreateContextFile(agentType, workingDirectory);

        // If agentId is provided, append agent ID and MCP tool instructions
        if (!string.IsNullOrWhiteSpace(agentId))
        {
            var agentIdSection = $@"

## Your Agent ID

Your unique agent ID is: `{agentId}`
**You MUST use this ID for all MCP tool interactions.**

";
            var mcpInstructions = GetMcpInstructions(agentId);
            var fullInstructions = agentIdSection + mcpInstructions;

            await File.AppendAllTextAsync(contextFilePath, fullInstructions);
        }

        return contextFilePath;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailableAgentTypes()
    {
        var agentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in AgentResources.Keys)
            agentTypes.Add(key);
        foreach (var key in GetAllPersonaFiles().Keys)
            agentTypes.Add(key);
        return agentTypes.OrderBy(x => x);
    }

    /// <inheritdoc />
    public bool IsValidAgentType(string agentType)
    {
        if (AgentResources.ContainsKey(agentType.ToLowerInvariant()))
            return true;
        return GetAllPersonaFiles().ContainsKey(agentType.ToLowerInvariant());
    }

    /// <inheritdoc />
    public Dictionary<string, string> GetAgentTypeSources()
    {
        var sources = new Dictionary<string, string>();
        var personaFiles = GetAllPersonaFiles();
        foreach (var kvp in personaFiles)
            sources[kvp.Key] = $"External: {kvp.Value}";
        foreach (var kvp in AgentResources)
            if (!sources.ContainsKey(kvp.Key))
                sources[kvp.Key] = "Embedded";
        return sources;
    }

    private static Dictionary<string, string> GetAllPersonaFiles()
    {
        var personaFiles = new Dictionary<string, string>();
        foreach (var directory in GetPersonaDirectories())
        {
            if (!Directory.Exists(directory))
                continue;
            var files = Directory.GetFiles(directory, "*_prompt.md", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (!fileName.EndsWith("_prompt"))
                    continue;
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
            Path.Combine(new[]{Environment.CurrentDirectory}.Concat(DefaultPersonasDirectory.Split('|')).ToArray())
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
