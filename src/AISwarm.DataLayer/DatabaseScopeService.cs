using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AISwarm.DataLayer;

/// <summary>
///     Database scope service that provides reading and writing scopes using dispose pattern.
///     When registered as scoped in DI, provides automatic caching and transaction coordination
///     across multiple service calls within the same operation.
/// </summary>
public class DatabaseScopeService(
    IDbContextFactory<CoordinationDbContext> contextFactory) : IDatabaseScopeService, IDisposable
{
    private IWriteScope? _cachedWriteScope;
    private IReadScope? _cachedReadScope;
    private bool _disposed;
    private bool _completed;

    /// <summary>
    ///     Creates a read-only scope for database operations
    /// </summary>
    public IReadScope CreateReadScope()
    {
        return new ReadScope(contextFactory.CreateDbContext(), ownsContext: true);
    }

    /// <summary>
    ///     Creates a write scope with TransactionScope for automatic transaction handling
    /// </summary>
    public IWriteScope CreateWriteScope()
    {
        return new WriteScope(contextFactory.CreateDbContext(), ownsContext: true);
    }

    /// <summary>
    /// Gets or creates a write scope for the current DI scope.
    /// The first call creates the scope, subsequent calls return the same instance.
    /// </summary>
    public IWriteScope GetWriteScope()
    {
        ThrowIfDisposed();
        
        if (_cachedWriteScope == null)
        {
            _cachedWriteScope = CreateWriteScope();
        }

        return _cachedWriteScope;
    }

    /// <summary>
    /// Gets or creates a read scope for the current DI scope.
    /// The first call creates the scope, subsequent calls return the same instance.
    /// </summary>
    public IReadScope GetReadScope()
    {
        ThrowIfDisposed();
        
        if (_cachedReadScope == null)
        {
            _cachedReadScope = CreateReadScope();
        }

        return _cachedReadScope;
    }

    /// <summary>
    /// Commits any active write scope transaction.
    /// Call this at the end of successful operations to persist changes.
    /// </summary>
    public Task CompleteAsync()
    {
        ThrowIfDisposed();
        
        if (_cachedWriteScope != null && !_completed)
        {
            _cachedWriteScope.Complete();
            _completed = true;
        }
        
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cachedWriteScope?.Dispose();
            _cachedReadScope?.Dispose();
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DatabaseScopeService));
        }
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
