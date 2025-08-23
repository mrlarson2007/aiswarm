namespace AISwarm.Infrastructure;

/// <summary>
/// Service for providing current time - enables time control in tests
/// </summary>
public interface ITimeService
{
    /// <summary>
    /// Gets the current UTC time
    /// </summary>
    DateTime UtcNow { get; }
}