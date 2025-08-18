namespace AgentLauncher.Services;

public interface IOperatingSystemService
{
    bool IsWindows();
    bool IsMacOS();
    bool IsLinux();
}
