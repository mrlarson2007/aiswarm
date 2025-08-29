using System.Text.Json;
using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Eventing;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AISwarm.Tests.Services;

/// <summary>
///     Base class for DatabaseEventLoggerService tests providing common test infrastructure
/// </summary>
public abstract class DatabaseEventLoggerServiceTestBase : IDisposable, ISystemUnderTest<DatabaseEventLoggerService>
{
    protected readonly AgentNotificationService _agentNotificationService;
    protected readonly TestLogger _logger;
    protected readonly IDatabaseScopeService _scopeService;
    protected readonly WorkItemNotificationService _taskNotificationService;
    protected readonly FakeTimeService _timeService;
    protected readonly IDbContextFactory<CoordinationDbContext> DbContextFactory;

    protected DatabaseEventLoggerServiceTestBase()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        DbContextFactory = new TestDbContextFactory(options);
        _scopeService = new DatabaseScopeService(DbContextFactory);
        _timeService = new FakeTimeService();
        _logger = new TestLogger();

        // Create fresh event buses for each test instance to ensure isolation
        var taskEventBus = new InMemoryEventBus<TaskEventType, ITaskLifecyclePayload>();
        var agentEventBus = new InMemoryEventBus<AgentEventType, IAgentLifecyclePayload>();

        _taskNotificationService = new WorkItemNotificationService(taskEventBus);
        _agentNotificationService = new AgentNotificationService(agentEventBus);

        // Ensure the database is created and ready
        using var context = DbContextFactory.CreateDbContext();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        using var context = DbContextFactory.CreateDbContext();
        context.Database.EnsureDeleted();
    }

    public DatabaseEventLoggerService SystemUnderTest => new(
        _scopeService,
        _taskNotificationService,
        _agentNotificationService,
        _timeService,
        _logger);

    protected async Task<EventLog?> GetEventLogAsync()
    {
        using var context = DbContextFactory.CreateDbContext();
        return await context.EventLogs.OrderBy(e => e.Timestamp).FirstOrDefaultAsync();
    }

    protected async Task<EventLog?> GetEventLogAsync(string eventType)
    {
        using var context = DbContextFactory.CreateDbContext();
        return await context.EventLogs
            .Where(e => e.EventType == eventType)
            .OrderBy(e => e.Timestamp)
            .FirstOrDefaultAsync();
    }

    protected async Task<List<EventLog>> GetAllEventLogsAsync()
    {
        using var context = DbContextFactory.CreateDbContext();
        return await context.EventLogs.OrderBy(e => e.Timestamp).ToListAsync();
    }
}

