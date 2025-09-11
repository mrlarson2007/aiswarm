using System.Text;
using AISwarm.Infrastructure.Models.A2A;
using AISwarm.Infrastructure.Services;
using AISwarm.Tests.TestDoubles;
using Shouldly;

namespace AISwarm.Tests.Services;

/// <summary>
/// Test for the A2AService
/// </summary>
public class A2SServiceTest : ISystemUnderTest<IA2AService>
{

    public IA2AService SystemUnderTest => throw new NotImplementedException();

    protected FakeProcessLauncher fakeProcessLauncher = new();
    protected FakeFileSystemService fakeFileSystem = new();
    protected TestLogger testLogger = new();

    public class ParameterValidation : A2SServiceTest
    {

        //[Fact]
        public async Task WhenConfigurationIsNull_ShouldThrowError()
        {
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await SystemUnderTest.LaunchAgentAsync(null!)  // Use null! to bypass nullable warning
            );
            exception.ParamName.ShouldNotBeNull();
            exception.ParamName.ShouldContain("config");
        }

        //[Fact]
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

        //[Fact]
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

        //[Fact]
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
    }

    public class RunAgentTests : A2SServiceTest
    {
        //[Fact]
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
            fakeProcessLauncher.LaunchedProcesses.Count.ShouldBe(1);
            var launchedProcess = fakeProcessLauncher.LaunchedProcesses.First();
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
            arguments.ShouldContain("--skills");
            arguments.ShouldContain(string.Join(',',configuration.Skills));
            arguments.ShouldContain("--capabilities");
            arguments.ShouldContain(string.Join(',',configuration.Capabilities));
            arguments.ShouldContain("--capabilities");
            arguments.ShouldContain(string.Join(',',configuration.Capabilities));

            // Verify the result contains expected agent information
            result.ShouldNotBeNull();
            result.AgentName.ShouldBe(configuration.AgentName);
            result.ProcessId.ShouldBeGreaterThan(0);
            result.Status.ShouldBe(A2AAgentStatus.Starting);
            result.AgentUrl.ShouldStartWith("http://localhost:");
        }

        //[Fact]
        public async Task WhenConfigurationIsCompleteAndPersonaDescriptionIsLarge_ShouldBePassedToAgentViaConfigFile()
        {
            var longPersonaDescription = new StringBuilder("Expert software implementer with deep technical knowledge");
            for(int i = 0; i < 50; i++)
            {
                longPersonaDescription.AppendLine((i + 1).ToString());
            }

            var configuration = new A2AAgentConfig
            {
                AgentName = "test-agent-123",
                Description = "Test agent for validation",
                Model = "gemini-2.0-flash-exp",
                Port = 3100,
                Persona = "implementer",
                PersonaDescription = longPersonaDescription.ToString(),
                Skills = new List<string>{"java", "javascript"},
                Capabilities = new List<string> {"coding", "testing"},
                WorkingDirectory = "/repo/test/path"
            };
            var expectedConfig = System.Text.Json.JsonSerializer.Serialize(configuration);

            // Act
            var result = await SystemUnderTest.LaunchAgentAsync(configuration);

            // Assert - Verify the process was launched with correct arguments
            fakeProcessLauncher.LaunchedProcesses.Count.ShouldBe(1);
            var launchedProcess = fakeProcessLauncher.LaunchedProcesses.First();
            launchedProcess.StartInfo.WorkingDirectory.ShouldBe(configuration.WorkingDirectory);

            var arguments = launchedProcess.StartInfo.Arguments;
            arguments.ShouldContain("--config");
            arguments.ShouldContain(configuration.AgentName + ".json");

            fakeFileSystem.FileExists(configuration.AgentName + ".json");
            var configFile = fakeFileSystem.GetFileContent(Path.Join(configuration.WorkingDirectory, configuration.AgentName + ".json"));
            configFile.ShouldNotBeNull();
            configFile.ShouldBe(expectedConfig);

            // Verify the result contains expected agent information
            result.ShouldNotBeNull();
            result.AgentName.ShouldBe(configuration.AgentName);
            result.ProcessId.ShouldBeGreaterThan(0);
            result.Status.ShouldBe(A2AAgentStatus.Starting);
            result.AgentUrl.ShouldStartWith("http://localhost:");
        }
    }
}
