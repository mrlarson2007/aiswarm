using AgentLauncher.Services;
using AISwarm.Tests.TestDoubles;
using Moq;
using Shouldly;

namespace AISwarm.Tests.Commands;

public class ListAgentsCommandTests
{
    private readonly Mock<IContextService> _contextService = new();
    private readonly TestLogger _logger = new();

    [Fact]
    public void WhenMultipleAgentTypesAvailableAndSourcesProvided_ShouldOutputHeaderAndEachAgent()
    {
        // Arrange
        _contextService.Setup(s => s.GetAgentTypeSources()).Returns(new Dictionary<string, string>
        {
            {"planner", "Embedded"},
            {"custom", "External: /tmp/custom_prompt.md"}
        });
        _contextService.Setup(s => s.GetAvailableAgentTypes()).Returns(new[] { "custom", "planner" });

        var env = new TestEnvironmentService { CurrentDirectory = "/repo" };
        env.SetVar("AISWARM_PERSONAS_PATH", null);
        var handler = new AgentLauncher.Commands.ListAgentsCommandHandler(_contextService.Object, _logger, env);

        // Act
        var result = handler.Run();
        result.ShouldBeTrue();

        // Assert via logger interactions
        _logger.Infos.ShouldContain(i => i.Contains("Available agent types"));
        _logger.Infos.ShouldContain(i => i.Contains("planner"));
        _logger.Infos.ShouldContain(i => i.Contains("custom"));
        _logger.Infos.ShouldContain(i => i.Contains("External:"));
        var sep = System.IO.Path.DirectorySeparatorChar;
        var expectedSegment = $"/repo{sep}.aiswarm{sep}personas";
        _logger.Infos.ShouldContain(s => s.Contains(expectedSegment));
    }

}
