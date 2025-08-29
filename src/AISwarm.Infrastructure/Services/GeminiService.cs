using System.Text.Json;

namespace AISwarm.Infrastructure;

/// <inheritdoc />
public class GeminiService(
    IInteractiveTerminalService terminal,
    IAppLogger logger,
    IFileSystemService fileSystem) : IGeminiService
{
    private const string GeminiProcessName = "gemini";
    private const string VersionCommand = $"{GeminiProcessName} --version";

    /// <inheritdoc />
    public async Task<bool> LaunchInteractiveAsync(
        string contextFilePath,
        string? model = null,
        string? workingDirectory = null,
        AgentSettings? agentSettings = null,
        bool yolo = false)
    {
        if (!fileSystem.FileExists(contextFilePath))
        {
            logger.Error($"Error: Context file not found: {contextFilePath}");
            return false;
        }

        try
        {
            var workDir = workingDirectory ?? Environment.CurrentDirectory;

            // If agent settings are provided, create Gemini configuration file
            if (agentSettings != null) await CreateGeminiConfigurationAsync(workDir, agentSettings);

            var arguments = BuildGeminiArguments(contextFilePath, model, yolo);
            logger.Info("Launching Gemini CLI...");
            logger.Info($"Command: {GeminiProcessName} {arguments}");
            logger.Info($"Working Directory: {workDir}");
            logger.Info(string.Empty);
            logger.Info("=" + new string('=', 60));
            logger.Info(" GEMINI CLI SESSION - Press Ctrl+C to exit");
            logger.Info("=" + new string('=', 60));
            logger.Info(string.Empty);

            var fullCommand = $"{GeminiProcessName} {arguments}".Trim();
            var started = terminal.LaunchTerminalInteractive(fullCommand, workDir);
            if (started)
            {
                logger.Info(string.Empty);
                logger.Info("=" + new string('=', 60));
                logger.Info(" GEMINI CLI SESSION STARTED");
                logger.Info("=" + new string('=', 60));
            }
            else
            {
                logger.Error("Failed to start terminal session for Gemini CLI.");
            }

            await Task.CompletedTask;
            return started;
        }
        catch (Exception ex)
        {
            logger.Error($"Error launching Gemini CLI: {ex.Message}");
            return false;
        }
    }

    private async Task CreateGeminiConfigurationAsync(
        string workingDirectory,
        AgentSettings agentSettings)
    {
        var geminiDir = Path.Combine(workingDirectory, ".gemini");
        fileSystem.CreateDirectory(geminiDir);

        var configPath = Path.Combine(geminiDir, "settings.json");

        var configuration = new
        {
            mcpServers = new Dictionary<string, object>
            {
                ["aiswarm"] = new
                {
                    url = agentSettings.McpServerUrl,
                    description = $"AISwarm coordination server for agent {agentSettings.AgentId}",
                    trust = true,
                    env = new Dictionary<string, string> { ["AISWARM_AGENT_ID"] = agentSettings.AgentId }
                }
            }
        };

        var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });

        await fileSystem.WriteAllTextAsync(configPath, json);
        logger.Info($"Created Gemini configuration file: {configPath}");
        logger.Info($"Agent ID: {agentSettings.AgentId}");
        logger.Info($"MCP Server: {agentSettings.McpServerUrl}");
    }

    private static string BuildGeminiArguments(
        string contextFilePath,
        string? model,
        bool yolo = false)
    {
        var args = new List<string>();
        if (!string.IsNullOrEmpty(model))
            args.Add($"-m \"{model}\"");

        if (yolo)
            args.Add("--yolo");

        // Tell Gemini that we just created the file and to read it for instructions
        var prompt = $"I've just created \"{contextFilePath}\". Please read it for your instructions.";
        args.Add($"-i \\\"{prompt}\\\"");

        return string.Join(' ', args);
    }
}
