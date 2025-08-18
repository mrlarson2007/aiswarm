using System.Reflection;
using AgentLauncher.Services.External;
using AgentLauncher.Services.Logging;

namespace AgentLauncher.Commands;

/// <summary>
/// Handles initialization of .aiswarm directory with template persona files.
/// </summary>
public class InitCommandHandler(
    IAppLogger logger,
    IFileSystemService fileSystem,
    IEnvironmentService environment)
{
    public async Task<bool> RunAsync()
    {
        var currentDir = environment.CurrentDirectory;
        var aiswarmDir = Path.Join(currentDir, ".aiswarm");
        var personasDir = Path.Join(aiswarmDir, "personas");
        var templateFile = Path.Join(personasDir, "template_prompt.md");

        // Check if directory already exists
        if (fileSystem.DirectoryExists(personasDir))
        {
            logger.Warn("Directory .aiswarm/personas already exists. Skipping initialization.");
            return true;
        }

        // Create directories
        fileSystem.CreateDirectory(aiswarmDir);
        fileSystem.CreateDirectory(personasDir);

        // Create template persona file
        var templateContent = GetTemplatePersonaContent();
        await fileSystem.WriteAllTextAsync(templateFile, templateContent);

        logger.Info($"Initialized .aiswarm directory at: {aiswarmDir}");
        logger.Info($"Created personas directory: {personasDir}");
        logger.Info($"Created template persona file: {templateFile}");
        logger.Info("You can now create custom persona files in .aiswarm/personas/ using the *_prompt.md naming convention.");

        return true;
    }

    private static string GetTemplatePersonaContent()
    {
        const string resourceName = "AgentLauncher.Resources.template_prompt.md";
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException($"Resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}