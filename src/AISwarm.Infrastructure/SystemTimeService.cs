namespace AISwarm.Infrastructure;

/// <inheritdoc />
public class SystemTimeService : ITimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
}
