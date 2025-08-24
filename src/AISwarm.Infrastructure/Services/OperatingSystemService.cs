namespace AISwarm.Infrastructure;

public class OperatingSystemService : IOperatingSystemService
{
    public bool IsWindows()
    {
        return OperatingSystem.IsWindows();
    }

    public bool IsMacOS()
    {
        return OperatingSystem.IsMacOS();
    }

    public bool IsLinux()
    {
        return OperatingSystem.IsLinux();
    }
}
