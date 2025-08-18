using AgentLauncher.Services.External;

namespace AgentLauncher.Services;

/// <inheritdoc />
public class GeminiService(
    IProcessLauncher process) : IGeminiService
{

    /// <inheritdoc />
    public async Task<bool> IsGeminiCliAvailableAsync()
    {
        try
        {
            var (shell, args) = BuildVersionCheckCommand();
            var result = await process.RunAsync(shell, args, Environment.CurrentDirectory, 5000);
            return result.IsSuccess || !string.IsNullOrEmpty(result.StandardOutput);
        }
        catch { return false; }
    }

    /// <inheritdoc />
    public async Task<string?> GetGeminiVersionAsync()
    {
        try
        {
            var (shell, args) = BuildVersionCheckCommand();
            var result = await process.RunAsync(shell, args, Environment.CurrentDirectory, 5000);
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
            Console.WriteLine($"Command: gemini {arguments}");
            Console.WriteLine($"Working Directory: {workingDirectory ?? Environment.CurrentDirectory}");
            Console.WriteLine();
            Console.WriteLine("=" + new string('=', 60));
            Console.WriteLine(" GEMINI CLI SESSION - Press Ctrl+C to exit");
            Console.WriteLine("=" + new string('=', 60));
            Console.WriteLine();

            var started = StartInteractiveGemini(arguments, workingDirectory ?? Environment.CurrentDirectory);
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

    private static string EscapeSingleQuotes(string input) => input.Replace("'", "''");

    private static bool IsWindows => OperatingSystem.IsWindows();
    private static bool IsMac => OperatingSystem.IsMacOS();
    private static bool IsLinux => OperatingSystem.IsLinux();

    private (string shell, string args) BuildVersionCheckCommand()
    {
        if (IsWindows)
            return ("pwsh.exe", "-Command \"gemini --version\"");
        // Use sh -c for broad POSIX compatibility
        return ("/bin/sh", "-c 'gemini --version'" );
    }

    private bool StartInteractiveGemini(string geminiArgs, string workDir)
    {
        // Windows: launch new PowerShell window staying open
        if (IsWindows)
        {
            var cmd = $"-NoExit -Command \"& {EscapeSingleQuotes("Set-Location")} '{workDir.Replace("'", "''")}' ; gemini {geminiArgs}\"";
            return process.StartInteractive("pwsh.exe", cmd, workDir);
        }
        // macOS: try default Terminal via 'osascript' to open a new window
        if (IsMac)
        {
                // Attempt to open new macOS Terminal window using AppleScript
                var escapedWd = workDir.Replace("\"", "\\\"");
                var escapedCmd = $"cd '{workDir.Replace("'", "'\\''")}' ; gemini {geminiArgs}".Replace("\"", "\\\"");
                var appleScript = $"osascript -e \"tell application 'Terminal' to do script \"\"{escapedCmd}\"\"\"";
                var started = process.StartInteractive("/bin/sh", $"-c \"{appleScript}\"", workDir);
                if (started) return true;
                // Fallback to current shell
                return process.StartInteractive("/bin/sh", $"-c 'cd {workDir.Replace("'", "'\\''")} ; gemini {geminiArgs}'", workDir);
        }
        // Linux: attempt x-terminal-emulator, then gnome-terminal, fallback to current shell
        var terminalCandidates = new[]{"x-terminal-emulator","gnome-terminal","konsole","xfce4-terminal","xterm"};
        foreach (var term in terminalCandidates)
        {
            if (process.StartInteractive(term, $"-e sh -c 'cd {workDir.Replace("'", "'\\''")} ; gemini {geminiArgs}; exec sh'", workDir))
                return true;
        }
        // final fallback: current shell (blocks)
        return process.StartInteractive("/bin/sh", $"-c 'cd {workDir.Replace("'", "'\\''")} ; gemini {geminiArgs}'", workDir);
    }
}
