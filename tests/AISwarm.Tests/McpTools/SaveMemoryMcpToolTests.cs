using System.Text.Json;
using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Services;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;

using AISwarm.Infrastructure.Eventing; // Added for InMemoryEventBus and event types

namespace AISwarm.Tests.McpTools;

public class SaveMemoryMcpToolTests : ISystemUnderTest<SaveMemoryMcpTool>
{
    // setup database context factory for testing, and Memory Service
    private readonly DatabaseScopeService _scopeService;
    private readonly ITimeService _timeService = new FakeTimeService();
    private readonly IEventBus<MemoryEventType, IMemoryLifecyclePayload> _memoryEventBus = new InMemoryEventBus<MemoryEventType, IMemoryLifecyclePayload>(); // Added

    protected SaveMemoryMcpToolTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _scopeService = new DatabaseScopeService(new TestDbContextFactory(options));
    }

    public SaveMemoryMcpTool SystemUnderTest => new(
        new MemoryService(_scopeService, _timeService, _memoryEventBus)); // Added _memoryEventBus

    public class ValidationTests : SaveMemoryMcpToolTests
    {
        [Fact]
        public async Task WhenSavingMemoryWithEmptyKey_ShouldReturnErrorMessage()
        {
            // Act
            var result = await SystemUnderTest.SaveMemory(
                string.Empty,
                "test-value");

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("key");
        }

        [Fact]
        public async Task WhenSavingMemoryWithEmptyValue_ShouldReturnErrorMessage()
        {
            var result = await SystemUnderTest.SaveMemory(
                "test-key",
                string.Empty);

            result.Success.ShouldBeFalse();
            result.ErrorMessage.ShouldNotBeNull();
            result.ErrorMessage.ShouldContain("value");
        }
    }

    private record TestMetadata(string Prop1, string Prop2);

    public class SuccessTests : SaveMemoryMcpToolTests
    {
        [Fact]
        public async Task WhenSavingMemoryWithValidInput_ShouldReturnSuccess()
        {
            var key = Guid.NewGuid().ToString();
            var value = Guid.NewGuid().ToString();
            var memoryNamespace = Guid.NewGuid().ToString();
            var testMetadata = new TestMetadata("test1", "test2");
            var expectedType = "text";
            var result = await SystemUnderTest.SaveMemory(
                key,
                value,
                expectedType,
                JsonSerializer.Serialize(testMetadata),
                memoryNamespace);

            result.Success.ShouldBeTrue();
            result.ErrorMessage.ShouldBeNull();
            result.Key.ShouldBe(key);
            result.Namespace.ShouldBe(memoryNamespace);

            using var readScope = _scopeService.GetReadScope();
            var memoryEntry = await readScope.MemoryEntries
                .FirstOrDefaultAsync(m => m.Key == key && m.Namespace == memoryNamespace, CancellationToken.None);
            memoryEntry.ShouldNotBeNull();
            memoryEntry.Key.ShouldBe(key);
            memoryEntry.Value.ShouldBe(value);
            memoryEntry.Namespace.ShouldBe(memoryNamespace);

            // Verify new fields are populated correctly
            memoryEntry.Type.ShouldBe(expectedType);
            memoryEntry.Metadata.ShouldBe(JsonSerializer.Serialize(testMetadata));
            memoryEntry.IsCompressed.ShouldBeFalse();
            memoryEntry.Size.ShouldBeGreaterThan(0);
            memoryEntry.CreatedAt.ShouldBe(_timeService.UtcNow);
            memoryEntry.LastUpdatedAt.ShouldBe(_timeService.UtcNow);
            memoryEntry.AccessedAt.ShouldBeNull();
            memoryEntry.AccessCount.ShouldBe(0);
        }
    }
}
