using AgentLauncher.Services;
using AgentLauncher.Services.Logging;
using System.Text;

namespace AgentLauncher.Commands;

/// <summary>
/// Handler responsible for displaying available agent types and their source origins.
/// </summary>
public class ListAgentsCommandHandler : ICommandHandler
{
    private readonly IContextService _contextService;
    private readonly IAppLogger _logger;
    private readonly IEnvironmentService _env;

    public ListAgentsCommandHandler(IContextService contextService, IAppLogger logger, IEnvironmentService env)
    {
        _contextService = contextService;
        _logger = logger;
        _env = env;
    }

    public void Run()
    {
        static string Describe(string key) => key switch
        {
            "planner" => "Plans and breaks down tasks",
            "implementer" => "Implements code and features using TDD",
            "reviewer" => "Reviews and tests code",
            "tester" => "Tests code and functionality",
            _ => "Custom agent type"
        };

        var sources = _contextService.GetAgentTypeSources();
        var envPaths = _env.GetEnvironmentVariable("AISWARM_PERSONAS_PATH");
        var output = new StringBuilder()
            .AppendAgentSources(sources, Describe)
            .AppendPersonaLocations(_env.CurrentDirectory, envPaths)
            .AppendPersonaHelp(_env.CurrentDirectory)
            .AppendWorkspaceHelp()
            .AppendModelHelp()
            .ToString();
        _logger.Info(output);
    }
}
