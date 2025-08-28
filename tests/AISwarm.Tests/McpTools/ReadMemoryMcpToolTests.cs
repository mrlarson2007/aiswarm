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
            var result = await SystemUnderTest.ReadMemoryAsync("", "");

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldBe("key required");
        }

        [Fact]
        public async Task WhenMemoryDoesNotExist_ShouldReturnFailure()
        {
            // Arrange - Act
            var result = await SystemUnderTest.ReadMemoryAsync("nonexistent-key", "");

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldBe("memory not found");
        }
    }

    public class SuccessTests : ReadMemoryMcpToolTests
    {
        [Fact]
        public async Task WhenMemoryExists_ShouldReturnMemoryValue()
        {
            // Arrange
            const string key = "test-key";
            const string value = "test-value";
            const string @namespace = "";
            var memoryEntry = new AISwarm.DataLayer.Entities.MemoryEntry
            {
                Id = Guid.NewGuid().ToString(),
                Namespace = @namespace,
                Key = key,
                Value = value,
                Type = "text",
                Metadata = null,
                IsCompressed = false,
                Size = System.Text.Encoding.UTF8.GetBytes(value).Length,
                CreatedAt = _timeService.UtcNow,
                LastUpdatedAt = _timeService.UtcNow,
                AccessedAt = null,
                AccessCount = 0
            };
            // Create memory entry directly in database to avoid dependency on SaveMemoryAsync
            using (var scope = _scopeService.CreateWriteScope())
            {

                scope.MemoryEntries.Add(memoryEntry);
                await scope.SaveChangesAsync();
            }

            // Act
            var result = await SystemUnderTest.ReadMemoryAsync(key, @namespace);

            // Assert
            result.Success.ShouldBeTrue();
            result.ErrorMessage.ShouldBeNull();
            result.Value.ShouldBe(memoryEntry.Value);
            result.Key.ShouldBe(memoryEntry.Key);
            result.Namespace.ShouldBe(memoryEntry.Namespace);
            result.Type.ShouldBe(memoryEntry.Type);
            result.Size.ShouldBe(memoryEntry.Size);
        }
    }
}
