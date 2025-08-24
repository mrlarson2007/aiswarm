namespace AISwarm.Infrastructure;

/// <summary>
/// Abstraction over System.Environment to enable deterministic testing.
/// </summary>
public interface IEnvironmentService
{
    /// <summary>Gets the current working directory.</summary>
    string CurrentDirectory
    {
        get;
    }

    /// <summary>Returns the value of the specified environment variable or null.</summary>
    string? GetEnvironmentVariable(string variable);
}
