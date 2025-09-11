using System.Text;
using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Models.A2A;
using AISwarm.Tests.TestDoubles;
using Shouldly;

namespace AISwarm.Tests.Services;

/// <summary>
/// Tests for the A2AService
/// </summary>
public class A2AServiceTest : ISystemUnderTest<IA2AService>
{

    public IA2AService SystemUnderTest => new A2AService(FakeProcessLauncher, FakeFileSystem, TestLogger, "dummy/path/to/agent.exe");

    protected FakeProcessLauncher FakeProcessLauncher = new();
    protected FakeFileSystemService FakeFileSystem = new();
    protected TestLogger TestLogger = new();

    public class ParameterValidation : A2AServiceTest
    {

        [Fact]
        public async Task WhenConfigurationIsNull_ShouldThrowError()
        {
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await SystemUnderTest.LaunchAgentAsync(null!)  // Use null! to bypass nullable warning
            );
            exception.ParamName.ShouldNotBeNull();
            exception.ParamName.ShouldContain("config");
        }

        [Fact]
        public async Task WhenAgentNameIsNotSpecified_ShouldThrowError()
        {
            var configuration = new A2AAgentConfig
            {
                AgentName = string.Empty,
                Description = null,
                Model = null,
                Port = null,
                Persona = "reviewer",
                PersonaDescription = "test",

            };

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await SystemUnderTest.LaunchAgentAsync(configuration)
            );
            exception.ParamName.ShouldNotBeNull();
            exception.ParamName.ShouldContain(nameof(configuration.AgentName));
        }

        [Fact]
        public async Task WhenAgentPersonaIsNotSpecified_ShouldThrowError()
        {
            var configuration = new A2AAgentConfig
            {
                AgentName = "agent1",
                Description = null,
                Model = null,
                Port = null,
                Persona = string.Empty,
                PersonaDescription = "test"
            };

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await SystemUnderTest.LaunchAgentAsync(configuration)
            );
            exception.ParamName.ShouldNotBeNull();
            exception.ParamName.ShouldContain(nameof(configuration.Persona));
        }

        [Fact]
        public async Task WhenAgentPersonaDescriptionIsNotSpecified_ShouldThrowError()
        {
            var configuration = new A2AAgentConfig
            {
                AgentName = "agent1",
                Description = null,
                Model = null,
                Port = null,
                Persona = "test",
                PersonaDescription = string.Empty
            };

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await SystemUnderTest.LaunchAgentAsync(configuration)
            );
            exception.ParamName.ShouldNotBeNull();
            exception.ParamName.ShouldContain(nameof(configuration.PersonaDescription));
        }

        [Fact]
        public async Task WhenWorkingDirectoryIsNotSpecified_ShouldThrowError()
        {
            var configuration = new A2AAgentConfig
            {
                AgentName = "agent1",
                Description = null,
                Model = null,
                Port = null,
                Persona = "implementer",
                PersonaDescription = "test",
                WorkingDirectory = string.Empty
            };

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await SystemUnderTest.LaunchAgentAsync(configuration)
            );
            exception.ParamName.ShouldNotBeNull();
            exception.ParamName.ShouldContain(nameof(configuration.WorkingDirectory));
        }
    }

    public class RunAgentTests : A2AServiceTest
    {
        [Fact]
        public async Task WhenConfigurationIsComplete_AllOptionsShouldBePassedToAgent()
        {
            // Arrange
            var configuration = new A2AAgentConfig
            {
                AgentName = "test-agent-123",
                Description = "Test agent for validation",
                Model = "gemini-2.0-flash-exp",
                Port = 3100,
                Persona = "implementer",
                PersonaDescription = "Expert software implementer with deep technical knowledge",
                Skills = new List<string>{"java", "javascript"},
                Capabilities = new List<string> {"coding", "testing"},
                WorkingDirectory = "/repo/test/path"
            };

            // Act
            var result = await SystemUnderTest.LaunchAgentAsync(configuration);

            // Assert - Verify the process was launched with correct arguments
            FakeProcessLauncher.LaunchedProcesses.Count.ShouldBe(1);
            var launchedProcess = FakeProcessLauncher.LaunchedProcesses.First();
            launchedProcess.StartInfo.WorkingDirectory.ShouldBe(configuration.WorkingDirectory);

            // Verify all configuration values are passed as command line arguments
            // Access arguments through StartInfo property
            var arguments = launchedProcess.StartInfo.Arguments;
            arguments.ShouldContain("--agent-name");
            arguments.ShouldContain(configuration.AgentName);
            arguments.ShouldContain("--description");
            arguments.ShouldContain(configuration.Description);
            arguments.ShouldContain("--model");
            arguments.ShouldContain(configuration.Model);
            arguments.ShouldContain("--port");
            arguments.ShouldContain(configuration.Port.GetValueOrDefault().ToString());
            arguments.ShouldContain("--persona");
            arguments.ShouldContain(configuration.Persona);
            arguments.ShouldContain("--persona-description");
            arguments.ShouldContain(configuration.PersonaDescription);
            arguments.ShouldContain("--skills");
            arguments.ShouldContain(string.Join(',',configuration.Skills));
            arguments.ShouldContain("--capabilities");
            arguments.ShouldContain(string.Join(',',configuration.Capabilities));

            // Verify the result contains expected agent information
            result.ShouldNotBeNull();
            result.AgentName.ShouldBe(configuration.AgentName);
            result.Status.ShouldBe(A2AAgentStatus.Starting);
            result.AgentUrl.ShouldStartWith("http://localhost:");
        }

        [Fact]
        public async Task WhenConfigurationIsCompleteAndPersonaDescriptionIsLarge_ShouldBePassedToAgentViaConfigFile()
        {
            // Arrange
            var basePersonaDescription = "Expert software implementer with deep technical knowledge";
            var targetLength = A2AService.MaxCommandLineLength + 1;
            var longPersonaDescriptionBuilder = new StringBuilder(basePersonaDescription);

            // Fill the persona description to exceed the max command line length
            while (longPersonaDescriptionBuilder.Length < targetLength)
            {
                longPersonaDescriptionBuilder.Append("a"); // Append a simple character
            }

            var configuration = new A2AAgentConfig
            {
                AgentName = "test-agent-123",
                Description = "Test agent for validation",
                Model = "gemini-2.0-flash-exp",
                Port = 3100,
                Persona = "implementer",
                PersonaDescription = longPersonaDescriptionBuilder.ToString(),
                Skills = new List<string>{"java", "javascript"},
                Capabilities = new List<string> {"coding", "testing"},
                WorkingDirectory = "/repo/test/path"
            };
            var expectedConfig = System.Text.Json.JsonSerializer.Serialize(configuration);

            // Act
            var result = await SystemUnderTest.LaunchAgentAsync(configuration);

            // Assert - Verify the process was launched with correct arguments
            FakeProcessLauncher.LaunchedProcesses.Count.ShouldBe(1);
            var launchedProcess = FakeProcessLauncher.LaunchedProcesses.First();
            launchedProcess.StartInfo.WorkingDirectory.ShouldBe(configuration.WorkingDirectory);

            var arguments = launchedProcess.StartInfo.Arguments;
            arguments.ShouldContain("--config-file");
            arguments.ShouldContain(configuration.AgentName + ".json");

            FakeFileSystem.FileExists(Path.Combine(configuration.WorkingDirectory, configuration.AgentName + ".json")).ShouldBeTrue();
            var configFile = FakeFileSystem.GetFileContent(Path.Combine(configuration.WorkingDirectory, configuration.AgentName + ".json"));
            configFile.ShouldNotBeNull();
            configFile.ShouldBe(expectedConfig);

            // Verify the result contains expected agent information
            result.ShouldNotBeNull();
            result.AgentName.ShouldBe(configuration.AgentName);
            result.Status.ShouldBe(A2AAgentStatus.Starting);
            result.AgentUrl.ShouldStartWith("http://localhost:");
        }
    }
}
