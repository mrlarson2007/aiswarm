namespace AISwarm.Infrastructure;

public class OperatingSystemService : IOperatingSystemService
{
    public bool IsWindows() => OperatingSystem.IsWindows();
    public bool IsMacOS() => OperatingSystem.IsMacOS();
    public bool IsLinux() => OperatingSystem.IsLinux();
}
