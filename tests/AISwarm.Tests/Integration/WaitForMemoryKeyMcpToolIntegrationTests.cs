using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shouldly;
using AISwarm.Server.Entities; // Add this using directive

using AISwarm.Infrastructure.Eventing;

// Added for InMemoryEventBus and event types

namespace AISwarm.Tests.Integration;

/// <summary>
///     Integration tests for the WaitForMemoryKeyMcpTool.
/// </summary>
public class WaitForMemoryKeyMcpToolIntegrationTests : ISystemUnderTest<WaitForMemoryKeyMcpTool>
{
    private readonly MemoryService _memoryService;
    private readonly IEventBus<MemoryEventType, IMemoryLifecyclePayload> _memoryEventBus;

    public WaitForMemoryKeyMcpTool SystemUnderTest => new WaitForMemoryKeyMcpTool(
         _memoryService,
        _memoryEventBus); // Pass the event bus to the system under test

    public WaitForMemoryKeyMcpToolIntegrationTests()
    {
        // do not use service provider, just build object graph manually here
        // to have full control over the services and their lifetimes

        // Initialize the event bus first so both services can use the same instance
        _memoryEventBus = new InMemoryEventBus<MemoryEventType, IMemoryLifecyclePayload>();

        var timeService = new FakeTimeService();
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var databaseScopeService = new DatabaseScopeService(new TestDbContextFactory(options));
        _memoryService = new MemoryService(
            databaseScopeService,
            timeService,
            _memoryEventBus); // Pass the event bus to MemoryService
    }

    [Fact]
    public async Task WhenMemoryKeyDoesNotExist_ShouldTimeout()
    {
        // Arrange
        // This will cause a compilation error until WaitForMemoryKeyMcpTool is created
        const string key = "non-existent-key";
        const string @namespace = "test-namespace";

        // Act
        WaitForMemoryKeyResult result = await SystemUnderTest.WaitForMemoryKeyUpdateAsync(key, @namespace, 1000); // Update return type

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.MemoryEntry.ShouldBeNull();
        result.ErrorMessage.ShouldNotBeNullOrEmpty();
        result.ErrorMessage.ShouldContain("Wait for memory key timed out."); // Updated expected error message
    }

    [Fact]
    public async Task WhenMemoryKeyIsCreated_ShouldReturnImmediately()
    {
        // Arrange
        const string key = "existing-key";
        const string value = "initial-value";
        const string @namespace = "test-namespace";

        // Create the memory entry before waiting
        await _memoryService.SaveMemoryAsync(key, value, @namespace: @namespace);

        // Act
        // The current implementation of WaitForMemoryKeyAsync will still just delay and return failure
        // This will make the test fail, which is the RED state.
        var result = await SystemUnderTest.WaitForMemoryKeyCreationAsync(key, @namespace, 1000);

        // Assert
        result.Success.ShouldBeTrue();
        result.MemoryEntry.ShouldNotBeNull();
        result.MemoryEntry.Key.ShouldBe(key);
        result.MemoryEntry.Value.ShouldBe(value);
        result.MemoryEntry.Namespace.ShouldBe(@namespace);
    }

    [Fact]
    public async Task WhenMemoryKeyIsCreatedAfterWaiting_ShouldReturnNewValue()
    {
        // Arrange
        const string key = "delayed-key";
        const string value = "delayed-value";
        const string @namespace = "test-namespace";

        // --- Event Subscription Setup (similar to ReportTaskCompletionMcpToolTests.cs) ---
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Overall test timeout
        var token = cts.Token;

        var readTask = Task.Run(async () =>
            await SystemUnderTest.WaitForMemoryKeyCreationAsync(key, @namespace, 10000));
        await Task.Delay(30, token); // Give the subscription a moment to become active

        // --- Act: Publish the event ---
        await _memoryService.SaveMemoryAsync(key, value, @namespace: @namespace);

        // --- Assert: Wait for the event to be received ---
        var receivedMemoryEntry = await readTask; // Wait for the event to be signaled

        receivedMemoryEntry.ShouldNotBeNull();
        receivedMemoryEntry.Success.ShouldBeTrue();
        receivedMemoryEntry.MemoryEntry.ShouldNotBeNull();
        receivedMemoryEntry.MemoryEntry.Key.ShouldBe(key);
        receivedMemoryEntry.MemoryEntry.Value.ShouldBe(value);
        receivedMemoryEntry.MemoryEntry.Namespace.ShouldBe(@namespace);
    }

