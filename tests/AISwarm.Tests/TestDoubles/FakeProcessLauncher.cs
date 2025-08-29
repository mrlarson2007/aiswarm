using System.Collections.Concurrent;
using System.Diagnostics;
using AISwarm.Infrastructure;

namespace AISwarm.Tests.TestDoubles;

/// <summary>
/// Fake process launcher that records launch attempts without actually starting processes.
/// Used for testing scenarios where we need to verify process launch behavior without
/// spawning real processes.
/// </summary>
public class FakeProcessLauncher : IProcessLauncher
{
    private readonly ConcurrentBag<Process> _launchedProcesses = new();

    /// <summary>
    /// Gets the collection of processes that were "launched" (but not actually started).
    /// </summary>
    public IReadOnlyCollection<Process> LaunchedProcesses => _launchedProcesses.ToList();

    /// <summary>
    /// Records a process launch attempt and returns a fake process without actually starting it.
    /// </summary>
    /// <param name="startInfo">The process start information</param>
    /// <returns>A fake process object</returns>
    public Process LaunchProcess(ProcessStartInfo startInfo)
    {
        // Create a fake process that appears to be running but doesn't actually start anything
        var fakeProcess = new Process();
        
        // Modify the start info to point to a fake executable to avoid actually launching anything
        var fakeStartInfo = new ProcessStartInfo
        {
            FileName = "fake-process.exe",
            Arguments = startInfo.Arguments,
            WorkingDirectory = startInfo.WorkingDirectory,
            UseShellExecute = startInfo.UseShellExecute,
            RedirectStandardOutput = startInfo.RedirectStandardOutput,
            RedirectStandardError = startInfo.RedirectStandardError,
            CreateNoWindow = startInfo.CreateNoWindow
        };
        
        fakeProcess.StartInfo = fakeStartInfo;
        
        _launchedProcesses.Add(fakeProcess);
        
        return fakeProcess;
    }

    /// <summary>
    /// Simulates running a process asynchronously and returns a fake successful result.
    /// </summary>
    public async Task<ProcessResult> RunAsync(string fileName, string arguments, string workingDirectory, int? timeoutMs = null, bool captureOutput = true)
    {
        // Simulate a small delay
        await Task.Delay(10);
        
        // Record the launch attempt
        var fakeStartInfo = new ProcessStartInfo
        {
            FileName = "fake-" + fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
            CreateNoWindow = true
        };
        
        var fakeProcess = new Process { StartInfo = fakeStartInfo };
        _launchedProcesses.Add(fakeProcess);
        
        // Return a fake successful result
        return new ProcessResult(
            IsSuccess: true,
            StandardOutput: captureOutput ? $"Fake output from {fileName}" : string.Empty,
            StandardError: string.Empty,
            ExitCode: 0);
    }

    /// <summary>
    /// Simulates starting an interactive process and returns true to indicate success.
    /// </summary>
    public bool StartInteractive(string fileName, string arguments, string workingDirectory)
    {
        // Record the launch attempt
        var fakeStartInfo = new ProcessStartInfo
        {
            FileName = "fake-" + fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true
        };
        
        var fakeProcess = new Process { StartInfo = fakeStartInfo };
        _launchedProcesses.Add(fakeProcess);
        
        // Always return true to simulate successful start
        return true;
    }

    /// <summary>
    /// Clears the collection of launched processes. Useful for test cleanup.
    /// </summary>
    public void Reset()
    {
        while (_launchedProcesses.TryTake(out _))
        {
            // Clear all items
        }
    }
}