namespace AgentLauncher.Services.External;

internal sealed class EnvironmentService : IEnvironmentService
{
    public string CurrentDirectory => System.Environment.CurrentDirectory;
    public string? GetEnvironmentVariable(string variable) => System.Environment.GetEnvironmentVariable(variable);
}
