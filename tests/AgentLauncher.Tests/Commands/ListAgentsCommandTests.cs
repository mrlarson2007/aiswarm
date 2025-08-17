using AgentLauncher.Services;
using AgentLauncher.Services.Logging;
using Moq;
using System.Text;
using FluentAssertions;
using AgentLauncher.Tests.TestDoubles;

namespace AgentLauncher.Tests.Commands;

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
        handler.Run();

        // Assert via logger interactions
        _logger.Infos.Should().Contain(s => s.Contains("Available agent types"));
        _logger.Infos.Should().Contain(s => s.Contains("planner"));
        _logger.Infos.Should().Contain(s => s.Contains("custom"));
        _logger.Infos.Should().Contain(s => s.Contains("External:"));
        var sep = System.IO.Path.DirectorySeparatorChar;
        var expectedSegment = $"/repo{sep}.aiswarm{sep}personas";
        _logger.Infos.Should().Contain(s => s.Contains(expectedSegment));
    }

}
