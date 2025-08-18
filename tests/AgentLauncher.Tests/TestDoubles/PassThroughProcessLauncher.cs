using AgentLauncher.Services.External;
using System.Collections.Concurrent;

namespace AgentLauncher.Tests.TestDoubles;

/// <summary>
/// Lightweight pass-through test double for IProcessLauncher. Provides default
/// successful git responses while allowing explicit expectations for error or
/// specialized scenarios. Non-matching calls succeed with empty output.
/// </summary>
public sealed class PassThroughProcessLauncher : IProcessLauncher
{
    private readonly ConcurrentQueue<Expectation> _expectations = new();
    public readonly List<Invocation> Invocations = new();

    public record Invocation(string File, string Arguments, string WorkingDir);
    public record Expectation(string File, Func<string, bool> ArgsMatch, ProcessResult Result, bool Consume = true);

    public void Enqueue(
        string file,
        Func<string, bool> argsMatch,
        ProcessResult result,
        bool consume = true) => _expectations.Enqueue(new Expectation(file, argsMatch, result, consume));

    public Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        int? timeoutMs = null,
        bool captureOutput = true)
    {
        Invocations.Add(new Invocation(fileName, arguments, workingDirectory));

        if (TryMatchExpectation(fileName, arguments, out var exp))
            return Task.FromResult(exp.Result);

        return Task.FromResult(new ProcessResult(true, string.Empty, string.Empty, 0));
    }

    public bool StartInteractive(string fileName, string arguments, string workingDirectory)
    {
        Invocations.Add(new Invocation(fileName, arguments, workingDirectory));
        return true;
    }

    private bool TryMatchExpectation(string file, string args, out Expectation exp)
    {
        foreach (var e in _expectations)
        {
            if (string.Equals(e.File, file, StringComparison.OrdinalIgnoreCase) && e.ArgsMatch(args))
            {
                if (e.Consume)
                {
                    var remaining = new List<Expectation>();
                    var removed = false;
                    while (_expectations.TryDequeue(out var current))
                    {
                        if (!removed && ReferenceEquals(current, e))
                        {
                            removed = true;
                            continue;
                        }
                        remaining.Add(current);
                    }
                    foreach (var r in remaining)
                        _expectations.Enqueue(r);
                }
                exp = e;
                return true;
            }
        }
        exp = default!;
        return false;
    }
}
