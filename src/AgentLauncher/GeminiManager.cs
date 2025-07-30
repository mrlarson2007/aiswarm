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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await LaunchLinuxGeminiProcess(arguments, workingDirectory);
        }
        else
        {
            return await LaunchWindowsGeminiProcess(arguments, workingDirectory);
        }
    }

    /// <summary>
    /// Launch Gemini CLI in a Linux terminal emulator
    /// </summary>
    /// <param name="arguments">Gemini command arguments</param>
    /// <param name="workingDirectory">Working directory</param>
    /// <returns>True if process started successfully</returns>
    private static async Task<bool> LaunchLinuxGeminiProcess(string arguments, string? workingDirectory)
    {
        var workDir = workingDirectory ?? Environment.CurrentDirectory;
        var geminiCommand = $"cd '{workDir}' && gemini {arguments}";

        // Try different terminal emulators in order of preference
        var terminals = new[]
        {
            new { Name = "gnome-terminal", Args = $"-- bash -c \"{geminiCommand}; exec bash\"" },
            new { Name = "konsole", Args = $"--hold -e bash -c \"{geminiCommand}\"" },
            new { Name = "xterm", Args = $"-hold -e bash -c \"{geminiCommand}\"" },
            new { Name = "x-terminal-emulator", Args = $"-- bash -c \"{geminiCommand}; exec bash\"" }
        };

        foreach (var terminal in terminals)
        {
            if (await IsCommandAvailableAsync(terminal.Name))
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = terminal.Name,
                        Arguments = terminal.Args,
                        UseShellExecute = false,
                        CreateNoWindow = false,
                        WorkingDirectory = workDir
                    };

                    using var process = new Process { StartInfo = startInfo };
                    process.Start();

                    Console.WriteLine($"Gemini CLI session started in {terminal.Name} terminal.");
                    Console.WriteLine("The session will run independently. You can close this launcher.");

                    await Task.CompletedTask;
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start {terminal.Name}: {ex.Message}");
                    // Continue to try next terminal
                }
            }
        }

        Console.WriteLine("No supported terminal emulator found.");
        Console.WriteLine("Please install one of: gnome-terminal, konsole, xterm");
        Console.WriteLine("Or run the command manually:");
        Console.WriteLine($"  {geminiCommand}");
        
        return false;
    }

    /// <summary>
    /// Launch Gemini CLI using Windows PowerShell
    /// </summary>
    /// <param name="arguments">Gemini command arguments</param>
    /// <param name="workingDirectory">Working directory</param>
    /// <returns>True if process started successfully</returns>
    private static async Task<bool> LaunchWindowsGeminiProcess(string arguments, string? workingDirectory)
    {
        // Use PowerShell to launch Gemini CLI since it's likely a PowerShell function/command
        var powershellCommand = $"gemini {arguments}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh.exe", // Try PowerShell Core first
            Arguments = $"-NoExit -Command \"& {{Set-Location '{workingDirectory ?? Environment.CurrentDirectory}'; {powershellCommand}}}\"",
            UseShellExecute = true,  // This ensures it opens in a new window
            CreateNoWindow = false,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
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
    /// Check if a command is available on the system
    /// </summary>
    /// <param name="command">Command name to check</param>
    /// <returns>True if command is available</returns>
    private static async Task<bool> IsCommandAvailableAsync(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

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
    /// Run a Gemini command and capture output (for testing/validation)
    /// </summary>
    /// <param name="arguments">Command arguments</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>Command result</returns>
    private static async Task<GeminiCommandResult> RunGeminiCommandAsync(string arguments, int timeoutMs = 10000)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await RunLinuxGeminiCommandAsync(arguments, timeoutMs);
        }
        else
        {
            return await RunWindowsGeminiCommandAsync(arguments, timeoutMs);
        }
    }

    /// <summary>
    /// Run a Gemini command on Linux using bash
    /// </summary>
    /// <param name="arguments">Command arguments</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    /// <returns>Command result</returns>
    private static async Task<GeminiCommandResult> RunLinuxGeminiCommandAsync(string arguments, int timeoutMs = 10000)
    {
        var geminiCommand = $"gemini {arguments}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-c \"{geminiCommand}\"",
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
    private static async Task<GeminiCommandResult> RunWindowsGeminiCommandAsync(string arguments, int timeoutMs = 10000)
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
