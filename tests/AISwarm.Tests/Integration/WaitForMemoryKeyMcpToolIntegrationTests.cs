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

namespace AISwarm.Tests.Integration;

/// <summary>
///     Integration tests for the WaitForMemoryKeyMcpTool.
/// </summary>
public class WaitForMemoryKeyMcpToolIntegrationTests : IDisposable
{
    private readonly string _databasePath;
    private readonly IServiceProvider _serviceProvider;
    private readonly FakeTimeService _timeService;

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

        // Add MCP tools (WaitForMemoryKeyMcpTool will be added later)
        services.AddSingleton(sp => new WaitForMemoryKeyMcpTool(sp.GetRequiredService<IMemoryService>()));

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
}
