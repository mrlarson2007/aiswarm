using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Models.A2A;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Shouldly;
using Xunit;

namespace AISwarm.Tests.McpTools;

public class LaunchA2AAgentToolTests : ISystemUnderTest<LaunchA2AAgentTool>
{
    private readonly FakeA2AService _fakeA2AService;
    private readonly TestLogger _fakeLogger;
    private LaunchA2AAgentTool? _systemUnderTest;

    public LaunchA2AAgentToolTests()
    {
        _fakeA2AService = new FakeA2AService();
        _fakeLogger = new TestLogger();
    }

    public LaunchA2AAgentTool SystemUnderTest =>
        _systemUnderTest ??= new LaunchA2AAgentTool(
            _fakeA2AService,
            _fakeLogger);

    public class LaunchFailureTests : LaunchA2AAgentToolTests
    {
        [Fact]
        public async Task WhenAgentNameIsMissing_ShouldReturnFailureResult()
        {
            // Arrange
            string? agentName = null; // Test with null agentName
            var description = "Test description";
            int? port = null;
            string? model = null;

            // Act
            var result = await SystemUnderTest.LaunchA2AAgentAsync(agentName, description, port, model);

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("Agent name is required");
            _fakeA2AService.LaunchedConfigs.ShouldBeEmpty();
        }

        [Fact]
        public async Task WhenA2AServiceThrowsException_ShouldReturnFailureResult()
        {
            // Arrange
            var agentName = "test-agent";
            var description = "Test description";
            int? port = null;
            string? model = null;

            _fakeA2AService.ShouldThrowException = true;
            _fakeA2AService.ExceptionMessage = "Simulated launch failure";

            // Act
            var result = await SystemUnderTest.LaunchA2AAgentAsync(agentName, description, port, model);

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("Simulated launch failure");
        }
    }

    public class LaunchSuccessTests : LaunchA2AAgentToolTests
    {
        [Fact]
        public async Task WhenValidParametersProvided_ShouldReturnSuccessResult()
        {
            // Arrange
            var agentName = "test-agent";
            var description = "Test description";
            int? port = 3001;
            var model = "gemini-2.5-flash";

            // Act
            var result = await SystemUnderTest.LaunchA2AAgentAsync(agentName, description, port, model);

            // Assert
            result.Success.ShouldBeTrue();
            result.AgentName.ShouldBe(agentName);
            result.Port.ShouldBe(port);
            result.AgentUrl.ShouldBe($"http://localhost:{port}");
            result.ProcessId.ShouldBe(12345);
            result.Status.ShouldBe("Starting");

            // Verify A2AService was called with correct config
            _fakeA2AService.LaunchedConfigs.ShouldHaveSingleItem();
            var config = _fakeA2AService.LaunchedConfigs.First();
            config.AgentName.ShouldBe(agentName);
            config.Description.ShouldBe(description);
            config.Port.ShouldBe(port);
            config.Model.ShouldBe(model);
            config.Persona.ShouldBe("test-agent");
            config.PersonaDescription.ShouldBe("A test agent for A2A protocol testing");
        }

        [Fact]
        public async Task WhenPortNotProvided_ShouldUseAutoAssignedPort()
        {
            // Arrange
            var agentName = "test-agent";
            var description = "Test description";
            int? port = null; // No port specified
            var model = "gemini-2.5-flash";

            // Act
            var result = await SystemUnderTest.LaunchA2AAgentAsync(agentName, description, port, model);

            // Assert
            result.Success.ShouldBeTrue();
            result.AgentName.ShouldBe(agentName);
            result.Port.ShouldBe(3001); // Default port from fake service
            result.AgentUrl.ShouldBe("http://localhost:3001");

            // Verify A2AService was called with null port (for auto-assignment)
            var config = _fakeA2AService.LaunchedConfigs.First();
            config.Port.ShouldBeNull();
        }
    }
}
