using AISwarm.DataLayer.Contracts;

namespace AgentLauncher.Services;

public class SystemTimeService : ITimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
}