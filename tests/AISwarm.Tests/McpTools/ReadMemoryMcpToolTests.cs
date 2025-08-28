using System;
using System.Threading.Tasks;
using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Server.Entities;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AISwarm.Tests.McpTools;

public class ReadMemoryMcpToolTests : IDisposable, ISystemUnderTest<ReadMemoryMcpTool>
{
    private readonly CoordinationDbContext _dbContext;
    private readonly IDatabaseScopeService _scopeService;
    private readonly IMemoryService _memoryService;
    private readonly FakeTimeService _timeService;

    public ReadMemoryMcpTool SystemUnderTest { get; }

    public ReadMemoryMcpToolTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CoordinationDbContext(options);
        _timeService = new FakeTimeService();
        _scopeService = new DatabaseScopeService(_dbContext);
        _memoryService = new MemoryService(_scopeService, _timeService);
        SystemUnderTest = new ReadMemoryMcpTool(_memoryService);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    public class ValidationTests : ReadMemoryMcpToolTests
    {
        [Fact]
        public async Task WhenKeyIsEmpty_ShouldReturnFailure()
        {
            // Arrange - Act
            var result = await SystemUnderTest.ReadMemory("", "default");

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldBe("key required");
        }
    }
}