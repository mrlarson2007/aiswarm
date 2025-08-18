namespace AgentLauncher.Services;

public class OperatingSystemService : IOperatingSystemService
{
    public bool IsWindows() => System.OperatingSystem.IsWindows();
    public bool IsMacOS() => System.OperatingSystem.IsMacOS();
    public bool IsLinux() => System.OperatingSystem.IsLinux();
}
