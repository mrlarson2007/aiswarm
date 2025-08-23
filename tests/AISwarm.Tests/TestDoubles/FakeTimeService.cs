using AISwarm.Infrastructure;

namespace AISwarm.Tests.TestDoubles;

/// <summary>
/// Controllable time service for testing - allows setting specific times
/// and advancing time for deterministic test execution
/// </summary>
public class FakeTimeService : ITimeService
{
    public DateTime UtcNow { get; set; } = new DateTime(2025, 8, 21, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Advances time by the specified amount
    /// </summary>
    public void AdvanceTime(TimeSpan timeSpan)
    {
        UtcNow = UtcNow.Add(timeSpan);
    }

    /// <summary>
    /// Sets the current time to a specific value
    /// </summary>
    public void SetCurrentTime(DateTime dateTime)
    {
        UtcNow = dateTime;
    }
}
