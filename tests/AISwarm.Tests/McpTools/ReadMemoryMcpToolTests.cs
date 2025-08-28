using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Services;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AISwarm.Tests.McpTools;

public class ReadMemoryMcpToolTests : ISystemUnderTest<ReadMemoryMcpTool>
{
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;

    public ReadMemoryMcpTool SystemUnderTest { get; }

    protected ReadMemoryMcpToolTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _timeService = new FakeTimeService();
        _scopeService = new DatabaseScopeService(new TestDbContextFactory(options));
        IMemoryService memoryService = new MemoryService(_scopeService, _timeService);
        SystemUnderTest = new ReadMemoryMcpTool(memoryService);
    }

    public class ValidationTests : ReadMemoryMcpToolTests
    {
        [Fact]
        public async Task WhenKeyIsEmpty_ShouldReturnFailure()
        {
            // Arrange - Act
            var result = await SystemUnderTest.ReadMemoryAsync("", @namespace:"");

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldBe("key required");
        }

        [Fact]
        public async Task WhenMemoryDoesNotExist_ShouldReturnFailure()
        {
            // Arrange - Act
            var result = await SystemUnderTest.ReadMemoryAsync("nonexistent-key", @namespace:"");

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
            const string @namespace = "mynamespace";
            const string metadata = "{\"source\":\"test\",\"priority\":\"high\"}";
            var memoryEntry = new AISwarm.DataLayer.Entities.MemoryEntry
            {
                Id = Guid.NewGuid().ToString(),
                Namespace = @namespace,
                Key = key,
                Value = value,
                Type = "text",
                Metadata = metadata,
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
            result.Metadata.ShouldBe(memoryEntry.Metadata);
        }

        [Fact]
        public async Task WhenMemoryIsRead_ShouldUpdateAccessTimeAndCount()
        {
            // Arrange
            const string key = "access-test-key";
            const string value = "access-test-value";
            const string @namespace = "mynamespace2";

            var initialTime = _timeService.UtcNow;
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
                CreatedAt = initialTime,
                LastUpdatedAt = initialTime,
                AccessedAt = null,
                AccessCount = 0
            };

            // Create memory entry directly in database
            using (var scope = _scopeService.CreateWriteScope())
            {
                scope.MemoryEntries.Add(memoryEntry);
                await scope.SaveChangesAsync();
            }

            // Advance time to verify AccessedAt is updated
            var accessTime = initialTime.AddMinutes(5);
            _timeService.AdvanceTime(TimeSpan.FromMinutes(5));

            // Act
            var result = await SystemUnderTest.ReadMemoryAsync(key, @namespace);

            // Assert - Check database directly for access tracking updates
            using (var scope = _scopeService.CreateReadScope())
            {
                var updatedEntry = await scope.MemoryEntries
                    .FirstOrDefaultAsync(m => m.Key == key && m.Namespace == @namespace);

                updatedEntry.ShouldNotBeNull();
                updatedEntry.AccessedAt.ShouldBe(accessTime);
                updatedEntry.AccessCount.ShouldBe(1);

                // Verify other fields unchanged
                updatedEntry.CreatedAt.ShouldBe(initialTime);
                updatedEntry.LastUpdatedAt.ShouldBe(initialTime);
            }

            result.Success.ShouldBeTrue();
        }
    }
}
