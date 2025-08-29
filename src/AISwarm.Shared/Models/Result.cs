namespace AISwarm.Shared.Models;

/// <summary>
/// Base class for operation results with common success/error patterns
/// </summary>
public abstract class Result<T> where T : Result<T>, new()
{
    public bool Success
    {
        get; init;
    }
    public string? ErrorMessage
    {
        get; init;
    }

    /// <summary>
    /// Creates a failure result with the specified error message
    /// </summary>
    public static T Failure(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };

    /// <summary>
    /// Creates a basic success result (for derived classes to override)
    /// </summary>
    protected static T CreateSuccess() => new()
    {
        Success = true
    };
}
