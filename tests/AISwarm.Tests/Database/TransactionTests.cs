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
        var agent = new Agent
        {
            Id = "read-test-agent",
            PersonaId = "tester",
            WorkingDirectory = "/read/test",
            Status = AgentStatus.Starting,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow
        };

        // Arrange - Setup test data
        using (var setupScope = _scopeService.GetWriteScope())
        {
            setupScope.Agents.Add(agent);
            await setupScope.SaveChangesAsync();
            setupScope.Complete();
        }

        // Act
        Agent? retrievedAgent;
        using (var scope = _scopeService.GetReadScope())
        {
            retrievedAgent = await scope.Agents.FindAsync("read-test-agent");
        }

        // Assert
        retrievedAgent.ShouldNotBeNull();
        retrievedAgent.PersonaId.ShouldBe("tester");
    }

    [Fact]
    public async Task WhenUsingWriteScope_ShouldPersistChanges()
    {
        // Arrange & Act
        string agentId;
        using (var scope = _scopeService.GetWriteScope())
        {
            var agent = new Agent
            {
                Id = "write-test-agent",
                PersonaId = "implementer",
                WorkingDirectory = "/write/test",
                Status = AgentStatus.Starting,
                RegisteredAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow
            };

            scope.Agents.Add(agent);
            await scope.SaveChangesAsync();
            agentId = agent.Id;
            scope.Complete();
        }

        // Assert - Verify agent was saved
        using var assertScope = _scopeService.GetReadScope();
        var savedAgent = await assertScope.Agents.FindAsync(agentId);
        savedAgent.ShouldNotBeNull();
        savedAgent.PersonaId.ShouldBe("implementer");
    }


    [Fact]
    public async Task WhenUsingTransactionScope_ShouldWorkWithInMemoryDatabase()
    {
        // This test verifies that TransactionScope doesn't throw exceptions with in-memory database

        // Act & Assert - Should not throw
        using (var scope = _scopeService.GetWriteScope())
        {
            var agent = new Agent
            {
                Id = "transaction-scope-test",
                PersonaId = "tester",
                WorkingDirectory = "/scope/test",
                Status = AgentStatus.Starting,
                RegisteredAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow
            };

            scope.Agents.Add(agent);
            await scope.SaveChangesAsync();
            scope.Complete();
        }

        // Verify the agent was saved
        using var verifyScope = _scopeService.GetReadScope();
        var savedAgent = await verifyScope.Agents.FindAsync("transaction-scope-test");
        savedAgent.ShouldNotBeNull();
    }
}