    [Fact]
    public async Task WhenMemoryKeyIsUpdated_ShouldReturnNewValue()
    {
        // Arrange
        const string key = "update-test-key";
        const string initialValue = "initial-value";
        const string updatedValue = "updated-value";
        const string @namespace = "test-namespace";

        // Create the memory entry with an initial value
        await _memoryService.SaveMemoryAsync(key, initialValue, @namespace: @namespace);

        // --- Event Subscription Setup (similar to ReportTaskCompletionMcpToolTests.cs) ---
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // Overall test timeout
        var token = cts.Token;

        var readTask = Task.Run(async () =>
            await SystemUnderTest.WaitForMemoryKeyUpdateAsync(key, @namespace, 10000));

        // --- Synchronization Delay ---
        await Task.Delay(30, token); // Give the subscription a moment to become active

        // --- Act: Publish the event ---
        await _memoryService.SaveMemoryAsync(key, updatedValue, @namespace: @namespace);

        // --- Assert: Wait for the event to be received ---
        var receivedMemoryEntry = await readTask; // Wait for the event to be signaled

        receivedMemoryEntry.ShouldNotBeNull();
        receivedMemoryEntry.Success.ShouldBeTrue();
        receivedMemoryEntry.MemoryEntry.ShouldNotBeNull();
        receivedMemoryEntry.MemoryEntry.Key.ShouldBe(key);
        receivedMemoryEntry.MemoryEntry.Value.ShouldBe(updatedValue); // Expect the updated value
        receivedMemoryEntry.MemoryEntry.Namespace.ShouldBe(@namespace);
    }

    [Fact]
    public async Task WhenNamespaceFiltering_ShouldOnlyReturnMatchingKeys()
    {
        // Arrange
        const string key = "filtered-key";
        const string value = "filtered-value";
        const string secondValue = "other-value";
        const string targetNamespace = "target-namespace";
        const string otherNamespace = "other-namespace";

        // Create a memory entry in the target namespace
        await _memoryService.SaveMemoryAsync(key, "initial-value", @namespace: targetNamespace);

        // --- Event Subscription Setup ---
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Overall test timeout[]

        var readTask = Task.Run(async () =>
            await SystemUnderTest.WaitForMemoryKeyUpdateAsync(key, targetNamespace, 10000), cts.Token);
        await Task.Delay(30, cts.Token); // Give the subscription a moment to become active

        // Act: Publish events in different namespaces
        await _memoryService.SaveMemoryAsync(key, secondValue, @namespace: otherNamespace); // This should NOT trigger the subscription
        await _memoryService.SaveMemoryAsync(key, value, @namespace: targetNamespace); // This SHOULD trigger the subscription

        // --- Assert: Wait for the event to be received ---
        var receivedMemoryEntry = await readTask; // Wait for the event to be signaled

        receivedMemoryEntry.ShouldNotBeNull();
        receivedMemoryEntry.Success.ShouldBeTrue();
        receivedMemoryEntry.MemoryEntry.ShouldNotBeNull();
        receivedMemoryEntry.MemoryEntry.Key.ShouldBe(key);
        receivedMemoryEntry.MemoryEntry.Value.ShouldBe(value);
        receivedMemoryEntry.MemoryEntry.Namespace.ShouldBe(targetNamespace); // Ensure it's from the target namespace
    }
}
