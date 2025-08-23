namespace AISwarm.Infrastructure;

/// <inheritdoc />
public class EnvironmentService : IEnvironmentService
{
    public string CurrentDirectory => Environment.CurrentDirectory;
    public string? GetEnvironmentVariable(string variable) => Environment.GetEnvironmentVariable(variable);
}
