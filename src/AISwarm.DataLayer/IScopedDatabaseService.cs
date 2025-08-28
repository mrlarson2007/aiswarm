namespace AISwarm.DataLayer;

/// <summary>
/// Provides scoped database access that caches the first scope created within a DI scope.
/// This enables transaction coordination across multiple service calls within the same operation.
/// Perfect for MCP tool invocations where multiple services need to share the same transaction.
/// </summary>
public interface IScopedDatabaseService
{
    /// <summary>
    /// Gets or creates a write scope for the current DI scope.
    /// The first call creates the scope, subsequent calls return the same instance.
    /// </summary>
    IWriteScope GetWriteScope();

    /// <summary>
    /// Gets or creates a read scope for the current DI scope.
    /// The first call creates the scope, subsequent calls return the same instance.
    /// </summary>
    IReadScope GetReadScope();

    /// <summary>
    /// Commits any active write scope transaction.
    /// Call this at the end of successful operations to persist changes.
    /// </summary>
    Task CompleteAsync();
}