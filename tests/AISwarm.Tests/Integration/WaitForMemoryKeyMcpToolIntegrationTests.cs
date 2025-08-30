using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

using AISwarm.Infrastructure.Entities; // Add this using directive
using AISwarm.Server.Entities; // Add this using directive

using AISwarm.Infrastructure.Eventing; // Added for InMemoryEventBus and event types

namespace AISwarm.Tests.Integration;

/// <summary>
///     Integration tests for the WaitForMemoryKeyMcpTool.
/// </summary>
public class WaitForMemoryKeyMcpToolIntegrationTests : IDisposable
{
    private readonly string _databasePath;
    private readonly IServiceProvider _serviceProvider;
    private readonly FakeTimeService _timeService;
    private readonly IEventBus<MemoryEventType, IMemoryLifecyclePayload> _memoryEventBus = new TestEventBus<MemoryEventType, IMemoryLifecyclePayload>(); // Added

    public WaitForMemoryKeyMcpToolIntegrationTests()
    {
        // Create a temporary SQLite database file
        _databasePath = Path.Combine(Path.GetTempPath(), $"test_waitformemory_{Guid.NewGuid()}.db");

        var services = new ServiceCollection();

        // Configure services similar to real application
        services.AddDbContextFactory<CoordinationDbContext>(options =>
            options.UseSqlite($"Data Source={_databasePath};Cache=Shared")
                .EnableSensitiveDataLogging()
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.AmbientTransactionWarning)));

        // Add Database services
        services.AddScoped<IDatabaseScopeService>(sp =>
            new DatabaseScopeService(sp.GetRequiredService<IDbContextFactory<CoordinationDbContext>>()));

        // Add Infrastructure services
        services.AddInfrastructureServices();

        // Override time service with fake for predictable testing
        _timeService = new FakeTimeService();
        services.AddSingleton<ITimeService>(_timeService);

        // Add MCP tools
        services.AddSingleton<WaitForMemoryKeyMcpTool>();
        services.AddSingleton<IEventBus<MemoryEventType, IMemoryLifecyclePayload>>(_memoryEventBus); // Register the TestEventBus instance

        _serviceProvider = services.BuildServiceProvider();

        // Initialize database and enable WAL mode
        using var context = _serviceProvider.GetRequiredService<IDbContextFactory<CoordinationDbContext>>()
            .CreateDbContext();
        context.Database.EnsureCreated();

        // Enable WAL mode for better concurrency
        context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }

    public void Dispose()
    {
        // Dispose service provider first to close all database connections
        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();
    }

    [Fact]
    public async Task WhenMemoryKeyDoesNotExist_ShouldTimeout()
    {
        // Arrange
        // This will cause a compilation error until WaitForMemoryKeyMcpTool is created
        var waitForMemoryTool = _serviceProvider.GetRequiredService<WaitForMemoryKeyMcpTool>();
        const string key = "non-existent-key";
        const string @namespace = "test-namespace";
        var timeout = TimeSpan.FromSeconds(1);

        // Act
        WaitForMemoryKeyResult result = await waitForMemoryTool.WaitForMemoryKeyAsync(key, @namespace, timeout); // Update return type

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Wait for memory key timed out."); // Updated expected error message
    }

    [Fact]
    public async Task WhenMemoryKeyIsCreated_ShouldReturnImmediately()
    {
        // Arrange
        var waitForMemoryTool = _serviceProvider.GetRequiredService<WaitForMemoryKeyMcpTool>();
        var memoryService = _serviceProvider.GetRequiredService<IMemoryService>(); // Need to inject IMemoryService
        const string key = "existing-key";
        const string value = "initial-value";
        const string @namespace = "test-namespace";

        // Create the memory entry before waiting
        await memoryService.SaveMemoryAsync(key, value, @namespace: @namespace);

        // Act
        // The current implementation of WaitForMemoryKeyAsync will still just delay and return failure
        // This will make the test fail, which is the RED state.
        var result = await waitForMemoryTool.WaitForMemoryKeyAsync(key, @namespace, TimeSpan.FromSeconds(1));

        // Assert
        result.Success.ShouldBeTrue();
        result.MemoryEntry.ShouldNotBeNull();
        result.MemoryEntry.Key.ShouldBe(key);
        result.MemoryEntry.Value.ShouldBe(value);
        result.MemoryEntry.Namespace.ShouldBe(@namespace);
    }

    [Fact]
    public async Task WhenMemoryKeyIsUpdated_ShouldReturnNewValue()
    {
        // Arrange
        var waitForMemoryTool = _serviceProvider.GetRequiredService<WaitForMemoryKeyMcpTool>();
        var memoryService = _serviceProvider.GetRequiredService<IMemoryService>();
        const string key = "update-test-key";
        const string initialValue = "initial-value";
        const string updatedValue = "updated-value";
        const string @namespace = "test-namespace";

        // Create the memory entry with an initial value
        await memoryService.SaveMemoryAsync(key, initialValue, @namespace: @namespace);

        // --- Event Subscription Setup (similar to ReportTaskCompletionMcpToolTests.cs) ---
        var eventReceivedTcs = new TaskCompletionSource<MemoryEntryDto>(); // To signal when the event is received
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // Overall test timeout
        var token = cts.Token;

        var readTask = Task.Run(async () =>
        {
            try
            {
                // This is where WaitForMemoryKeyAsync would subscribe to events
                // For now, we'll simulate it by directly subscribing to the TestEventBus
                await foreach (var envelope in (_memoryEventBus as TestEventBus<MemoryEventType, IMemoryLifecyclePayload>)!.Subscribe(
                    new EventFilter<MemoryEventType, IMemoryLifecyclePayload>
                    {
                        Predicate = e => e.Payload.MemoryEntry.Key == key && e.Payload.MemoryEntry.Namespace == @namespace
                    }, token))
                {
                    if (envelope.Type == MemoryEventType.Updated) // Only interested in updated events
                    {
                        eventReceivedTcs.TrySetResult(envelope.Payload.MemoryEntry);
                        break; // Event received, stop consuming
                    }
                }
            }
            catch (OperationCanceledException)
            {
                eventReceivedTcs.TrySetCanceled(token);
            }
            catch (Exception ex)
            {
                eventReceivedTcs.TrySetException(ex);
            }
        }, token);

        // --- Synchronization Delay ---
        await Task.Delay(30, token); // Give the subscription a moment to become active

        // --- Act: Publish the event ---
        await memoryService.SaveMemoryAsync(key, updatedValue, @namespace: @namespace);

        // --- Assert: Wait for the event to be received ---
        var receivedMemoryEntry = await eventReceivedTcs.Task; // Wait for the event to be signaled

        receivedMemoryEntry.ShouldNotBeNull();
        receivedMemoryEntry.Key.ShouldBe(key);
        receivedMemoryEntry.Value.ShouldBe(updatedValue); // Expect the updated value
        receivedMemoryEntry.Namespace.ShouldBe(@namespace);
    }

    [Fact]
    public async Task WhenNamespaceFiltering_ShouldOnlyReturnMatchingKeys()
    {
        // Arrange
        var waitForMemoryTool = _serviceProvider.GetRequiredService<WaitForMemoryKeyMcpTool>();
        var memoryService = _serviceProvider.GetRequiredService<IMemoryService>();
        const string key = "filtered-key";
        const string value = "filtered-value";
        const string targetNamespace = "target-namespace";
        const string otherNamespace = "other-namespace";

        // Create a memory entry in the target namespace
        await memoryService.SaveMemoryAsync(key, "initial-value", @namespace: targetNamespace);

        // --- Event Subscription Setup ---
        var eventReceivedTcs = new TaskCompletionSource<MemoryEntryDto>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // Overall test timeout
        var token = cts.Token;

        var readTask = Task.Run(async () =>
        {
            try
            {
                // Subscribe to events for the target namespace
                await foreach (var envelope in (_memoryEventBus as TestEventBus<MemoryEventType, IMemoryLifecyclePayload>)!.Subscribe(
                    new EventFilter<MemoryEventType, IMemoryLifecyclePayload>
                    {
                        Predicate = e => e.Payload.MemoryEntry.Key == key && e.Payload.MemoryEntry.Namespace == targetNamespace
                    }, token))
                {
                    if (envelope.Type == MemoryEventType.Updated) // Only interested in updated events
                    {
                        eventReceivedTcs.TrySetResult(envelope.Payload.MemoryEntry);
                        break; // Event received, stop consuming
                    }
                }
            }
            catch (OperationCanceledException)
            {
                eventReceivedTcs.TrySetCanceled(token);
            }
            catch (Exception ex)
            {
                eventReceivedTcs.TrySetException(ex);
            }
        }, token);

        // --- Synchronization Delay ---
        await Task.Delay(30, token); // Give the subscription a moment to become active

        // Act: Publish events in different namespaces
        await memoryService.SaveMemoryAsync(key, value, @namespace: otherNamespace); // This should NOT trigger the subscription
        await memoryService.SaveMemoryAsync(key, value, @namespace: targetNamespace); // This SHOULD trigger the subscription

        // --- Assert: Wait for the event to be received ---
        var receivedMemoryEntry = await eventReceivedTcs.Task; // Wait for the event to be signaled

        receivedMemoryEntry.ShouldNotBeNull();
        receivedMemoryEntry.Key.ShouldBe(key);
        receivedMemoryEntry.Value.ShouldBe(value);
        receivedMemoryEntry.Namespace.ShouldBe(targetNamespace); // Ensure it's from the target namespace
    }
}
