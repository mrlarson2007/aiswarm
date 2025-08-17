using AgentLauncher.Services;
using AgentLauncher.Services.Logging;
using System.Text;

namespace AgentLauncher.Commands;

/// <summary>
/// Handler responsible for displaying available agent types and their source origins.
/// </summary>
public class ListAgentsCommandHandler(IContextService contextService, IAppLogger logger, IEnvironmentService env) : ICommandHandler
{

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

        var sources = contextService.GetAgentTypeSources();
        var envPaths = env.GetEnvironmentVariable("AISWARM_PERSONAS_PATH");
        var output = new StringBuilder()
            .AppendAgentSources(sources, Describe)
            .AppendPersonaLocations(env.CurrentDirectory, envPaths)
            .AppendPersonaHelp(env.CurrentDirectory)
            .AppendWorkspaceHelp()
            .AppendModelHelp()
            .ToString();
        logger.Info(output);
    }
}
