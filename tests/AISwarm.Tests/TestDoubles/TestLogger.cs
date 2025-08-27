using System.Collections.Concurrent;
using AISwarm.Infrastructure;

namespace AISwarm.Tests.TestDoubles;

/// <summary>
///     Test logger that captures log messages for verification in tests
/// </summary>
public class TestLogger : IAppLogger
{
    public ConcurrentBag<string> Infos
    {
        get;
    } = [];

    public ConcurrentBag<string> Warnings
    {
        get;
    } = [];

    public ConcurrentBag<string> Errors
    {
        get;
    } = [];

    public void Info(string message)
    {
        Infos.Add(message);
    }

    public void Warn(string message)
    {
        Warnings.Add(message);
    }

    public void Error(string message)
    {
        Errors.Add(message);
    }
}
