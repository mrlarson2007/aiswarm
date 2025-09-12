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
    private readonly FakeContextService _fakeContextService;
    private readonly TestLogger _fakeLogger;
    private LaunchA2AAgentTool? _systemUnderTest;

    public LaunchA2AAgentToolTests()
    {
        _fakeA2AService = new FakeA2AService();
        _fakeContextService = new FakeContextService();
        _fakeLogger = new TestLogger();
    }

    public LaunchA2AAgentTool SystemUnderTest =>
        _systemUnderTest ??= new LaunchA2AAgentTool(
            _fakeA2AService,
            _fakeContextService,
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
            string? persona = null;

            // Act
            var result = await SystemUnderTest.LaunchA2AAgentAsync(agentName, description, port, model, persona);

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
            string? persona = null;

            _fakeA2AService.ShouldThrowException = true;
            _fakeA2AService.ExceptionMessage = "Simulated launch failure";

            // Act
            var result = await SystemUnderTest.LaunchA2AAgentAsync(agentName, description, port, model, persona);

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("Simulated launch failure");
        }

        [Fact]
        public async Task WhenInvalidPersonaProvided_ShouldReturnFailureResult()
        {
            // Arrange
            var agentName = "test-agent";
            var description = "Test description";
            int? port = null;
            string? model = null;
            var persona = "invalid-persona";

            // Act
            var result = await SystemUnderTest.LaunchA2AAgentAsync(agentName, description, port, model, persona);

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("Invalid persona 'invalid-persona'");
            result.ErrorMessage.ShouldContain("Available personas:");
            _fakeA2AService.LaunchedConfigs.ShouldBeEmpty();
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
            var persona = "implementer";

            // Act
            var result = await SystemUnderTest.LaunchA2AAgentAsync(agentName, description, port, model, persona);

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
            config.Persona.ShouldBe(persona);
            config.PersonaDescription.ShouldContain("Implementer Agent"); // Should contain actual persona prompt
            config.PersonaDescription.ShouldContain("TDD methodology");
        }

        [Fact]
        public async Task WhenPortNotProvided_ShouldUseAutoAssignedPort()
        {
            // Arrange
            var agentName = "test-agent";
            var description = "Test description";
            int? port = null; // No port specified
            var model = "gemini-2.5-flash";
            string? persona = null; // No persona specified - should default to 'implementer'

            // Act
            var result = await SystemUnderTest.LaunchA2AAgentAsync(agentName, description, port, model, persona);

            // Assert
            result.Success.ShouldBeTrue();
            result.AgentName.ShouldBe(agentName);
            result.Port.ShouldBe(3001); // Default port from fake service
            result.AgentUrl.ShouldBe("http://localhost:3001");

            // Verify A2AService was called with null port (for auto-assignment) and default persona
            var config = _fakeA2AService.LaunchedConfigs.First();
            config.Port.ShouldBeNull();
            config.Persona.ShouldBe("implementer"); // Should default to 'implementer'
            config.PersonaDescription.ShouldContain("Implementer Agent"); // Should contain actual persona prompt
        }
    }
}
