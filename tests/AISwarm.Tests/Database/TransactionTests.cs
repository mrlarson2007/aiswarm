using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AISwarm.Tests.Database;

public class DatabaseScopeTests : IDisposable
{
    private readonly IDatabaseScopeService _scopeService;

    public DatabaseScopeTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _scopeService = new DatabaseScopeService(new TestDbContextFactory(options));
    }

    public void Dispose()
    {
        // Context lifecycle managed by scopes
    }

    [Fact]
    public async Task WhenUsingReadScope_ShouldReturnData()
    {
        // Arrange
        var agent = new MemoryEntry
        {
            Id = "read-test-agent",
            Key = "tester",
            Value = "read value"
        };

        // Arrange - Setup test data
        using (var setupScope = _scopeService.GetWriteScope())
        {
            setupScope.MemoryEntries.Add(agent);
            await setupScope.SaveChangesAsync();
            setupScope.Complete();
        }

        // Act
        MemoryEntry? retrievedAgent;
        using (var scope = _scopeService.GetReadScope())
        {
            retrievedAgent = await scope.MemoryEntries.FindAsync("read-test-agent");
        }

        // Assert
        retrievedAgent.ShouldNotBeNull();
        retrievedAgent.Key.ShouldBe("tester");
    }

    [Fact]
    public async Task WhenUsingWriteScope_ShouldPersistChanges()
    {
        // Arrange & Act
        string agentId;
        using (var scope = _scopeService.GetWriteScope())
        {
            var agent = new MemoryEntry
            {
                Id = "write-test-agent",
                Key = "implementer",
                Value = "write value"
            };

            scope.MemoryEntries.Add(agent);
            await scope.SaveChangesAsync();
            agentId = agent.Id;
            scope.Complete();
        }

        // Assert - Verify agent was saved
        using var assertScope = _scopeService.GetReadScope();
        var savedAgent = await assertScope.MemoryEntries.FindAsync(agentId);
        savedAgent.ShouldNotBeNull();
        savedAgent.Key.ShouldBe("implementer");
    }


    [Fact]
    public async Task WhenUsingTransactionScope_ShouldWorkWithInMemoryDatabase()
    {
        // This test verifies that TransactionScope doesn't throw exceptions with in-memory database

        // Act & Assert - Should not throw
        using (var scope = _scopeService.GetWriteScope())
        {
            var agent = new MemoryEntry
            {
                Id = "transaction-scope-test",
                Key = "tester",
                Value = "transaction value"
            };

            scope.MemoryEntries.Add(agent);
            await scope.SaveChangesAsync();
            scope.Complete();
        }

        // Verify the agent was saved
        using var verifyScope = _scopeService.GetReadScope();
        var savedAgent = await verifyScope.MemoryEntries.FindAsync("transaction-scope-test");
        savedAgent.ShouldNotBeNull();
    }
}
