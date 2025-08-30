using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Entities;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using AISwarm.DataLayer.Entities;

using AISwarm.Infrastructure.Eventing; // Added for InMemoryEventBus and event types

namespace AISwarm.Tests.McpTools;

public class ListMemoryMcpToolTests : ISystemUnderTest<ListMemoryMcpTool>
{
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;
    private readonly IEventBus<MemoryEventType, IMemoryLifecyclePayload> _memoryEventBus = new InMemoryEventBus<MemoryEventType, IMemoryLifecyclePayload>(); // Added

    protected ListMemoryMcpToolTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _timeService = new FakeTimeService();
        _scopeService = new DatabaseScopeService(new TestDbContextFactory(options));
        IMemoryService memoryService = new MemoryService(_scopeService, _timeService, _memoryEventBus); // Added _memoryEventBus
        SystemUnderTest = new ListMemoryMcpTool(memoryService);
    }

    public ListMemoryMcpTool SystemUnderTest
    {
        get;
    }

    public class SuccessTests : ListMemoryMcpToolTests
    {
        [Fact]
        public async Task WhenNoMemoryExists_ShouldReturnEmptyList()
        {
            // Arrange - Act
            var result = await SystemUnderTest.ListMemoryAsync("");

            // Assert
            result.Success.ShouldBeTrue();
            result.Entries.ShouldBeEmpty();
        }

        [Fact]
        public async Task WhenMemoryExistsInDefaultNamespace_ShouldReturnEntries()
        {
            // Arrange
            await AddMemoryEntry("key1", "value1", "");
            await AddMemoryEntry("key2", "value2", "");

            // Act
            var result = await SystemUnderTest.ListMemoryAsync("");

            // Assert
            result.Success.ShouldBeTrue();
            result.Entries.ShouldNotBeNull();
            result.Entries.Count.ShouldBe(2);
            result.Entries.ShouldContain(e => e.Key == "key1" && e.Value == "value1");
            result.Entries.ShouldContain(e => e.Key == "key2" && e.Value == "value2");
        }

        [Fact]
        public async Task WhenMemoryExistsInSpecificNamespace_ShouldReturnOnlyRelevantEntries()
        {
            // Arrange
            await AddMemoryEntry("keyA", "valueA", "namespace1");
            await AddMemoryEntry("keyB", "valueB", "namespace1");
            await AddMemoryEntry("keyC", "valueC", "namespace2");

            // Act
            var result = await SystemUnderTest.ListMemoryAsync("namespace1");

            // Assert
            result.Success.ShouldBeTrue();
            result.Entries.ShouldNotBeNull();
            result.Entries.Count.ShouldBe(2);
            result.Entries.ShouldContain(e => e.Key == "keyA" && e.Value == "valueA");
            result.Entries.ShouldContain(e => e.Key == "keyB" && e.Value == "valueB");
            result.Entries.ShouldNotContain(e => e.Key == "keyC");
        }

        [Fact]
        public async Task WhenMemoryExistsInMultipleNamespaces_ShouldReturnCorrectEntriesForEmptyNamespace()
        {
            // Arrange
            await AddMemoryEntry("key1", "value1", "");
            await AddMemoryEntry("key2", "value2", "namespace1");
            await AddMemoryEntry("key3", "value3", "");

            // Act
            var result = await SystemUnderTest.ListMemoryAsync("");

            // Assert
            result.Success.ShouldBeTrue();
            result.Entries.ShouldNotBeNull();
            result.Entries.Count.ShouldBe(2);
            result.Entries.ShouldContain(e => e.Key == "key1");
            result.Entries.ShouldContain(e => e.Key == "key3");
            result.Entries.ShouldNotContain(e => e.Key == "key2");
        }

        private async Task AddMemoryEntry(string key, string value, string @namespace)
        {
            using var scope = _scopeService.GetWriteScope();
            scope.MemoryEntries.Add(new MemoryEntry
            {
                Id = Guid.NewGuid().ToString(),
                Key = key,
                Value = value,
                Namespace = @namespace,
                Type = "text",
                CreatedAt = _timeService.UtcNow,
                LastUpdatedAt = _timeService.UtcNow
            });
            await scope.SaveChangesAsync();
        }
    }
}
