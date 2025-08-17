using AgentLauncher.Services.Logging;
using System.Collections.Concurrent;

namespace AgentLauncher.Tests.TestDoubles;

public class TestLogger : IAppLogger
{
    public ConcurrentBag<string> Infos { get; } = new();
    public ConcurrentBag<string> Warnings { get; } = new();
    public ConcurrentBag<string> Errors { get; } = new();

    public void Info(string message) => Infos.Add(message);
    public void Warn(string message) => Warnings.Add(message);
    public void Error(string message, Exception? ex = null)
    {
        if (ex != null)
            Errors.Add($"{message} :: {ex.GetType().Name}: {ex.Message}");
        else
            Errors.Add(message);
    }
}
