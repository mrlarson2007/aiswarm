using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AISwarm.Tests.Integration;

/// <summary>
///     Integration tests for memory tools using SQLite database to catch entity configuration issues
///     that in-memory tests might miss
/// </summary>
public class MemoryToolsIntegrationTests : IDisposable
{
    private readonly string _databasePath;
    private readonly IServiceProvider _serviceProvider;

    public MemoryToolsIntegrationTests()
    {
        // Create a temporary SQLite database file
        _databasePath = Path.Combine(Path.GetTempPath(), $"test_memory_{Guid.NewGuid()}.db");

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
        services.AddSingleton<ITimeService, FakeTimeService>();

        // Add MCP tools
        services.AddSingleton<SaveMemoryMcpTool>();
        services.AddSingleton<ReadMemoryMcpTool>();

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
    public async Task WhenSavingAndReadingMemory_ShouldWorkEndToEnd()
    {
        // Arrange
        var saveMemoryTool = _serviceProvider.GetRequiredService<SaveMemoryMcpTool>();
        var readMemoryTool = _serviceProvider.GetRequiredService<ReadMemoryMcpTool>();

        const string key = "integration-test-key";
        const string value = "integration test value";
        const string @namespace = "test-namespace";
        const string type = "text";
        const string metadata = "{\"test\": true}";

        // Act - Save memory
        var saveResult = await saveMemoryTool.SaveMemory(
            key,
            value,
            type,
            metadata,
            @namespace);

        // Assert - Save succeeded
        saveResult.Success.ShouldBeTrue($"Save failed with error: {saveResult.ErrorMessage}");
        saveResult.Key.ShouldBe(key);
        saveResult.Namespace.ShouldBe(@namespace);

        // Act - Read memory back
        var readResult = await readMemoryTool.ReadMemoryAsync(key, @namespace);

        // Assert - Read succeeded and data matches
        readResult.Success.ShouldBeTrue($"Read failed with error: {readResult.ErrorMessage}");
        readResult.Key.ShouldBe(key);
        readResult.Value.ShouldBe(value);
        readResult.Namespace.ShouldBe(@namespace);
        readResult.Type.ShouldBe(type);
        readResult.Metadata.ShouldBe(metadata);
    }

    [Fact]
    public async Task WhenSavingMemoryWithDefaultNamespace_ShouldBeReadableWithEmptyNamespace()
    {
        // Arrange
        var saveMemoryTool = _serviceProvider.GetRequiredService<SaveMemoryMcpTool>();
        var readMemoryTool = _serviceProvider.GetRequiredService<ReadMemoryMcpTool>();

        const string key = "default-namespace-test";
        const string value = "test value";

        // Act - Save with null namespace (should default to empty string)
        var saveResult = await saveMemoryTool.SaveMemory(
            key,
            value,
            @namespace: null);

        saveResult.Success.ShouldBeTrue();

        // Act - Read with empty namespace
        var readResult = await readMemoryTool.ReadMemoryAsync(key);

        // Assert
        readResult.Success.ShouldBeTrue($"Read failed with error: {readResult.ErrorMessage}");
        readResult.Key.ShouldBe(key);
        readResult.Value.ShouldBe(value);
        readResult.Namespace.ShouldBe("");
    }

    [Fact]
    public async Task WhenReadingNonExistentMemory_ShouldReturnNotFound()
    {
        // Arrange
        var readMemoryTool = _serviceProvider.GetRequiredService<ReadMemoryMcpTool>();

        // Act
        var result = await readMemoryTool.ReadMemoryAsync("non-existent-key");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("memory not found");
    }

    [Fact]
    public async Task WhenSavingMultipleMemoryEntries_ShouldMaintainSeparateNamespaces()
    {
        // Arrange
        var saveMemoryTool = _serviceProvider.GetRequiredService<SaveMemoryMcpTool>();
        var readMemoryTool = _serviceProvider.GetRequiredService<ReadMemoryMcpTool>();

        const string key = "same-key";
        const string value1 = "value in namespace 1";
        const string value2 = "value in namespace 2";
        const string namespace1 = "ns1";
        const string namespace2 = "ns2";

        // Act - Save same key in different namespaces
        var saveResult1 = await saveMemoryTool.SaveMemory(key, value1, @namespace: namespace1);
        var saveResult2 = await saveMemoryTool.SaveMemory(key, value2, @namespace: namespace2);

        // Assert - Both saves succeeded
        saveResult1.Success.ShouldBeTrue();
        saveResult2.Success.ShouldBeTrue();

        // Act - Read from both namespaces
        var readResult1 = await readMemoryTool.ReadMemoryAsync(key, namespace1);
        var readResult2 = await readMemoryTool.ReadMemoryAsync(key, namespace2);

        // Assert - Values are correctly isolated by namespace
        readResult1.Success.ShouldBeTrue($"Read from namespace1 failed with error: {readResult1.ErrorMessage}");
        readResult1.Value.ShouldBe(value1);

        readResult2.Success.ShouldBeTrue($"Read from namespace2 failed with error: {readResult2.ErrorMessage}");
        readResult2.Value.ShouldBe(value2);
    }
}
