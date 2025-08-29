namespace AISwarm.DataLayer;

/// <summary>
/// Read-only interface for database scope service that provides read operations and transaction completion.
/// </summary>
public interface IReadOnlyDatabaseScopeService : IDisposable
{
    /// <summary>
    /// Gets a cached read scope for database operations. If no cached scope exists, creates a new one.
    /// Multiple calls return the same cached instance.
    /// </summary>
    /// <returns>A read scope for database operations</returns>
    IReadScope GetReadScope();

    /// <summary>
    /// Completes all pending transactions in both read and write scopes if they exist.
    /// This method can be called multiple times safely.
    /// </summary>
    /// <returns>A task representing the completion operation</returns>
    Task CompleteAsync();
}
