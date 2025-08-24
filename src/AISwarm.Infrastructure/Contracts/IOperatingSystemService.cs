namespace AISwarm.Infrastructure;

public interface IOperatingSystemService
{
    bool IsWindows();
    bool IsMacOS();
    bool IsLinux();
}