[Collection("DatabaseEventLogger")]
public class TaskEventLoggingTests : DatabaseEventLoggerServiceTestBase
{
    [Fact]
    public async Task WhenMultipleTaskEventsPublished_ShouldLogAllToDatabase()
    {
        // Arrange
        var eventLogger = SystemUnderTest;

        // Test data for TaskCreated
        var taskId1 = "test-task-1";
        var agentId1 = "test-agent-1";
        var personaId = "implementer";

        // Test data for TaskCompleted
        var taskId2 = "test-task-2";
        var agentId2 = "test-agent-2";

        // Test data for TaskFailed
        var taskId3 = "test-task-3";
        var agentId3 = "test-agent-3";
        var reason = "Test failure reason";

        await eventLogger.StartAsync();

        // Act - Publish all three event types
        await _taskNotificationService.PublishTaskCreated(taskId1, agentId1, personaId);
        await _taskNotificationService.PublishTaskCompleted(taskId2, agentId2);
        await _taskNotificationService.PublishTaskFailed(taskId3, agentId3, reason);

        // Give async event processing time to complete
        await Task.Delay(100);

        // Assert - Check all events are in database
        var allLogs = await GetAllEventLogsAsync();
        if (allLogs.Count != 3)
            throw new Exception(
                $"Expected 3 events but found {allLogs.Count}. Event types: [{string.Join(", ", allLogs.Select(e => e.EventType))}]");
        allLogs.Count.ShouldBe(3);

        // Verify TaskCreated event
        var createdEvent = allLogs.FirstOrDefault(e => e.EventType == "TaskCreated");
        createdEvent.ShouldNotBeNull();
        createdEvent.EntityType.ShouldBe("Task");
        createdEvent.EntityId.ShouldBe(taskId1);
        createdEvent.Actor.ShouldBe(agentId1);
        createdEvent.Timestamp.ShouldBe(_timeService.UtcNow);
        createdEvent.Severity.ShouldBe("Information");
        createdEvent.Tags.ShouldNotBeNull();
        createdEvent.Tags.ShouldContain("persona:implementer");

        var createdPayload = JsonSerializer.Deserialize<TaskCreatedPayload>(createdEvent.Payload!);
        createdPayload.ShouldNotBeNull();
        createdPayload.TaskId.ShouldBe(taskId1);
        createdPayload.AgentId.ShouldBe(agentId1);
        createdPayload.PersonaId.ShouldBe(personaId);

        // Verify TaskCompleted event
        var completedEvent = allLogs.FirstOrDefault(e => e.EventType == "TaskCompleted");
        completedEvent.ShouldNotBeNull();
        completedEvent.EntityType.ShouldBe("Task");
        completedEvent.EntityId.ShouldBe(taskId2);
        completedEvent.Actor.ShouldBe(agentId2);
        completedEvent.Timestamp.ShouldBe(_timeService.UtcNow);
        completedEvent.Severity.ShouldBe("Information");

        var completedPayload = JsonSerializer.Deserialize<TaskCompletedPayload>(completedEvent.Payload!);
        completedPayload.ShouldNotBeNull();
        completedPayload.TaskId.ShouldBe(taskId2);
        completedPayload.AgentId.ShouldBe(agentId2);

        // Verify TaskFailed event
        var failedEvent = allLogs.FirstOrDefault(e => e.EventType == "TaskFailed");
        failedEvent.ShouldNotBeNull();
        failedEvent.EntityType.ShouldBe("Task");
        failedEvent.EntityId.ShouldBe(taskId3);
        failedEvent.Actor.ShouldBe(agentId3);
        failedEvent.Timestamp.ShouldBe(_timeService.UtcNow);
        failedEvent.Severity.ShouldBe("Warning");

        var failedPayload = JsonSerializer.Deserialize<TaskFailedPayload>(failedEvent.Payload!);
        failedPayload.ShouldNotBeNull();
        failedPayload.TaskId.ShouldBe(taskId3);
        failedPayload.AgentId.ShouldBe(agentId3);
        failedPayload.Reason.ShouldBe(reason);

        await eventLogger.StopAsync();
    }

    [Fact]
    public async Task WhenTaskClaimedEventPublished_ShouldLogToDatabase()
    {
        // Arrange
        var eventLogger = SystemUnderTest;
        var taskId = "test-task-claimed";
        var agentId = "test-agent-claimer";

        await eventLogger.StartAsync();

        // Act - Publish TaskClaimed event
        await _taskNotificationService.PublishTaskClaimed(taskId, agentId);

        // Give async event processing time to complete
        await Task.Delay(100);

        // Assert - Check event is in database
        var allLogs = await GetAllEventLogsAsync();
        allLogs.Count.ShouldBe(1);

        var claimedEvent = allLogs.FirstOrDefault(e => e.EventType == "TaskClaimed");
        claimedEvent.ShouldNotBeNull();
        claimedEvent.EntityType.ShouldBe("Task");
        claimedEvent.EntityId.ShouldBe(taskId);
        claimedEvent.Actor.ShouldBe(agentId);
        claimedEvent.Timestamp.ShouldBe(_timeService.UtcNow);
        claimedEvent.Severity.ShouldBe("Information");

        var claimedPayload = JsonSerializer.Deserialize<TaskClaimedPayload>(claimedEvent.Payload!);
        claimedPayload.ShouldNotBeNull();
        claimedPayload.TaskId.ShouldBe(taskId);
        claimedPayload.AgentId.ShouldBe(agentId);

        await eventLogger.StopAsync();
    }
}

[Collection("DatabaseEventLogger")]
public class ServiceLifecycleTests : DatabaseEventLoggerServiceTestBase
{
    [Fact]
    public async Task WhenStartingAndStoppingService_ShouldHandleGracefully()
    {
        // Arrange
        var eventLogger = SystemUnderTest;

        // Act & Assert - Should start without errors
        await eventLogger.StartAsync();
        _logger.Infos.ShouldContain(m => m.Contains("Database event logger service started successfully"));

        // Should stop without errors
        await eventLogger.StopAsync();
        _logger.Infos.ShouldContain(m => m.Contains("Database event logger service stopped"));
    }

    [Fact]
    public async Task WhenStoppingWithoutStarting_ShouldHandleGracefully()
    {
        // Arrange
        var eventLogger = SystemUnderTest;

        // Act & Assert - Should stop gracefully even if never started
        await eventLogger.StopAsync();

        // Should not have any error messages
        _logger.Errors.ShouldBeEmpty();
    }
}
