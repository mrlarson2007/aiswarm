using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AgentLauncher;

public static class GeminiManager
{
    /// <summary>
    /// Check if Gemini CLI is available on the system
    /// </summary>
    /// <returns>True if Gemini CLI is found, false otherwise</returns>
    public static async Task<bool> IsGeminiCliAvailableAsync()
    {
        try
        {
            var result = await RunGeminiCommandAsync("--version", timeoutMs: 5000);
            // Consider it available if we get any output (even with warnings) or exit code 0
            return result.IsSuccess || !string.IsNullOrEmpty(result.Output);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the version of the installed Gemini CLI
    /// </summary>
    /// <returns>Version string or null if not available</returns>
    public static async Task<string?> GetGeminiVersionAsync()
    {
        try
        {
            var result = await RunGeminiCommandAsync("--version", timeoutMs: 5000);
            if (result.IsSuccess || !string.IsNullOrEmpty(result.Output))
            {
                // Extract version from output, ignoring warnings
                var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var versionLine = lines.FirstOrDefault(line =>
                    !line.Contains("DeprecationWarning") &&
                    !line.Contains("trace-deprecation") &&
                    !string.IsNullOrWhiteSpace(line));
                return versionLine?.Trim();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Launch Gemini CLI in interactive mode with the specified context file
    /// </summary>
    /// <param name="contextFilePath">Path to the context file</param>
    /// <param name="model">The model to use (optional)</param>
    /// <param name="workingDirectory">Working directory for the Gemini CLI</param>
    /// <returns>True if launched successfully, false otherwise</returns>
    public static async Task<bool> LaunchInteractiveAsync(string contextFilePath, string? model = null, string? workingDirectory = null)
    {
        // Verify context file exists
        if (!File.Exists(contextFilePath))
        {
            Console.WriteLine($"Error: Context file not found: {contextFilePath}");
            return false;
        }

        try
        {
            // Build the Gemini command
            var arguments = BuildGeminiArguments(contextFilePath, model);

            Console.WriteLine($"Launching Gemini CLI...");
            Console.WriteLine($"Command: gemini {arguments}");
            Console.WriteLine($"Working Directory: {workingDirectory ?? Environment.CurrentDirectory}");
            Console.WriteLine();
            Console.WriteLine("=" + new string('=', 60));
            Console.WriteLine(" GEMINI CLI SESSION - Press Ctrl+C to exit");
            Console.WriteLine("=" + new string('=', 60));
            Console.WriteLine();

            // Launch Gemini CLI in interactive mode
            var success = await LaunchGeminiInteractiveProcess(arguments, workingDirectory);

            if (success)
            {
                Console.WriteLine();
                Console.WriteLine("=" + new string('=', 60));
                Console.WriteLine(" GEMINI CLI SESSION STARTED");
                Console.WriteLine("=" + new string('=', 60));
            }

            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error launching Gemini CLI: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Build the command line arguments for Gemini CLI
    /// </summary>
    /// <param name="contextFilePath">Path to the context file</param>
    /// <param name="model">The model to use (optional)</param>
    /// <returns>Command line arguments string</returns>
    private static string BuildGeminiArguments(string contextFilePath, string? model)
    {
        var args = new List<string>();

        // Add model if specified
        if (!string.IsNullOrEmpty(model))
        {
            args.Add($"-m \"{model}\"");
        }

        // Add interactive mode with context file
        args.Add($"-i \"{contextFilePath}\"");

        return string.Join(" ", args);
    }

    /// <summary>
    /// Launch Gemini CLI as an interactive process
    /// </summary>
    /// <param name="arguments">Command line arguments</param>
    /// <param name="workingDirectory">Working directory</param>
    /// <returns>True if process completed successfully</returns>
    private static async Task<bool> LaunchGeminiInteractiveProcess(string arguments, string? workingDirectory)
    {
        var effectiveWorkingDirectory = workingDirectory ?? Environment.CurrentDirectory;

        return await (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? LaunchMacOSTerminal(arguments, effectiveWorkingDirectory)
            : LaunchWindowsPowerShell(arguments, effectiveWorkingDirectory));
    }

    /// <summary>
    /// Launch Gemini CLI in macOS Terminal.app
    /// </summary>
    /// <param name="arguments">Gemini CLI arguments</param>
    /// <param name="workingDirectory">Working directory</param>
    /// <returns>True if launched successfully</returns>
    private static async Task<bool> LaunchMacOSTerminal(string arguments, string workingDirectory)
    {
        // Check if gemini command is available
        if (!await IsCommandAvailableAsync("gemini"))
        {
            Console.WriteLine("Error: 'gemini' command not found. Please ensure Gemini CLI is installed and in your PATH.");
            Console.WriteLine("To install Gemini CLI, follow the instructions at: https://ai.google.dev/gemini-api/docs/cli");
            return false;
        }

        try
        {
            // Build the shell command for macOS
            var shellCommand = GetShellCommand(workingDirectory, arguments);

            // Create process to launch Terminal.app
            var startInfo = CreateMacOSProcessStartInfo(shellCommand, workingDirectory);

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                Console.WriteLine("Gemini CLI session started in new Terminal window.");
                Console.WriteLine("The session will run independently. You can close this launcher.");
                return true;
            }
            else
            {
                Console.WriteLine($"Failed to launch Terminal.app: Exit code {process.ExitCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start Terminal.app: {ex.Message}");
            Console.WriteLine("Please ensure macOS Terminal.app is available and try again.");
            return false;
        }
    }

    /// <summary>
    /// Launch Gemini CLI in Windows PowerShell
    /// </summary>
    /// <param name="arguments">Gemini CLI arguments</param>
    /// <param name="workingDirectory">Working directory</param>
    /// <returns>True if launched successfully</returns>
    private static async Task<bool> LaunchWindowsPowerShell(string arguments, string workingDirectory)
    {
        // Use PowerShell to launch Gemini CLI since it's likely a PowerShell function/command
        var powershellCommand = $"gemini {arguments}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh.exe", // Try PowerShell Core first
            Arguments = $"-NoExit -Command \"& {{Set-Location '{workingDirectory}'; {powershellCommand}}}\"",
            UseShellExecute = true,  // This ensures it opens in a new window
            CreateNoWindow = false,
            WorkingDirectory = workingDirectory,
            WindowStyle = ProcessWindowStyle.Normal
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            Console.WriteLine("Gemini CLI session started in new terminal window.");
            Console.WriteLine("The session will run independently. You can close this launcher.");

            // Don't wait for the process to exit since it's interactive
            await Task.CompletedTask;
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // PowerShell Core not found, try Windows PowerShell
            Console.WriteLine("PowerShell Core (pwsh.exe) not found, trying Windows PowerShell...");

            startInfo.FileName = "powershell.exe";

            try
            {
                using var process = new Process { StartInfo = startInfo };
                process.Start();

                Console.WriteLine("Gemini CLI session started in new terminal window.");
                Console.WriteLine("The session will run independently. You can close this launcher.");

                await Task.CompletedTask;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start PowerShell: {ex.Message}");
                Console.WriteLine("Please ensure PowerShell is available and Gemini CLI is properly installed.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start Gemini CLI: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Run a Gemini command and capture output (for testing/validation)
    /// </summary>
    /// <param name="arguments">Command arguments</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>Command result</returns>
    private static async Task<GeminiCommandResult> RunGeminiCommandAsync(string arguments, int timeoutMs = 10000)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await RunGeminiCommandMacOSAsync(arguments, timeoutMs);
        }
        else
        {
            return await RunGeminiCommandWindowsAsync(arguments, timeoutMs);
        }
    }

    /// <summary>
    /// Run a Gemini command on macOS using shell
    /// </summary>
    /// <param name="arguments">Command arguments</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>Command result</returns>
    private static async Task<GeminiCommandResult> RunGeminiCommandMacOSAsync(string arguments, int timeoutMs)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"gemini {arguments}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Wait for exit with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                return new GeminiCommandResult
                {
                    IsSuccess = false,
                    Output = "",
                    Error = "Command timed out",
                    ExitCode = -1
                };
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            return new GeminiCommandResult
            {
                IsSuccess = process.ExitCode == 0,
                Output = output,
                Error = error,
                ExitCode = process.ExitCode
            };
        }
        catch (Exception ex)
        {
            return new GeminiCommandResult
            {
                IsSuccess = false,
                Output = "",
                Error = ex.Message,
                ExitCode = -1
            };
        }
    }

    /// <summary>
    /// Run a Gemini command on Windows using PowerShell
    /// </summary>
    /// <param name="arguments">Command arguments</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>Command result</returns>
    private static async Task<GeminiCommandResult> RunGeminiCommandWindowsAsync(string arguments, int timeoutMs)
    {
        // Use PowerShell to run Gemini commands since it's likely a PowerShell function
        var powershellCommand = $"gemini {arguments}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh.exe", // Try PowerShell Core first
            Arguments = $"-Command \"{powershellCommand}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false,
            WorkingDirectory = Environment.CurrentDirectory
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Wait for exit with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                return new GeminiCommandResult
                {
                    IsSuccess = false,
                    Output = "",
                    Error = "Command timed out",
                    ExitCode = -1
                };
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            return new GeminiCommandResult
            {
                IsSuccess = process.ExitCode == 0,
                Output = output,
                Error = error,
                ExitCode = process.ExitCode
            };
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // PowerShell Core not found, try Windows PowerShell
            startInfo.FileName = "powershell.exe";

            try
            {
                using var process = new Process { StartInfo = startInfo };
                process.Start();

                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill();
                    return new GeminiCommandResult
                    {
                        IsSuccess = false,
                        Output = "",
                        Error = "Command timed out",
                        ExitCode = -1
                    };
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                return new GeminiCommandResult
                {
                    IsSuccess = process.ExitCode == 0,
                    Output = output,
                    Error = error,
                    ExitCode = process.ExitCode
                };
            }
            catch (Exception ex)
            {
                return new GeminiCommandResult
                {
                    IsSuccess = false,
                    Output = "",
                    Error = ex.Message,
                    ExitCode = -1
                };
            }
        }
        catch (Exception ex)
        {
            return new GeminiCommandResult
            {
                IsSuccess = false,
                Output = "",
                Error = ex.Message,
                ExitCode = -1
            };
        }
    }

    /// <summary>
    /// Get the appropriate shell command for the current platform
    /// </summary>
    /// <param name="workingDirectory">Working directory</param>
    /// <param name="geminiArguments">Gemini CLI arguments</param>
    /// <returns>Shell command appropriate for the platform</returns>
    private static string GetShellCommand(string workingDirectory, string geminiArguments)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: Use shell commands (cd and gemini)
            return $"cd '{workingDirectory}' && gemini {geminiArguments}";
        }
        else
        {
            // Windows: Use PowerShell commands
            return $"Set-Location '{workingDirectory}'; gemini {geminiArguments}";
        }
    }

    /// <summary>
    /// Create ProcessStartInfo for macOS Terminal.app launch
    /// </summary>
    /// <param name="command">Command to execute in terminal</param>
    /// <param name="workingDirectory">Working directory</param>
    /// <returns>ProcessStartInfo configured for macOS</returns>
    private static ProcessStartInfo CreateMacOSProcessStartInfo(string command, string workingDirectory)
    {
        // Use AppleScript to launch Terminal.app with command
        // Escape quotes in the command for AppleScript
        var escapedCommand = command.Replace("\"", "\\\"").Replace("'", "\\'");
        var script = $"tell application \"Terminal\" to do script \"{escapedCommand}\"";

        return new ProcessStartInfo
        {
            FileName = "osascript",
            Arguments = $"-e \"{script}\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    /// <summary>
    /// Check if a command is available on the system
    /// </summary>
    /// <param name="command">Command name to check</param>
    /// <returns>True if command is available, false otherwise</returns>
    private static async Task<bool> IsCommandAvailableAsync(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                startInfo.FileName = "which";
                startInfo.Arguments = command;
            }
            else
            {
                // Windows: Use where command
                startInfo.FileName = "where";
                startInfo.Arguments = command;
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Result of a Gemini CLI command execution
    /// </summary>
    private record GeminiCommandResult
    {
        public bool IsSuccess { get; init; }
        public string Output { get; init; } = "";
        public string Error { get; init; } = "";
        public int ExitCode { get; init; }
    }
}
