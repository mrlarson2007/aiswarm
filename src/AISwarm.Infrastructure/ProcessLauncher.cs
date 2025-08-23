using System.Diagnostics;

namespace AISwarm.Infrastructure;

/// <inheritdoc />
public class ProcessLauncher : IProcessLauncher
{
    private readonly IAppLogger _logger;

    public ProcessLauncher(IAppLogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        int? timeoutMs = null,
        bool captureOutput = true)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = !captureOutput,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
            CreateNoWindow = captureOutput
        };

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();

            if (captureOutput)
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                if (timeoutMs.HasValue)
                {
                    var completed = await Task.WhenAny(Task.Run(() => process.WaitForExit()), Task.Delay(timeoutMs.Value));
                    if (completed is not Task t || t.IsCanceled)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch { }
                        return new ProcessResult(false, "", "Process timed out", -1);
                    }
                }
                else
                {
                    await process.WaitForExitAsync();
                }

                var stdout = await stdoutTask;
                var stderr = await stderrTask;
                return new ProcessResult(process.ExitCode == 0, stdout, stderr, process.ExitCode);
            }
            else
            {
                if (timeoutMs.HasValue)
                {
                    var completed = await Task.WhenAny(Task.Run(() => process.WaitForExit()), Task.Delay(timeoutMs.Value));
                    if (completed is not Task t || t.IsCanceled)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch { }
                        return new ProcessResult(false, "", "Process timed out", -1);
                    }
                }
                else
                {
                    await process.WaitForExitAsync();
                }
                return new ProcessResult(process.ExitCode == 0, "", "", process.ExitCode);
            }
        }
        catch (Exception ex)
        {
            return new ProcessResult(false, "", ex.Message, -1);
        }
    }

    /// <inheritdoc />
    public bool StartInteractive(
        string fileName,
        string arguments,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true,
            CreateNoWindow = false
        };

        try
        {
            var result = Process.Start(startInfo);
            if (result == null)
            {
                _logger.Error($"Failed to start interactive process: Process.Start returned null for '{fileName} {arguments}'");
                return false;
            }

            int counter = 0;
            while (result.HasExited == false && counter < 15)
            {
                Thread.Sleep(1000);
                result.Refresh();
                counter++;
            }

            if (result.HasExited)
            {
                _logger.Error($"Interactive process exited immediately with code {result.ExitCode}: '{fileName} {arguments}' in directory '{workingDirectory}'");
                _logger.Error(result.StandardOutput.ReadToEnd());
                _logger.Error(result.StandardError.ReadToEnd());
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Exception starting interactive process '{fileName} {arguments}': {ex.Message}");
            return false;
        }
    }
}
