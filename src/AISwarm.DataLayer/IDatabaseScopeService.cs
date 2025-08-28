using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.DataLayer;

/// <summary>
///     Database scope service that provides reading and writing scopes using dispose pattern.
///     When registered as scoped in DI, provides automatic caching and transaction coordination
///     across multiple service calls within the same operation.
/// </summary>
public interface IDatabaseScopeService
{
    /// <summary>
    ///     Creates a read-only scope for database operations
    /// </summary>
    IReadScope CreateReadScope();

    /// <summary>
    ///     Creates a write scope with TransactionScope for automatic transaction handling
    /// </summary>
    IWriteScope CreateWriteScope();

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

/// <summary>
///     Read scope that provides access to database operations
/// </summary>
public interface IReadScope : IDisposable
{
    /// <summary>
    ///     Database context for advanced operations
    /// </summary>
    CoordinationDbContext Context
    {
        get;
    }

    /// <summary>
    ///     Agents DbSet for direct access
    /// </summary>
    DbSet<Agent> Agents
    {
        get;
    }

    /// <summary>
    ///     Tasks DbSet for direct access
    /// </summary>
    DbSet<WorkItem> Tasks
    {
        get;
    }

    DbSet<MemoryEntry> MemoryEntries
    {
        get;
    }
}

/// <summary>
///     Write scope that provides access to database operations with automatic transaction handling
/// </summary>
public interface IWriteScope : IDisposable
{
    /// <summary>
    ///     Database context for advanced operations
    /// </summary>
    CoordinationDbContext Context
    {
        get;
    }

    /// <summary>
    ///     Agents DbSet for direct access
    /// </summary>
    DbSet<Agent> Agents
    {
        get;
    }

    /// <summary>
    ///     Tasks DbSet for direct access
    /// </summary>
    DbSet<WorkItem> Tasks
    {
        get;
    }

    /// <summary>
    ///     MemoryEntries DbSet for direct access
    /// </summary>
    DbSet<MemoryEntry> MemoryEntries
    {
        get;
    }

    /// <summary>
    ///     Save changes to the database
    /// </summary>
    Task<int> SaveChangesAsync();

    /// <summary>
    ///     Commits the transaction (call this before disposing to save changes)
    /// </summary>
    void Complete();
}
