using System.Diagnostics;

namespace AgentLauncher.Services.External;

/// <inheritdoc />
public class ProcessLauncher : IProcessLauncher
{
    /// <inheritdoc />
    public async Task<ProcessResult> RunAsync(string fileName, string arguments, string workingDirectory, int? timeoutMs = null, bool captureOutput = true)
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
                        try { process.Kill(); } catch { }
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
                        try { process.Kill(); } catch { }
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
    public bool StartInteractive(string fileName, string arguments, string workingDirectory)
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
            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
