using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AISwarm.DataLayer;

/// <summary>
///     Database scope service that provides reading and writing scopes using dispose pattern
/// </summary>
public class DatabaseScopeService : IDatabaseScopeService
{
    private readonly IDbContextFactory<CoordinationDbContext>? _contextFactory;
    private readonly CoordinationDbContext? _sharedContext;

    public DatabaseScopeService(IDbContextFactory<CoordinationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // Back-compat for tests that directly provide a DbContext
    public DatabaseScopeService(CoordinationDbContext sharedContext)
    {
        _sharedContext = sharedContext;
    }
    /// <summary>
    ///     Creates a read-only scope for database operations
    /// </summary>
    public IReadScope CreateReadScope()
    {
        if (_contextFactory != null)
            return new ReadScope(_contextFactory.CreateDbContext(), ownsContext: true);
        if (_sharedContext != null)
            return new ReadScope(_sharedContext, ownsContext: false);
        throw new InvalidOperationException("No DbContext available for read scope.");
    }

    /// <summary>
    ///     Creates a write scope with TransactionScope for automatic transaction handling
    /// </summary>
    public IWriteScope CreateWriteScope()
    {
        if (_contextFactory != null)
            return new WriteScope(_contextFactory.CreateDbContext(), ownsContext: true);
        if (_sharedContext != null)
            return new WriteScope(_sharedContext, ownsContext: false);
        throw new InvalidOperationException("No DbContext available for write scope.");
    }
}

/// <summary>
///     Read scope implementation that provides access to database operations
/// </summary>
public class ReadScope(CoordinationDbContext context, bool ownsContext = true) : IReadScope
{
    public CoordinationDbContext Context
    {
        get;
    } = context;
    private readonly bool _ownsContext = ownsContext;

    public DbSet<Agent> Agents => Context.Agents;
    public DbSet<WorkItem> Tasks => Context.Tasks;
    public DbSet<MemoryEntry> MemoryEntries => Context.MemoryEntries;

    public void Dispose()
    {
        if (_ownsContext)
            Context.Dispose();
    }
}

/// <summary>
///     Write scope implementation that provides access to database operations with transaction support
/// </summary>
public class WriteScope(CoordinationDbContext context, bool ownsContext = true) : IWriteScope
{
    private readonly IDbContextTransaction? _transaction =
        (context.Database.ProviderName?.IndexOf("InMemory", StringComparison.OrdinalIgnoreCase) >= 0)
            ? null
            : context.Database.CurrentTransaction ?? context.Database.BeginTransaction();
    private readonly bool _ownsContext = ownsContext;

    public CoordinationDbContext Context
    {
        get;
    } = context;

    public DbSet<Agent> Agents => Context.Agents;
    public DbSet<WorkItem> Tasks => Context.Tasks;
    public DbSet<MemoryEntry> MemoryEntries => Context.MemoryEntries;

    /// <summary>
    ///     Save changes to the database
    /// </summary>
    public async Task<int> SaveChangesAsync() => await Context.SaveChangesAsync();

    /// <summary>
    ///     Commits the transaction (call this before disposing to save changes)
    /// </summary>
    public void Complete()
    {
        _transaction?.Commit();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        if (_ownsContext)
            Context.Dispose();
    }
}
