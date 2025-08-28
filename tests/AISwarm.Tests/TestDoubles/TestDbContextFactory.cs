using AISwarm.DataLayer;
using Microsoft.EntityFrameworkCore;

namespace AISwarm.Tests.TestDoubles;

public class TestDbContextFactory(DbContextOptions<CoordinationDbContext> options)
    : IDbContextFactory<CoordinationDbContext>
{
    public CoordinationDbContext CreateDbContext()
    {
        return new CoordinationDbContext(options);
    }
}
