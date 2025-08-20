using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Database;
using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using System.Transactions;

namespace AISwarm.DataLayer.Services;

/// <summary>
/// Database scope service that provides reading and writing scopes using dispose pattern
/// </summary>
public class DatabaseScopeService : IDatabaseScopeService
{
    private readonly CoordinationDbContext _context;

    public DatabaseScopeService(CoordinationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a read-only scope for database operations
    /// </summary>
    public IReadScope CreateReadScope()
    {
        return new ReadScope(_context);
    }

    /// <summary>
    /// Creates a write scope with TransactionScope for automatic transaction handling
    /// </summary>
    public IWriteScope CreateWriteScope()
    {
        return new WriteScope(_context);
    }
}

/// <summary>
/// Read scope implementation that provides access to database operations
/// </summary>
public class ReadScope : IReadScope
{
    public CoordinationDbContext Context { get; }
    public DbSet<Agent> Agents => Context.Agents;

    public ReadScope(CoordinationDbContext context)
    {
        Context = context;
    }

    public void Dispose()
    {
        // Nothing to dispose for read scope
    }
}

/// <summary>
/// Write scope implementation that provides access to database operations with transaction support
/// </summary>
public class WriteScope : IWriteScope
{
    private readonly TransactionScope _transactionScope;
    
    public CoordinationDbContext Context { get; }
    public DbSet<Agent> Agents => Context.Agents;

    public WriteScope(CoordinationDbContext context)
    {
        Context = context;
        _transactionScope = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled);
    }

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