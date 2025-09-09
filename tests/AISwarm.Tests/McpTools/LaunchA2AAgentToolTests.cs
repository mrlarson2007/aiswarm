using AISwarm.Infrastructure;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Shouldly;
using Xunit;

namespace AISwarm.Tests.McpTools;

public class LaunchA2AAgentToolTests : ISystemUnderTest<LaunchA2AAgentTool>
{
    private readonly FakeProcessLauncher _fakeProcessLauncher;
    private readonly TestLogger _fakeLogger;
    private readonly FakeFileSystemService _fakeFileSystemService;
    private LaunchA2AAgentTool? _systemUnderTest;

    public LaunchA2AAgentToolTests()
    {
        _fakeProcessLauncher = new FakeProcessLauncher();
        _fakeLogger = new TestLogger();
        _fakeFileSystemService = new FakeFileSystemService();
    }

    public LaunchA2AAgentTool SystemUnderTest =>
        _systemUnderTest ??= new LaunchA2AAgentTool(
            _fakeProcessLauncher,
            _fakeLogger,
            _fakeFileSystemService);

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
            result.ErrorMessage.ShouldContain("Agent name is required.");
            _fakeProcessLauncher.LaunchedProcesses.ShouldBeEmpty();
        }

        [Fact]
        public async Task WhenExecutableNotFound_ShouldReturnFailureResult()
        {
            // Arrange
            var agentName = "test-agent";
            var description = "Test description";
            int? port = null;
            string? model = null;

            

            // Act
            var result = await SystemUnderTest.LaunchA2AAgentAsync(agentName, description, port, model);

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldContain("AISwarm.TestAgent.exe not found");
            _fakeProcessLauncher.LaunchedProcesses.ShouldBeEmpty();
        }
    }
}
