using AgentLauncher.Services.External;

namespace AgentLauncher.Services;

/// <inheritdoc />
public class GeminiService(
    Terminals.IInteractiveTerminalService terminal) : IGeminiService
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
            Console.WriteLine($"Error: Context file not found: {contextFilePath}");
            return false;
        }
        try
        {
            var arguments = BuildGeminiArguments(contextFilePath, model);
            Console.WriteLine("Launching Gemini CLI...");
            Console.WriteLine($"Command: {GeminiProcessName} {arguments}");
            Console.WriteLine($"Working Directory: {workingDirectory ?? Environment.CurrentDirectory}");
            Console.WriteLine();
            Console.WriteLine("=" + new string('=', 60));
            Console.WriteLine(" GEMINI CLI SESSION - Press Ctrl+C to exit");
            Console.WriteLine("=" + new string('=', 60));
            Console.WriteLine();

            var fullCommand = $"{GeminiProcessName} {arguments}".Trim();
            var started = terminal.LaunchTerminalInteractive(fullCommand, workingDirectory ?? Environment.CurrentDirectory);
            if (started)
            {
                Console.WriteLine();
                Console.WriteLine("=" + new string('=', 60));
                Console.WriteLine(" GEMINI CLI SESSION STARTED");
                Console.WriteLine("=" + new string('=', 60));
            }
            else
            {
                Console.WriteLine("Failed to start PowerShell window for Gemini CLI.");
            }
            await Task.CompletedTask;
            return started;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error launching Gemini CLI: {ex.Message}");
            return false;
        }
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
