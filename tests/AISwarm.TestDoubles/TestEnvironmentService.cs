using AISwarm.Infrastructure;

namespace AISwarm.TestDoubles;

/// <summary>
/// Test environment service that allows setting environment variables for testing
/// </summary>
public class TestEnvironmentService : IEnvironmentService
{
    public string CurrentDirectory { get; set; } = "/repo";
    private readonly Dictionary<string, string?> _vars = new();

    public string? GetEnvironmentVariable(string variable)
        => _vars.TryGetValue(variable, out var v) ? v : null;

    public TestEnvironmentService SetVar(
        string key, 
        string? value)
    {
        _vars[key] = value;
        return this;
    }
}