using AgentLauncher.Commands;
using AISwarm.Tests.TestDoubles;
using Shouldly;

namespace AISwarm.Tests.Commands;

public class InitCommandHandlerTests 
    : ISystemUnderTest<InitCommandHandler>
{
    private readonly TestLogger _logger;
    private readonly FakeFileSystemService _fileSystem;
    private readonly TestEnvironmentService _environment;
    private InitCommandHandler? _systemUnderTest;

    public InitCommandHandler SystemUnderTest =>
        _systemUnderTest ??= new InitCommandHandler(_logger, _fileSystem, _environment);

    public InitCommandHandlerTests()
    {
        _logger = new TestLogger();
        _fileSystem = new FakeFileSystemService();
        _environment = new TestEnvironmentService();
    }

    public class InitializationSuccessTests : InitCommandHandlerTests
    {
        [Fact]
        public async Task WhenInitializingDirectory_ShouldCreateAiswarmPersonasDirectory()
        {
            // Arrange
            _environment.CurrentDirectory = "/test/repo";

            // Act
            var result = await SystemUnderTest.RunAsync();

            // Assert
            result.ShouldBeTrue();
            _fileSystem.DirectoryExists("/test/repo/.aiswarm").ShouldBeTrue();
            _fileSystem.DirectoryExists("/test/repo/.aiswarm/personas").ShouldBeTrue();
        }

        [Fact]
        public async Task WhenInitializingDirectory_ShouldCreateTemplatePersonaFile()
        {
            // Arrange
            _environment.CurrentDirectory = "/test/repo";

            // Act
            var result = await SystemUnderTest.RunAsync();

            // Assert
            result.ShouldBeTrue();
            _fileSystem.FileExists("/test/repo/.aiswarm/personas/template_prompt.md").ShouldBeTrue();
        }

        [Fact]
        public async Task WhenInitializingDirectory_ShouldLogSuccessMessage()
        {
            // Arrange
            _environment.CurrentDirectory = "/test/repo";

            // Act
            var result = await SystemUnderTest.RunAsync();

            // Assert
            result.ShouldBeTrue();
            _logger.Infos.ShouldContain(i => i.Contains("Initialized .aiswarm directory"));
            _logger.Infos.ShouldContain(i => i.Contains(".aiswarm/personas"));
        }
    }

    public class InitializationEdgeCaseTests : InitCommandHandlerTests
    {
        [Fact]
        public async Task WhenDirectoryAlreadyExists_ShouldNotOverwriteAndLogWarning()
        {
            // Arrange
            _environment.CurrentDirectory = "/test/repo";
            _fileSystem.AddDirectory("/test/repo/.aiswarm/personas");
            _fileSystem.AddFile("/test/repo/.aiswarm/personas/template_prompt.md");

            // Act
            var result = await SystemUnderTest.RunAsync();

            // Assert
            result.ShouldBeTrue();
            _logger.Warnings.ShouldContain(w => w.Contains("already exists"));
        }
    }
}
