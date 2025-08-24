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

    private const string DefaultPersonasDirectory = ".aiswarm|personas"; // '|' placeholder to be split
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
        using var stream = assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException($"Resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
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

        // If agentId is provided, append MCP tool instructions
        if (!string.IsNullOrWhiteSpace(agentId))
        {
            var mcpToolInstructionsPrompt = $@"

## Your Agent ID

Your unique agent ID is: `{agentId}`
**You MUST use this ID for all MCP tool interactions.**

## IMMEDIATE ACTION REQUIRED

**1. Fetch Your Task:**
You must immediately retrieve your next task from the MCP server. Use the following tool call:

`mcp_aiswarm_get_next_task(agentId='{agentId}')`

**2. Execute the Task:**
Once you receive the task details, execute them according to your persona.

**3. Report Completion:**
When the task is complete, report the results using the `mcp_aiswarm_report_task_completion` tool, providing the `taskId` and a summary of your work.

---

## Reference: Available Tools

You have access to the following MCP tools for task management:

### `mcp_aiswarm_get_next_task`

- **Purpose**: Fetches your next assigned task.
- **Parameters**:
  - `agentId`: Your agent ID.

### `mcp_aiswarm_create_task`

- **Purpose**: Creates new tasks for other agents.
- **Parameters**:
  - `agentId`: Target agent ID (or empty for unassigned).
  - `persona`: Full persona markdown for the new agent.
  - `description`: A clear description of the task.
  - `priority`: `Low`, `Normal`, `High`, `Critical`.

### `mcp_aiswarm_report_task_completion`

- **Purpose**: Reports that a task is finished.
- **Parameters**:
  - `taskId`: The ID of the completed task.
  - `result`: A summary of the results.
### Task Management Workflow
1. **Start Work Session**: Call `mcp_aiswarm_get_next_task` with your agentId ({agentId}) to check for pending tasks.
2. **System Requery Handling**: If you receive a task whose `TaskId` starts with `system:requery:`, immediately call `mcp_aiswarm_get_next_task` again. Do not produce other output before re-querying.
3. **Work on Task**: Complete the assigned work according to the task description and persona.
4. **Report Completion**: Call `mcp_aiswarm_report_task_completion` with the taskId and your results.
5. **Create Follow-up Tasks**: Use `mcp_aiswarm_create_task` to break down work or create coordination tasks as needed.

### Best Practices
- Always include your agent ID ({agentId}) when calling get_next_task.
- Provide detailed results when reporting task completion.
- Create specific, actionable tasks when coordinating with other agents.
- Use appropriate priority levels for time-sensitive work.";

            await File.AppendAllTextAsync(contextFilePath, mcpToolInstructionsPrompt);
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
