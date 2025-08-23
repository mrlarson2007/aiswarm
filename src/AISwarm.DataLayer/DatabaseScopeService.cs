using System.Transactions;
using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.DataLayer;

/// <summary>
/// Database scope service that provides reading and writing scopes using dispose pattern
/// </summary>
public class DatabaseScopeService(
    CoordinationDbContext context) : IDatabaseScopeService
{
    /// <summary>
    /// Creates a read-only scope for database operations
    /// </summary>
    public IReadScope CreateReadScope()
    {
        return new ReadScope(context);
    }

    /// <summary>
    /// Creates a write scope with TransactionScope for automatic transaction handling
    /// </summary>
    public IWriteScope CreateWriteScope()
    {
        return new WriteScope(context);
    }
}

/// <summary>
/// Read scope implementation that provides access to database operations
/// </summary>
public class ReadScope(CoordinationDbContext context) : IReadScope
{
    public CoordinationDbContext Context
    {
        get;
    } = context;

    public DbSet<Agent> Agents => Context.Agents;
    public DbSet<WorkItem> Tasks => Context.Tasks;

    public void Dispose()
    {
        // Nothing to dispose for read scope
    }
}

/// <summary>
/// Write scope implementation that provides access to database operations with transaction support
/// </summary>
public class WriteScope(CoordinationDbContext context) : IWriteScope
{
    private readonly TransactionScope _transactionScope = new(
        TransactionScopeOption.Required,
        TransactionScopeAsyncFlowOption.Enabled);

    public CoordinationDbContext Context
    {
        get;
    } = context;

    public DbSet<Agent> Agents => Context.Agents;
    public DbSet<WorkItem> Tasks => Context.Tasks;

    /// <summary>
    /// Save changes to the database
    /// </summary>
    public async Task<int> SaveChangesAsync()
    {
        return await Context.SaveChangesAsync();
    }

    /// <summary>
    /// Commits the transaction (call this before disposing to save changes)
    /// </summary>
    public void Complete()
    {
        _transactionScope.Complete();
    }

    public void Dispose()
    {
        _transactionScope?.Dispose();
    }
}
