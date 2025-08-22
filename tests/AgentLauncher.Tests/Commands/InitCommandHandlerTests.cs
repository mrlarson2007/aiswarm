using AgentLauncher.Commands;
using AgentLauncher.Services.External;
using AgentLauncher.Services.Logging;
using AISwarm.TestDoubles;
using Shouldly;

namespace AgentLauncher.Tests.Commands;

public class InitCommandHandlerTests
{
    private readonly TestLogger _logger = new();
    private readonly FakeFileSystemService _fileSystem = new();
    private readonly TestEnvironmentService _environment = new();

    private InitCommandHandler SystemUnderTest => new(_logger, _fileSystem, _environment);

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
