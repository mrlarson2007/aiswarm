using AgentLauncher.Services.External;
using AgentLauncher.Services.Logging;
using AgentLauncher.Models;

namespace AgentLauncher.Services;

/// <inheritdoc />
public class GeminiService(
    Terminals.IInteractiveTerminalService terminal,
    IAppLogger logger) : IGeminiService
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
    public async Task<bool> LaunchInteractiveAsync(string contextFilePath, string? model = null, string? workingDirectory = null)
    {
        if (!File.Exists(contextFilePath))
        {
            logger.Error($"Error: Context file not found: {contextFilePath}");
            return false;
        }
        try
        {
            var arguments = BuildGeminiArguments(contextFilePath, model);
            logger.Info("Launching Gemini CLI...");
            logger.Info($"Command: {GeminiProcessName} {arguments}");
            logger.Info($"Working Directory: {workingDirectory ?? Environment.CurrentDirectory}");
            logger.Info(string.Empty);
            logger.Info("=" + new string('=', 60));
            logger.Info(" GEMINI CLI SESSION - Press Ctrl+C to exit");
            logger.Info("=" + new string('=', 60));
            logger.Info(string.Empty);

            var fullCommand = $"{GeminiProcessName} {arguments}".Trim();
            var started = terminal.LaunchTerminalInteractive(fullCommand, workingDirectory ?? Environment.CurrentDirectory);
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
    public Task<bool> LaunchInteractiveWithSettingsAsync(string contextFilePath, string? model, AgentSettings agentSettings, string? workingDirectory = null)
    {
        // TODO: Implement MCP server configuration and agent settings
        throw new NotImplementedException("LaunchInteractiveWithSettingsAsync not yet implemented");
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
