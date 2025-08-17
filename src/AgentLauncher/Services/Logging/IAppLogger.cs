namespace AgentLauncher.Services.Logging;

/// <summary>
/// Minimal application logging abstraction (decouples tests from Console redirection).
/// </summary>
public interface IAppLogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}
