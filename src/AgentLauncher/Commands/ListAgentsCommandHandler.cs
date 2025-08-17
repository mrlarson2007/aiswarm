using AgentLauncher.Services;
using AgentLauncher.Services.Logging;

namespace AgentLauncher.Commands;

/// <summary>
/// Handler responsible for displaying available agent types and their source origins.
/// </summary>
public class ListAgentsCommandHandler : ICommandHandler
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
        static string Describe(string key) => key switch
        {
            "planner" => "Plans and breaks down tasks",
            "implementer" => "Implements code and features using TDD",
            "reviewer" => "Reviews and tests code",
            "tester" => "Tests code and functionality",
            _ => "Custom agent type"
        };

        var sources = _contextService.GetAgentTypeSources();
        var envPaths = Environment.GetEnvironmentVariable("AISWARM_PERSONAS_PATH");
        var output = ListAgentsOutputBuilder.Build(sources, envPaths, Environment.CurrentDirectory, Describe);
        _logger.Info(output);
    }
}
