using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AISwarm.Tests.Database;

public class DatabaseScopeTests : IDisposable
{
    private readonly CoordinationDbContext _context;
    private readonly DatabaseScopeService _scopeService;

    public DatabaseScopeTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new CoordinationDbContext(options);
        _scopeService = new DatabaseScopeService(_context);
    }

    [Fact]
    public async Task WhenUsingReadScope_ShouldReturnData()
    {
        // Arrange
        var agent = new Agent
        {
            Id = "read-test-agent",
            PersonaId = "tester",
            AgentType = "tester",
            WorkingDirectory = "/read/test",
            Status = AgentStatus.Starting,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow
        };

        _context.Agents.Add(agent);
        await _context.SaveChangesAsync();

        // Act
        Agent? retrievedAgent;
        using (var scope = _scopeService.CreateReadScope())
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
        using (var scope = _scopeService.CreateWriteScope())
        {
            var agent = new Agent
            {
                Id = "write-test-agent",
                PersonaId = "implementer",
                AgentType = "implementer",
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

        // Assert
        var savedAgent = await _context.Agents.FindAsync(agentId);
        savedAgent.ShouldNotBeNull();
        savedAgent.PersonaId.ShouldBe("implementer");
    }


    [Fact]
    public async Task WhenUsingTransactionScope_ShouldWorkWithInMemoryDatabase()
    {
        // This test verifies that TransactionScope doesn't throw exceptions with in-memory database

        // Act & Assert - Should not throw
        using (var scope = _scopeService.CreateWriteScope())
        {
            var agent = new Agent
            {
                Id = "transaction-scope-test",
                PersonaId = "tester",
                AgentType = "tester",
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
        var savedAgent = await _context.Agents.FindAsync("transaction-scope-test");
        savedAgent.ShouldNotBeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
