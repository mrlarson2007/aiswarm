using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.DataLayer;

/// <summary>
/// Full database scope service that provides both read and write operations with cached transaction coordination.
/// Inherits read-only operations and adds write capabilities.
/// </summary>
public interface IDatabaseScopeService : IReadOnlyDatabaseScopeService
{
    /// <summary>
    /// Gets a cached write scope for database operations. If no cached scope exists, creates a new one.
    /// Multiple calls return the same cached instance.
    /// </summary>
    /// <returns>A write scope for database operations</returns>
    IWriteScope GetWriteScope();
}

/// <summary>
///     Read scope that provides access to database operations
/// </summary>
public interface IReadScope : IDisposable
{

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

    DbSet<EventLog> EventLogs
    {
        get;
    }
}

/// <summary>
///     Write scope that provides access to database operations with automatic transaction handling
/// </summary>
public interface IWriteScope : IReadScope
{

    /// <summary>
    ///     Save changes to the database
    /// </summary>
    Task<int> SaveChangesAsync();

    /// <summary>
    ///     Commits the transaction (call this before disposing to save changes)
    /// </summary>
    void Complete();
}
