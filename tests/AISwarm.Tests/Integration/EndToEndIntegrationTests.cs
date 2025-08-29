using AISwarm.DataLayer;
using AISwarm.Infrastructure;
using AISwarm.Infrastructure.Services;
using AISwarm.Infrastructure.Eventing;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;

namespace AISwarm.Tests.Integration;

/// <summary>
/// End-to-end integration tests that validate the complete workflow from agent registration
/// through task assignment, retrieval, completion, and memory operations using real database
/// and mocked process launching.
/// </summary>
public class EndToEndIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDbContextFactory<CoordinationDbContext> _dbContextFactory;

    public EndToEndIntegrationTests()
    {
        // Setup in-memory database
        var services = new ServiceCollection();

        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase($"EndToEndTest_{Guid.NewGuid()}")
            .Options;

        services.AddSingleton<IDbContextFactory<CoordinationDbContext>>(new TestDbContextFactory(options));

        // Setup fake services for agent launching
        var fakeProcessLauncher = new FakeProcessLauncher();
        services.AddSingleton<IProcessLauncher>(fakeProcessLauncher);
        services.AddSingleton<IContextService, FakeContextService>();
        services.AddSingleton<IGitService, FakeGitService>();
        services.AddSingleton<IGeminiService, FakeGeminiService>();
        services.AddSingleton<IEnvironmentService, TestEnvironmentService>();

        // Setup logger
        var logger = new TestLogger();
        services.AddSingleton<IAppLogger>(logger);

        // Add real services
        services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();
        services.AddSingleton<ILocalAgentService, LocalAgentService>();
        services.AddSingleton<IMemoryService, MemoryService>();
        services.AddSingleton<FakeTimeService>();
        services.AddSingleton<ITimeService>(provider => provider.GetRequiredService<FakeTimeService>());

        // Add event bus services
        // Create shared event buses that will be used by both publishers and subscribers
        var taskEventBus = new InMemoryEventBus<TaskEventType, ITaskLifecyclePayload>();
        var agentEventBus = new InMemoryEventBus<AgentEventType, IAgentLifecyclePayload>();

        // Register the shared event bus instances
        services.AddSingleton<IEventBus<TaskEventType, ITaskLifecyclePayload>>(taskEventBus);
        services.AddSingleton<IEventBus<AgentEventType, IAgentLifecyclePayload>>(agentEventBus);
        services.AddSingleton<IWorkItemNotificationService, WorkItemNotificationService>();
        services.AddSingleton<IAgentNotificationService, AgentNotificationService>();

        // Add missing dependencies for LocalAgentService
        services.AddSingleton<IAgentStateService, AgentStateService>();
        services.AddSingleton<IProcessTerminationService, FakeProcessTerminationService>();
        services.AddSingleton<IEventLoggerService, DatabaseEventLoggerService>();

        // Add MCP Tools
        services.AddTransient<CreateTaskMcpTool>();
        services.AddTransient<GetNextTaskMcpTool>();
        services.AddTransient<ReportTaskCompletionMcpTool>();
        services.AddTransient<SaveMemoryMcpTool>();
        services.AddTransient<ReadMemoryMcpTool>();
        services.AddTransient<AgentManagementMcpTool>();


        _serviceProvider = services.BuildServiceProvider();
        _dbContextFactory = _serviceProvider.GetRequiredService<IDbContextFactory<CoordinationDbContext>>();

        // Ensure database is created
        using var context = _dbContextFactory.CreateDbContext();
        context.Database.EnsureCreated();

    }

    [Fact]
    public async Task WhenAssigningTaskToRegisteredAgent_ShouldReturnAssignedTask()
    {
        var eventLogger = _serviceProvider.GetRequiredService<IEventLoggerService>();
        var agentTool = _serviceProvider.GetRequiredService<AgentManagementMcpTool>();
        var createTaskTool = _serviceProvider.GetRequiredService<CreateTaskMcpTool>();
        var getNextTaskTool = _serviceProvider.GetRequiredService<GetNextTaskMcpTool>();
        var completionTool = _serviceProvider.GetRequiredService<ReportTaskCompletionMcpTool>();

        try
        {
            await eventLogger.StartAsync();

            // Give subscription time to be fully established before publishing events
            // This follows the pattern used in other notification service tests
            await Task.Delay(200);

            // launch agent
            var persona = "implementer";
            var launchResult = await agentTool.LaunchAgentAsync(persona, "Test implementer agent");
            launchResult.Success.ShouldBeTrue();
            var agentId = launchResult.AgentId!;
            agentId.ShouldNotBeNullOrEmpty();

            // create task assigned to specific agent
            var createTaskResult = await createTaskTool.CreateTaskAsync(null, persona, "Implement feature X");
            createTaskResult.Success.ShouldBeTrue();
            var taskId = createTaskResult.TaskId!;

            // Wait a moment for event processing
            await Task.Delay(100);

            // agent should get the task when calling get_next_task
            var getTaskResult = await getNextTaskTool.GetNextTaskAsync(agentId, 1000);
            getTaskResult.Success.ShouldBeTrue();
            getTaskResult.TaskId.ShouldBe(taskId);
            getTaskResult.Description.ShouldBe("Implement feature X");

            // Wait a moment after task is claimed
            await Task.Delay(200);

            // complete the task
            var completeTaskResult =
                await completionTool.ReportTaskCompletionAsync(taskId, "Feature X implemented successfully");
            completeTaskResult.IsSuccess.ShouldBeTrue();

            // Give async event processing time to complete
            await Task.Delay(500);

            // verify log events in table
            using var context = _dbContextFactory.CreateDbContext();
            var events = await context.EventLogs.ToListAsync();

            // Check task status for debugging
            var task = await context.Tasks.FindAsync(taskId);
            var taskStatus = task?.Status.ToString() ?? "NOT_FOUND";

            // Debug: log what events we actually have
            var eventTypes = string.Join(", ", events.Select(e => e.EventType));
            if (events.Count != 3)
            {
                throw new Exception($"Expected 3 events but found {events.Count}. Types: [{eventTypes}]. Task status: {taskStatus}");
            }

            events.Count.ShouldBe(3);
            events[0].EventType.ShouldBe("TaskCreated");
            events[1].EventType.ShouldBe("TaskClaimed");
            events[2].EventType.ShouldBe("TaskCompleted");
        }
        finally
        {
            await eventLogger.StopAsync();
        }
    }

    [Fact]
    public async Task WhenTaskIsAssignedToPersona_ShouldReturnTaskToAnyAgentWithSamePersona()
    {
        var agentTool = _serviceProvider.GetRequiredService<AgentManagementMcpTool>();
        var createTaskTool = _serviceProvider.GetRequiredService<CreateTaskMcpTool>();
        var getNextTaskTool = _serviceProvider.GetRequiredService<GetNextTaskMcpTool>();
        var completionTool = _serviceProvider.GetRequiredService<ReportTaskCompletionMcpTool>();

        // launch agent
        var persona = "implementer";
        var launchResult = await agentTool.LaunchAgentAsync(persona, "Test implementer agent");
        launchResult.Success.ShouldBeTrue();
        var agentId = launchResult.AgentId!;
        agentId.ShouldNotBeNullOrEmpty();

        // create task assigned to persona (not specific agent)
        var createTaskResult = await createTaskTool.CreateTaskAsync(null, persona, "Implement feature Y");
        createTaskResult.Success.ShouldBeTrue();
        var taskId = createTaskResult.TaskId!;

        //create another task assigned to different persona
        var createTaskResult2 = await createTaskTool.CreateTaskAsync(null, "reviewer", "Review feature Z");
        createTaskResult2.Success.ShouldBeTrue();
        var taskId2 = createTaskResult2.TaskId!;

        // agent should get the task when calling get_next_task
        var getTaskResult = await getNextTaskTool.GetNextTaskAsync(agentId, 1000);
        getTaskResult.Success.ShouldBeTrue();
        getTaskResult.TaskId.ShouldBe(taskId);
        getTaskResult.Description.ShouldBe("Implement feature Y");

        // complete the task
        await completionTool.ReportTaskCompletionAsync(taskId, "Feature Y implemented successfully");

        // verify get_next_task returns no task for different persona
        var getTaskResult2 = await getNextTaskTool.GetNextTaskAsync(agentId, 100);
        getTaskResult2.Success.ShouldBeTrue();
        getTaskResult2.TaskId.ShouldStartWith("system:requery:"); // No task available since the other task is for different persona

        // verify first task is completed, second task is still pending in database
        using var context = _dbContextFactory.CreateDbContext();
        var task = await context.Tasks.FindAsync(taskId);
        task.ShouldNotBeNull();
        task.Status.ShouldBe(TaskStatus.Completed);

        var task2 = await context.Tasks.FindAsync(taskId2);
        task2.ShouldNotBeNull();
        task2.Status.ShouldBe(TaskStatus.Pending); // Task is still pending since it was for different persona
    }

    [Fact]
    public async Task WhenAgentHasMultipleTasksAssigned_ShouldReturnSamePendingTaskUntilCompleted()
    {
        var agentTool = _serviceProvider.GetRequiredService<AgentManagementMcpTool>();
        var createTaskTool = _serviceProvider.GetRequiredService<CreateTaskMcpTool>();
        var getNextTaskTool = _serviceProvider.GetRequiredService<GetNextTaskMcpTool>();
        var completionTool = _serviceProvider.GetRequiredService<ReportTaskCompletionMcpTool>();

        // launch agent
        var persona = "implementer";
        var launchResult = await agentTool.LaunchAgentAsync(persona, "Test implementer agent");
        launchResult.Success.ShouldBeTrue();
        var agentId = launchResult.AgentId!;
        agentId.ShouldNotBeNullOrEmpty();

        // create multiple tasks assigned to specific agent
        var createTaskResult1 = await createTaskTool.CreateTaskAsync(agentId, persona, "Implement feature A");
        createTaskResult1.Success.ShouldBeTrue();
        var taskId1 = createTaskResult1.TaskId!;

        var createTaskResult2 = await createTaskTool.CreateTaskAsync(agentId, persona, "Implement feature B");
        createTaskResult2.Success.ShouldBeTrue();
        var taskId2 = createTaskResult2.TaskId!;

        // agent should get the first task when calling get_next_task
        var getTaskResult1 = await getNextTaskTool.GetNextTaskAsync(agentId, 1000);
        getTaskResult1.Success.ShouldBeTrue();
        getTaskResult1.TaskId.ShouldBe(taskId1);
        getTaskResult1.Description.ShouldBe("Implement feature A");

        // calling get_next_task again should return the same in-progress task
        var getTaskResult2 = await getNextTaskTool.GetNextTaskAsync(agentId, 1000);
        getTaskResult2.Success.ShouldBeTrue();
        getTaskResult2.TaskId.ShouldBe(taskId1); // Still the first task (now InProgress)
        getTaskResult2.Description.ShouldBe("Implement feature A");

        // complete the first task
        var completeTaskResult1 =
            await completionTool.ReportTaskCompletionAsync(taskId1, "Feature A implemented successfully");
        completeTaskResult1.IsSuccess.ShouldBeTrue();

        // now get_next_task should return the second task
        var getTaskResult3 = await getNextTaskTool.GetNextTaskAsync(agentId, 1000);
        getTaskResult3.Success.ShouldBeTrue();
        getTaskResult3.TaskId.ShouldBe(taskId2);
        getTaskResult3.Description.ShouldBe("Implement feature B");

        // complete the second task
        var completeTaskResult2 =
            await completionTool.ReportTaskCompletionAsync(taskId2, "Feature B implemented successfully");
        completeTaskResult2.IsSuccess.ShouldBeTrue();

        // now get_next_task should return no task
        var getTaskResult4 = await getNextTaskTool.GetNextTaskAsync(agentId, 100);
        getTaskResult4.Success.ShouldBeTrue();
        getTaskResult4.TaskId.ShouldStartWith("system:requery:"); // No task available

        // verify both tasks are marked as completed in database
        using var context = _dbContextFactory.CreateDbContext();
        var task1 = await context.Tasks.FindAsync(taskId1);
        task1.ShouldNotBeNull();
        task1.Status.ShouldBe(TaskStatus.Completed);

        var task2 = await context.Tasks.FindAsync(taskId2);
        task2.ShouldNotBeNull();
        task2.Status.ShouldBe(TaskStatus.Completed);
    }

    [Fact]
    public async Task WhenMemoryIsCreated_ShouldRetrieveIt()
    {
        var agentTool = _serviceProvider.GetRequiredService<AgentManagementMcpTool>();
        var saveMemoryTool = _serviceProvider.GetRequiredService<SaveMemoryMcpTool>();
        var readMemoryTool = _serviceProvider.GetRequiredService<ReadMemoryMcpTool>();

        // launch agent
        var persona = "implementer";
        var launchResult = await agentTool.LaunchAgentAsync(persona, "Test implementer agent");
        launchResult.Success.ShouldBeTrue();
        var agentId = launchResult.AgentId!;
        agentId.ShouldNotBeNullOrEmpty();

        // save memory
        var saveResult = await saveMemoryTool.SaveMemory("key1", "value1");
        saveResult.Success.ShouldBeTrue();

        var readResult = await readMemoryTool.ReadMemoryAsync("key1");
        readResult.Success.ShouldBeTrue();
        readResult.Value.ShouldBe("value1");
    }

    public void Dispose()
    {
        // cleanup test database
        using var context = _dbContextFactory.CreateDbContext();
        context.Database.EnsureDeleted();
        _serviceProvider.Dispose();
    }
}
