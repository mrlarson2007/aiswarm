using AgentLauncher.Services.External;
using AgentLauncher.Services.Logging;
using AgentLauncher.Models;

namespace AgentLauncher.Services;

/// <inheritdoc />
public class GeminiService(
    Terminals.IInteractiveTerminalService terminal,
    IAppLogger logger,
    IFileSystemService fileSystem) : IGeminiService
{

    private const string GeminiProcessName = "gemini";
    private const string VersionCommand = $"{GeminiProcessName} --version";
    /// <inheritdoc />
    public async Task<bool> IsGeminiCliAvailableAsync()
    {
        try
        {
            var result = await terminal.RunAsync(VersionCommand, Environment.CurrentDirectory, 5000);
            return result.IsSuccess || !string.IsNullOrEmpty(result.StandardOutput);
        }
        catch { return false; }
    }

    /// <inheritdoc />
    public async Task<string?> GetGeminiVersionAsync()
    {
        try
        {
            var result = await terminal.RunAsync(VersionCommand, Environment.CurrentDirectory, 5000);
            if (result.IsSuccess || !string.IsNullOrEmpty(result.StandardOutput))
            {
                var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var versionLine = lines.FirstOrDefault(line => !line.Contains("DeprecationWarning") && !line.Contains("trace-deprecation") && !string.IsNullOrWhiteSpace(line));
                return versionLine?.Trim();
            }
            return null;
        }
        catch { return null; }
    }

    /// <inheritdoc />
    public async Task<bool> LaunchInteractiveAsync(
        string contextFilePath, 
        string? model = null, 
        string? workingDirectory = null, 
        AgentSettings? agentSettings = null)
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
            if (agentSettings != null)
            {
                await CreateGeminiConfigurationAsync(workDir, agentSettings);
            }

            var arguments = BuildGeminiArguments(contextFilePath, model);
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

    /// <inheritdoc />
    [Obsolete("Use LaunchInteractiveAsync with agentSettings parameter instead")]
    public Task<bool> LaunchInteractiveWithSettingsAsync(
        string contextFilePath, 
        string? model, 
        AgentSettings agentSettings, 
        string? workingDirectory = null)
    {
        return LaunchInteractiveAsync(
            contextFilePath, 
            model, 
            workingDirectory, 
            agentSettings);
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
                    timeout = 10000,
                    trust = true,
                    env = new Dictionary<string, string>
                    {
                        ["AISWARM_AGENT_ID"] = agentSettings.AgentId
                    }
                }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(configuration, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        await fileSystem.WriteAllTextAsync(configPath, json);
        logger.Info($"Created Gemini configuration file: {configPath}");
        logger.Info($"Agent ID: {agentSettings.AgentId}");
        logger.Info($"MCP Server: {agentSettings.McpServerUrl}");
    }

    private static string BuildGeminiArguments(string contextFilePath, string? model)
    {
        var args = new List<string>();
        if (!string.IsNullOrEmpty(model))
            args.Add($"-m \"{model}\"");
        args.Add($"-i \"{contextFilePath}\"");
        return string.Join(' ', args);
    }
}
