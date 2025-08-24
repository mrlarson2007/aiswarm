namespace AISwarm.Infrastructure;

/// <summary>
///     Abstraction for launching external processes in either captured (wait + collect output)
///     or interactive (fire-and-forget) modes. Enables test substitution and centralizes
///     process configuration.
/// </summary>
public interface IProcessLauncher
{
    /// <summary>
    ///     Run a process to completion optionally capturing stdout/stderr.
    /// </summary>
    /// <param name="fileName">Executable or shell.</param>
    /// <param name="arguments">Raw argument string.</param>
    /// <param name="workingDirectory">Working directory for the process.</param>
    /// <param name="timeoutMs">Optional timeout (ms) after which the process is killed.</param>
    /// <param name="captureOutput">If true, captures stdout/stderr; otherwise inherits console.</param>
    /// <returns>Structured result including success flag and exit code.</returns>
    Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        int? timeoutMs = null,
        bool captureOutput = true);

    /// <summary>
    ///     Start a process intended for interactive user session (non-blocking, no captured output).
    /// </summary>
    /// <returns><c>true</c> if process start was successful.</returns>
    bool StartInteractive(
        string fileName,
        string arguments,
        string workingDirectory);
}

/// <summary>
///     Represents the outcome of a completed process execution.
/// </summary>
/// <param name="IsSuccess">True if exit code == 0.</param>
/// <param name="StandardOutput">Captured standard output (if requested).</param>
/// <param name="StandardError">Captured standard error (if requested).</param>
/// <param name="ExitCode">Raw process exit code.</param>
public record ProcessResult(
    bool IsSuccess,
    string StandardOutput,
    string StandardError,
    int ExitCode);
