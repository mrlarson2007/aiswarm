namespace AISwarm.DataLayer;

/// <summary>
/// Scoped database service that caches database scopes per DI scope.
/// Enables transaction coordination across multiple service calls within the same operation.
/// </summary>
public class ScopedDatabaseService : IScopedDatabaseService, IDisposable
{
    private readonly IDatabaseScopeService _scopeService;
    private IWriteScope? _cachedWriteScope;
    private IReadScope? _cachedReadScope;
    private bool _disposed;

    public ScopedDatabaseService(IDatabaseScopeService scopeService)
    {
        _scopeService = scopeService;
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
            _cachedWriteScope = _scopeService.CreateWriteScope();
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
            _cachedReadScope = _scopeService.CreateReadScope();
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
        
        if (_cachedWriteScope != null)
        {
            _cachedWriteScope.Complete();
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
            throw new ObjectDisposedException(nameof(ScopedDatabaseService));
        }
    }
}