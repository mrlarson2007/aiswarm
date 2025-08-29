using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace AISwarm.Infrastructure;

/// <summary>
///     Logger implementation using Microsoft.Extensions.Logging for structured logging capabilities.
/// </summary>
public class ConsoleAppLogger : IAppLogger, IDisposable
{
    private readonly ILogger<ConsoleAppLogger> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public ConsoleAppLogger()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information));

        _logger = _loggerFactory.CreateLogger<ConsoleAppLogger>();
    }

    public void Info(string message)
    {
        _logger.LogInformation("{Message}", message);
    }

    public void Warn(string message)
    {
        _logger.LogWarning("{Message}", message);
    }

    public void Error(string message)
    {
        _logger.LogError("{Message}", message);
    }

    public void Dispose()
    {
        _loggerFactory?.Dispose();
    }
}
