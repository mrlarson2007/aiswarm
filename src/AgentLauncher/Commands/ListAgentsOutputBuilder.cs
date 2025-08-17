using System.Text;
using AgentLauncher.Services;

namespace AgentLauncher.Commands;

internal static class ListAgentsOutputBuilder
{
    public static string Build(IDictionary<string, string> sources, string? personasEnvVar, string workingDirectory, Func<string, string> describer)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Available agent types:");
        foreach (var kvp in sources.OrderBy(k => k.Key))
        {
            sb.Append("  ")
              .Append(kvp.Key.PadRight(12))
              .Append(" - ")
              .Append(describer(kvp.Key))
              .Append(" (")
              .Append(kvp.Value)
              .AppendLine(")");
        }

        sb.AppendLine()
          .AppendLine("Persona file locations (in priority order):")
          .AppendLine($"  1. Local project: {Path.Combine(workingDirectory, ".aiswarm/personas")}");

        if (!string.IsNullOrEmpty(personasEnvVar))
        {
            var paths = personasEnvVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < paths.Length; i++)
            {
                sb.AppendLine($"  {i + 2}. Environment: {paths[i]}");
            }
        }
        else
        {
            sb.AppendLine("  2. Environment variable AISWARM_PERSONAS_PATH not set");
        }
        sb.AppendLine("  3. Embedded: Built-in personas");

        var personasDir = Path.Combine(workingDirectory, ".aiswarm/personas");
        sb.AppendLine()
          .AppendLine("To add custom personas:")
          .AppendLine($"  - Create .md files with '_prompt' suffix in {personasDir}")
          .AppendLine("  - Or set AISWARM_PERSONAS_PATH environment variable to additional directories")
          .AppendLine("  - Example: custom_agent_prompt.md becomes 'custom_agent' type")
          .AppendLine()
          .AppendLine("Workspace Options:")
          .AppendLine("  --worktree <name>   - Create a git worktree with specified name")
          .AppendLine("  (default)           - Work in current branch if no worktree specified")
          .AppendLine()
          .AppendLine("Models:")
          .AppendLine("  Any Gemini model name can be used (e.g., gemini-1.5-flash, gemini-1.5-pro, gemini-2.0-flash-exp)")
          .AppendLine("  Default: Uses Gemini CLI default if --model not specified")
          .Append("  Future: Dynamic model discovery from Gemini CLI");

        return sb.ToString();
    }
}
