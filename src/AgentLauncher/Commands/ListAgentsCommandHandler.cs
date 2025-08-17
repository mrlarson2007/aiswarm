using AgentLauncher.Services;
using AgentLauncher.Services.Logging;

namespace AgentLauncher.Commands;

/// <summary>
/// Handler responsible for displaying available agent types and their source origins.
/// </summary>
public class ListAgentsCommandHandler
{
    private readonly IContextService _contextService;
    private readonly IAppLogger _logger;

    public ListAgentsCommandHandler(IContextService contextService, IAppLogger logger)
    {
        _contextService = contextService;
        _logger = logger;
    }

    public void Run()
    {
        _logger.Info("Available agent types:\n");
        var sources = _contextService.GetAgentTypeSources();
        foreach (var kvp in sources.OrderBy(x => x.Key))
        {
            var description = kvp.Key switch
            {
                "planner" => "Plans and breaks down tasks",
                "implementer" => "Implements code and features using TDD",
                "reviewer" => "Reviews and tests code",
                "tester" => "Tests code and functionality",
                _ => "Custom agent type"
            };
            _logger.Info($"  {kvp.Key,-12} - {description} ({kvp.Value})");
        }

        _logger.Info(string.Empty);
        _logger.Info("Persona file locations (in priority order):");
        _logger.Info($"  1. Local project: {Path.Combine(Environment.CurrentDirectory, ".aiswarm/personas")}");

        var envPaths = Environment.GetEnvironmentVariable("AISWARM_PERSONAS_PATH");
        if (!string.IsNullOrEmpty(envPaths))
        {
            var paths = envPaths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < paths.Length; i++)
            {
                _logger.Info($"  {i + 2}. Environment: {paths[i]}");
            }
        }
        else
        {
            _logger.Info("  2. Environment variable AISWARM_PERSONAS_PATH not set");
        }
        _logger.Info("  3. Embedded: Built-in personas");
        _logger.Info(string.Empty);
        _logger.Info("To add custom personas:");
        _logger.Info($"  - Create .md files with '_prompt' suffix in {Path.Combine(Environment.CurrentDirectory, ".aiswarm/personas")}");
        _logger.Info("  - Or set AISWARM_PERSONAS_PATH environment variable to additional directories");
        _logger.Info("  - Example: custom_agent_prompt.md becomes 'custom_agent' type\n");
        _logger.Info("Workspace Options:");
        _logger.Info("  --worktree <name>   - Create a git worktree with specified name");
        _logger.Info("  (default)           - Work in current branch if no worktree specified\n");
        _logger.Info("Models:");
        _logger.Info("  Any Gemini model name can be used (e.g., gemini-1.5-flash, gemini-1.5-pro, gemini-2.0-flash-exp)");
        _logger.Info("  Default: Uses Gemini CLI default if --model not specified");
        _logger.Info("  Future: Dynamic model discovery from Gemini CLI");
    }
}
