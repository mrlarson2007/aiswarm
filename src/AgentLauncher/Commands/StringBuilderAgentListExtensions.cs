using System.Text;

namespace AgentLauncher.Commands;

internal static class StringBuilderAgentListExtensions
{
    public static StringBuilder AppendAgentSources(this StringBuilder sb,
        IDictionary<string, string> sources,
        Func<string, string> describe,
        int minPadding = 2)
    {
        sb.AppendLine("Available agent types:");
        if (sources.Count == 0)
        {
            sb.AppendLine("  (none discovered)").AppendLine();
            return sb;
        }
        var width = sources.Count > 0 ? sources.Keys.Max(k => k.Length) + minPadding : 0;
        foreach (var kvp in sources.OrderBy(k => k.Key))
        {
            sb.Append("  ")
              .Append(kvp.Key.PadRight(width))
              .Append("- ")
              .Append(describe(kvp.Key))
              .Append(" (")
              .Append(kvp.Value)
              .AppendLine(")");
        }
        sb.AppendLine();
        return sb;
    }

    public static StringBuilder AppendPersonaLocations(this StringBuilder sb, string workingDirectory, string? personasEnvVar)
    {
        sb.AppendLine("Persona file locations (in priority order):")
          .AppendLine($"  1. Local project: {Path.Combine(workingDirectory, ".aiswarm/personas")}");
        if (!string.IsNullOrWhiteSpace(personasEnvVar))
        {
            var paths = personasEnvVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < paths.Length; i++)
                sb.AppendLine($"  {i + 2}. Environment: {paths[i]}");
        }
        else
        {
            sb.AppendLine("  2. Environment variable AISWARM_PERSONAS_PATH not set");
        }
        sb.AppendLine("  3. Embedded: Built-in personas").AppendLine();
        return sb;
    }

    public static StringBuilder AppendPersonaHelp(this StringBuilder sb, string workingDirectory)
    {
        var personasDir = Path.Combine(workingDirectory, ".aiswarm/personas");
        sb.AppendLine("To add custom personas:")
          .AppendLine($"  - Create .md files with '_prompt' suffix in {personasDir}")
          .AppendLine("  - Or set AISWARM_PERSONAS_PATH environment variable to additional directories")
          .AppendLine("  - Example: custom_agent_prompt.md becomes 'custom_agent' type")
          .AppendLine();
        return sb;
    }

    public static StringBuilder AppendWorkspaceHelp(this StringBuilder sb)
    {
        sb.AppendLine("Workspace Options:")
          .AppendLine("  --worktree <name>   - Create a git worktree with specified name")
          .AppendLine("  (default)           - Work in current branch if no worktree specified")
          .AppendLine();
        return sb;
    }

    public static StringBuilder AppendModelHelp(this StringBuilder sb)
    {
        sb.AppendLine("Models:")
          .AppendLine("  Any Gemini model name can be used (e.g., gemini-1.5-flash, gemini-1.5-pro, gemini-2.0-flash-exp)")
          .AppendLine("  Default: Uses Gemini CLI default if --model not specified")
          .AppendLine("  Future: Dynamic model discovery from Gemini CLI");
        return sb;
    }
}
