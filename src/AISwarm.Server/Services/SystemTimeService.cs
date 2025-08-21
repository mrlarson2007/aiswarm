using AISwarm.DataLayer.Contracts;

namespace AISwarm.Server.Services;

/// <summary>
/// System time service implementation
/// </summary>
public class SystemTimeService : ITimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
}