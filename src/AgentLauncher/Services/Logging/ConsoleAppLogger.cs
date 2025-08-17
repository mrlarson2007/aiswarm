namespace AgentLauncher.Services.Logging;

/// <summary>
/// Basic logger writing messages to the Console. Can be replaced with richer logging later.
/// </summary>
public class ConsoleAppLogger : IAppLogger
{
    public void Info(string message) => Console.WriteLine(message);
    public void Warn(string message) => Console.WriteLine(message); // Could add prefix
    public void Error(string message) => Console.Error.WriteLine(message);
}
