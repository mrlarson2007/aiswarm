using AISwarm.Shared.Contracts;

namespace AISwarm.Server.Tests.TestDoubles;

/// <summary>
/// Controllable time service for testing - allows setting specific times
/// </summary>
public class FakeTimeService : ITimeService
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Advances time by the specified amount
    /// </summary>
    public void AdvanceTime(TimeSpan timeSpan)
    {
        UtcNow = UtcNow.Add(timeSpan);
    }
}