using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AISwarm.DataLayer;

/// <summary>
/// Database scope service that provides cached read and write scopes for transaction coordination.
/// Implements per-request caching to avoid nested transactions and supports both read-only and full access patterns.
/// </summary>
public class DatabaseScopeService : IDatabaseScopeService
{
    private readonly IDbContextFactory<CoordinationDbContext> _contextFactory;
    private IWriteScope? _cachedWriteScope;
    private IReadScope? _cachedReadScope;
    private bool _disposed;
    private bool _completed;

    public DatabaseScopeService(IDbContextFactory<CoordinationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    ///     Creates a read-only scope for database operations
    /// </summary>
    private IReadScope CreateReadScope()
    {
        return new ReadScope(_contextFactory.CreateDbContext(), ownsContext: true);
    }

    /// <summary>
    ///     Creates a write scope with TransactionScope for automatic transaction handling
    /// </summary>
    private IWriteScope CreateWriteScope()
    {
        return new WriteScope(_contextFactory.CreateDbContext(), ownsContext: true);
    }

    /// <summary>
    /// Gets or creates a write scope for the current DI scope.
    /// The first call creates the scope, subsequent calls return the same instance.
    /// </summary>
    public IWriteScope GetWriteScope()
    {
        ThrowIfDisposed();
        
        // If cached scope is disposed, clear the cache and create a new one
        if (_cachedWriteScope != null && IsDisposed(_cachedWriteScope))
        {
            _cachedWriteScope = null;
        }
        
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
        
        // If cached scope is disposed, clear the cache and create a new one
        if (_cachedReadScope != null && IsDisposed(_cachedReadScope))
        {
            _cachedReadScope = null;
        }
        
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

    private static bool IsDisposed(IDisposable scope)
    {
        // For ReadScope and WriteScope, we can check if the context is disposed
        try
        {
            if (scope is ReadScope readScope)
            {
                // Try to access a property that would throw if disposed
                _ = readScope.Context.Model;
                return false;
            }
            if (scope is WriteScope writeScope)
            {
                // Try to access a property that would throw if disposed
                _ = writeScope.Context.Model;
                return false;
            }
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
        
        return false;
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
    public DbSet<EventLog> EventLogs => Context.EventLogs;

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
    public DbSet<EventLog> EventLogs => Context.EventLogs;

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
