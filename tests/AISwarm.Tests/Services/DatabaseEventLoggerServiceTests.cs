using System.Text.Json;
using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure.Eventing;
using AISwarm.Infrastructure.Services;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AISwarm.Tests.Services;

public class DatabaseEventLoggerServiceTests : IDisposable, ISystemUnderTest<DatabaseEventLoggerService>
{
    private readonly IDbContextFactory<CoordinationDbContext> _dbContextFactory;
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;
    private readonly TestLogger _logger;
    private readonly WorkItemNotificationService _taskNotificationService;
    private readonly AgentNotificationService _agentNotificationService;

    public DatabaseEventLoggerServiceTests()
    {
        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = new TestDbContextFactory(options);
        _scopeService = new DatabaseScopeService(_dbContextFactory);
        _timeService = new FakeTimeService();
        _logger = new TestLogger();

        // Set up notification services
        var taskEventBus = new InMemoryEventBus<TaskEventType, ITaskLifecyclePayload>();
        var agentEventBus = new InMemoryEventBus<AgentEventType, IAgentLifecyclePayload>();

        _taskNotificationService = new WorkItemNotificationService(taskEventBus);
        _agentNotificationService = new AgentNotificationService(agentEventBus);
    }

    public DatabaseEventLoggerService SystemUnderTest => new(
        _scopeService,
        _taskNotificationService,
        _agentNotificationService,
        _timeService,
        _logger);

    public void Dispose()
    {
        using var context = _dbContextFactory.CreateDbContext();
        context.Database.EnsureDeleted();
    }

    private async Task<EventLog?> GetEventLogAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.EventLogs.FirstOrDefaultAsync();
    }

    private async Task<List<EventLog>> GetAllEventLogsAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();
        return await context.EventLogs.OrderBy(e => e.Timestamp).ToListAsync();
    }

    public class TaskEventLoggingTests : DatabaseEventLoggerServiceTests
    {
        [Fact]
        public async Task WhenTaskCreatedEventPublished_ShouldLogToDatabase()
        {
            // Arrange
            var eventLogger = SystemUnderTest;
            var taskId = "test-task-1";
            var agentId = "test-agent-1";
            var personaId = "implementer";

            await eventLogger.StartAsync();

            // Act
            await _taskNotificationService.PublishTaskCreated(taskId, agentId, personaId);

            // Give the async subscription time to process
            await Task.Delay(1000); // Increased delay

            // Assert - Check database directly
            var eventLog = await GetEventLogAsync();
            eventLog.ShouldNotBeNull();
            eventLog.EventType.ShouldBe("TaskCreated");
            eventLog.EntityType.ShouldBe("Task");
            eventLog.EntityId.ShouldBe(taskId);
            eventLog.Actor.ShouldBe(agentId);
            eventLog.Timestamp.ShouldBe(_timeService.UtcNow);
            eventLog.Severity.ShouldBe("Information");
            eventLog.Tags.ShouldNotBeNull();
            eventLog.Tags.ShouldContain("persona:implementer");

            // Verify payload contains expected data
            var payload = JsonSerializer.Deserialize<TaskCreatedPayload>(eventLog.Payload!);
            payload.ShouldNotBeNull();
            payload.TaskId.ShouldBe(taskId);
            payload.AgentId.ShouldBe(agentId);
            
            // Debug: Check the actual payload content if PersonaId is null
            if (payload.PersonaId == null)
            {
                throw new Exception($"PersonaId is null. Full payload JSON: {eventLog.Payload}");
            }
            
            payload.PersonaId.ShouldBe(personaId);

            await eventLogger.StopAsync();
        }

        [Fact]
        public async Task WhenTaskCompletedEventPublished_ShouldLogToDatabase()
        {
            // Arrange
            var eventLogger = SystemUnderTest;
            var taskId = "test-task-2";
            var agentId = "test-agent-2";

            await eventLogger.StartAsync();

            // Act
            await _taskNotificationService.PublishTaskCompleted(taskId, agentId);

            // Give the async subscription time to process
            await Task.Delay(1000); // Increased delay

            // Assert - Check database directly
            var eventLog = await GetEventLogAsync();
            eventLog.ShouldNotBeNull();
            eventLog.EventType.ShouldBe("TaskCompleted");
            eventLog.EntityType.ShouldBe("Task");
            eventLog.EntityId.ShouldBe(taskId);
            eventLog.Actor.ShouldBe(agentId);
            eventLog.Timestamp.ShouldBe(_timeService.UtcNow);
            eventLog.Severity.ShouldBe("Information");

            // Verify payload contains expected data
            var payload = JsonSerializer.Deserialize<TaskCompletedPayload>(eventLog.Payload!);
            payload.ShouldNotBeNull();
            payload.TaskId.ShouldBe(taskId);
            payload.AgentId.ShouldBe(agentId);

            await eventLogger.StopAsync();
        }

        [Fact]
        public async Task WhenTaskFailedEventPublished_ShouldLogToDatabase()
        {
            // Arrange
            var eventLogger = SystemUnderTest;
            var taskId = "test-task-3";
            var agentId = "test-agent-3";
            var reason = "Test failure reason";

            await eventLogger.StartAsync();

            // Act
            await _taskNotificationService.PublishTaskFailed(taskId, agentId, reason);

            // Give the async subscription time to process
            await Task.Delay(1000); // Increased delay

            // Assert - Check database directly
            var eventLog = await GetEventLogAsync();
            if (eventLog == null)
            {
                // Debug: Check if any events were logged at all
                var allLogs = await GetAllEventLogsAsync();
                throw new Exception($"No TaskFailed event found. Total events in DB: {allLogs.Count}. Event types: [{string.Join(", ", allLogs.Select(e => e.EventType))}]");
            }
            
            eventLog.ShouldNotBeNull();
            eventLog.EventType.ShouldBe("TaskFailed");
            eventLog.EntityType.ShouldBe("Task");
            eventLog.EntityId.ShouldBe(taskId);
            eventLog.Actor.ShouldBe(agentId);
            eventLog.Timestamp.ShouldBe(_timeService.UtcNow);
            eventLog.Severity.ShouldBe("Warning");

            // Verify payload contains expected data
            var payload = JsonSerializer.Deserialize<TaskFailedPayload>(eventLog.Payload!);
            payload.ShouldNotBeNull();
            payload.TaskId.ShouldBe(taskId);
            payload.AgentId.ShouldBe(agentId);
            payload.Reason.ShouldBe(reason);

            await eventLogger.StopAsync();
        }
    }

    public class ServiceLifecycleTests : DatabaseEventLoggerServiceTests
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

    private class TestDbContextFactory : IDbContextFactory<CoordinationDbContext>
    {
        private readonly DbContextOptions<CoordinationDbContext> _options;

        public TestDbContextFactory(DbContextOptions<CoordinationDbContext> options)
        {
            _options = options;
        }

        public CoordinationDbContext CreateDbContext()
        {
            return new CoordinationDbContext(_options);
        }
    }
}
