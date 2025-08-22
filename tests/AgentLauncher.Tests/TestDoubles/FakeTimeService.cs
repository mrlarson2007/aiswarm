using AISwarm.DataLayer.Contracts;

namespace AgentLauncher.Tests.TestDoubles;

/// <summary>
/// Controllable time service for testing - allows setting specific times
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
}