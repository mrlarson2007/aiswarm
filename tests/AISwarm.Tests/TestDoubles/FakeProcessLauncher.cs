using System.Collections.Concurrent;
using System.Diagnostics;
using AISwarm.Infrastructure;

namespace AISwarm.Tests.TestDoubles;

/// <summary>
///     Fake process launcher that records launch attempts without actually starting processes.
///     Used for testing scenarios where we need to verify process launch behavior without
///     spawning real processes.
/// </summary>
public class FakeProcessLauncher : IProcessLauncher
{
    private readonly ConcurrentBag<Process> _launchedProcesses = [];

    /// <summary>
    ///     Gets the collection of processes that were "launched" (but not actually started).
    /// </summary>
    public IReadOnlyCollection<Process> LaunchedProcesses => _launchedProcesses.ToList();

    /// <summary>
    ///     Simulates running a process asynchronously and returns a fake successful result.
    /// </summary>
    public async Task<ProcessResult> RunAsync(string fileName, string arguments, string workingDirectory,
        int? timeoutMs = null, bool captureOutput = true)
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
            true,
            captureOutput ? $"Fake output from {fileName}" : string.Empty,
            string.Empty,
            0);
    }

    /// <summary>
    ///     Simulates starting an interactive process and returns true to indicate success.
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
    ///     Records a process launch attempt and returns a fake process without actually starting it.
    /// </summary>
    /// <param name="fileName">Executable or shell.</param>
    /// <param name="arguments">Raw argument string.</param>
    /// <param name="workingDirectory">Working directory for the process.</param>
    /// <returns>A fake process launch result</returns>
    public ProcessLaunchResult Launch(string fileName, string arguments, string workingDirectory)
    {
        var fakeStartInfo = new ProcessStartInfo
        {
            FileName = "fake-" + fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true
        };

        var fakeProcess = new Process { StartInfo = fakeStartInfo };
        _launchedProcesses.Add(fakeProcess);

        return new ProcessLaunchResult(true, 9999, fakeProcess); // Use dummy ID 9999
    }

    /// <summary>
    ///     Clears the collection of launched processes. Useful for test cleanup.
    /// </summary>
    public void Reset()
    {
        while (_launchedProcesses.TryTake(out _))
        {
            // Clear all items
        }
    }
}
