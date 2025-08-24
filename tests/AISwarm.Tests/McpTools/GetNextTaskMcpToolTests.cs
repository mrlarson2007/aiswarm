using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AISwarm.Tests.McpTools;

public class GetNextTaskMcpToolTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;

    public GetNextTaskMcpToolTests()
    {
        var services = new ServiceCollection();

        // Add database services
        services.AddDbContext<CoordinationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
        _timeService = new FakeTimeService();
        services.AddSingleton<ITimeService>(_timeService);
        services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();

        // Add MCP tools
        services.AddSingleton<GetNextTaskMcpTool>();

        _serviceProvider = services.BuildServiceProvider();
        _scopeService = _serviceProvider.GetRequiredService<IDatabaseScopeService>();
    }

    private GetNextTaskMcpTool SystemUnderTest
    {
        get
        {
            var tool = _serviceProvider.GetRequiredService<GetNextTaskMcpTool>();
            tool.Configuration = new GetNextTaskConfiguration();
            return tool;
        }
    }

    [Fact]
    public async Task WhenNonExistentAgent_ShouldReturnFailureResult()
    {
        // Arrange
        var nonExistentAgentId = "non-existent-agent";

        // Act
        var result = await SystemUnderTest.GetNextTaskAsync(nonExistentAgentId);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Agent not found");
        result.ErrorMessage.ShouldContain(nonExistentAgentId);
    }

    [Fact]
    public async Task WhenAgentHasNoTasks_ShouldReturnNoTasksWithReinforcingPrompt()
    {
        // Arrange
        var agentId = "agent-no-tasks";

        // Create a running agent with no tasks
        await CreateRunningAgentAsync(agentId);

        // Act
        var result = await SystemUnderTest.GetNextTaskAsync(agentId);

        // Assert: return a default synthetic task instructing the agent to re-query
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldNotBeNull();
        result.TaskId.ShouldStartWith("system:");
        result.Persona.ShouldNotBeNull();
        result.Description.ShouldNotBeNull();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("No tasks available");
        result.Message.ShouldContain("call this tool again");
    }

    [Fact]
    public async Task WhenAgentHasPendingTask_ShouldReturnTaskWithReinforcingPrompt()
    {
        // Arrange
        var agentId = "agent-123";
        var expectedPersona = "You are a code reviewer. Review code for quality and security.";
        var expectedDescription = "Review the authentication module for security vulnerabilities";

        // Create a running agent first
        await CreateRunningAgentAsync(agentId);

        // Create a pending task for the agent
        var taskId = await CreatePendingTaskAsync(agentId, expectedPersona, expectedDescription);

        // Act
        var result = await SystemUnderTest.GetNextTaskAsync(agentId);

        // Assert
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldBe(taskId);
        result.Persona.ShouldBe(expectedPersona);
        result.Description.ShouldBe(expectedDescription);
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("call this tool again");
        result.Message.ShouldContain("get the next task");
    }

    [Fact]
    public async Task WhenAgentHasNoTasksAndPollingTimeoutExpires_ShouldReturnNoTasksAfterWaiting()
    {
        // Arrange
        var agentId = "agent-polling-timeout";

        // Create a running agent with no tasks
        await CreateRunningAgentAsync(agentId);

        // Configure very short polling timeout and interval for fast test
        var configuration = new GetNextTaskConfiguration
        {
            TimeToWaitForTask = TimeSpan.FromMilliseconds(50),  // Very short timeout
            PollingInterval = TimeSpan.FromMilliseconds(10)     // Very short polling interval
        };

        // Act
        var startTime = DateTime.UtcNow;
        var result = await SystemUnderTest.GetNextTaskAsync(agentId, configuration);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert - returns a synthetic default task instructing a re-query
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldNotBeNull();
        result.TaskId!.ShouldStartWith("system:");
        result.Persona.ShouldNotBeNull();
        result.Description.ShouldNotBeNull();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("No tasks available");
        result.Message.ShouldContain("call this tool again");

        // Should have waited at least the configured timeout duration
        elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(40));
        // But not too much longer (allowing for test execution overhead)
        elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public async Task WhenTaskArrivesWhilePolling_ShouldReturnTaskImmediately()
    {
        // Arrange
        var agentId = "agent-polling-success";
        var expectedPersona = "You are a code reviewer. Review code for quality and security.";
        var expectedDescription = "Review the authentication module for security vulnerabilities";

        // Create a running agent with no initial tasks
        await CreateRunningAgentAsync(agentId);

        // Configure longer polling timeout so we can simulate task arriving
        var configuration = new GetNextTaskConfiguration
        {
            TimeToWaitForTask = TimeSpan.FromSeconds(1),     // 1 second timeout
            PollingInterval = TimeSpan.FromMilliseconds(100) // Check every 100ms
        };

        // Act - Start polling in background and add task after delay
        var pollingTask = SystemUnderTest.GetNextTaskAsync(agentId, configuration);

        // Advance time to simulate task arriving during polling
        _timeService.AdvanceTime(TimeSpan.FromMilliseconds(200));
        var taskId = await CreatePendingTaskUsingNewScopeAsync(agentId, expectedPersona, expectedDescription);

        var startTime = DateTime.UtcNow;
        var result = await pollingTask;
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        ShouldBeBooleanExtensions.ShouldBeTrue(result.Success);
        ShouldBeStringTestExtensions.ShouldBe(result.TaskId, taskId);
        ShouldBeStringTestExtensions.ShouldBe(result.Persona, expectedPersona);
        ShouldBeStringTestExtensions.ShouldBe(result.Description, expectedDescription);
        ShouldBeNullExtensions.ShouldNotBeNull<string>(result.Message);
        ShouldBeStringTestExtensions.ShouldContain(result.Message, "call this tool again");
        ShouldBeStringTestExtensions.ShouldContain(result.Message, "get the next task");

        // Should have returned quickly once task was available, not waited full timeout
        elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(900));
    }

    private async Task CreateRunningAgentAsync(string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        var agent = new Agent
        {
            Id = agentId,
            PersonaId = "test-persona",
            AgentType = "test",
            WorkingDirectory = "/test",
            Status = AgentStatus.Running,
            LastHeartbeat = _serviceProvider.GetRequiredService<ITimeService>().UtcNow
        };
        scope.Agents.Add(agent);
        await scope.SaveChangesAsync();
        scope.Complete();
    }

    private async Task<string> CreatePendingTaskAsync(string agentId, string persona, string description)
    {
        using var scope = _scopeService.CreateWriteScope();
        var taskId = Guid.NewGuid().ToString();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = DataLayer.Entities.TaskStatus.Pending,
            Persona = persona,
            Description = description,
            CreatedAt = _serviceProvider.GetRequiredService<ITimeService>().UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return taskId;
    }

    private async Task<string> CreatePendingTaskUsingNewScopeAsync(string agentId, string persona, string description)
    {
        // Create a completely new scope service to simulate external process
        var newScopeService = _serviceProvider.GetRequiredService<IDatabaseScopeService>();
        using var scope = newScopeService.CreateWriteScope();
        var taskId = Guid.NewGuid().ToString();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = DataLayer.Entities.TaskStatus.Pending,
            Persona = persona,
            Description = description,
            CreatedAt = _serviceProvider.GetRequiredService<ITimeService>().UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return taskId;
    }

    [Fact]
    public async Task WhenUnassignedTaskExists_ShouldClaimTaskAndReturnIt()
    {
        // Arrange
        var agentId = "agent-claimer";
        var expectedPersona = "You are a planner. Plan and coordinate development tasks.";
        var expectedDescription = "Plan the authentication feature implementation";

        // Create a running agent
        await CreateRunningAgentAsync(agentId);

        // Create an unassigned task (AgentId is null/empty)
        var unassignedTaskId = await CreateUnassignedTaskAsync(expectedPersona, expectedDescription);

        // Act
        var result = await SystemUnderTest.GetNextTaskAsync(agentId);

        // Assert
        ShouldBeBooleanExtensions.ShouldBeTrue(result.Success);
        ShouldBeStringTestExtensions.ShouldBe(result.TaskId, unassignedTaskId);
        ShouldBeStringTestExtensions.ShouldBe(result.Persona, expectedPersona);
        ShouldBeStringTestExtensions.ShouldBe(result.Description, expectedDescription);
        ShouldBeNullExtensions.ShouldNotBeNull<string>(result.Message);
        ShouldBeStringTestExtensions.ShouldContain(result.Message, "call this tool again");

        // Verify the task is now assigned to the requesting agent
        using var scope = _scopeService.CreateReadScope();
        var claimedTask = await scope.Tasks.FindAsync(unassignedTaskId);
        claimedTask.ShouldNotBeNull();
        claimedTask.AgentId.ShouldBe(agentId);
    }

    private async Task<string> CreateUnassignedTaskAsync(string persona, string description)
    {
        using var scope = _scopeService.CreateWriteScope();
        var taskId = Guid.NewGuid().ToString();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = string.Empty, // Unassigned task
            Status = DataLayer.Entities.TaskStatus.Pending,
            Persona = persona,
            Description = description,
            CreatedAt = _serviceProvider.GetRequiredService<ITimeService>().UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return taskId;
    }

    [Fact]
    public async Task WhenAgentHasAssignedAndUnassignedTasks_ShouldReturnAssignedTaskFirst()
    {
        // Arrange
        var agentId = "agent-priority-test";
        var assignedPersona = "You are a reviewer. Review code for quality.";
        var assignedDescription = "Review the authentication module";
        var unassignedPersona = "You are a planner. Plan development tasks.";
        var unassignedDescription = "Plan the next feature implementation";

        // Create a running agent
        await CreateRunningAgentAsync(agentId);

        // Create an unassigned task first (older timestamp)
        var unassignedTaskId = await CreateUnassignedTaskAsync(unassignedPersona, unassignedDescription);

        // Advance time to ensure different timestamps
        _timeService.AdvanceTime(TimeSpan.FromMilliseconds(100));

        // Create an assigned task second (newer timestamp)
        var assignedTaskId = await CreatePendingTaskAsync(agentId, assignedPersona, assignedDescription);

        // Act
        var result = await SystemUnderTest.GetNextTaskAsync(agentId);

        // Assert - Should get the assigned task, not the unassigned one (even though unassigned is older)
        ShouldBeBooleanExtensions.ShouldBeTrue(result.Success);
        ShouldBeStringTestExtensions.ShouldBe(result.TaskId, assignedTaskId);  // Assigned task should have priority
        ShouldBeStringTestExtensions.ShouldBe(result.Persona, assignedPersona);
        ShouldBeStringTestExtensions.ShouldBe(result.Description, assignedDescription);

        // Verify the unassigned task is still unassigned
        using var scope = _scopeService.CreateReadScope();
        var unassignedTask = await scope.Tasks.FindAsync(unassignedTaskId);
        unassignedTask.ShouldNotBeNull();
        unassignedTask.AgentId.ShouldBe(string.Empty); // Still unassigned
    }

    [Fact]
    public async Task WhenTaskAlreadyClaimed_ShouldReturnNoTasksAvailable()
    {
        // Arrange
        var agentId = "agent-race-condition";
        var otherAgentId = "other-agent";
        var persona = "You are a planner. Plan development tasks.";
        var description = "Plan the authentication feature";

        // Create running agents
        await CreateRunningAgentAsync(agentId);
        await CreateRunningAgentAsync(otherAgentId);

        // Create an unassigned task
        var taskId = await CreateUnassignedTaskAsync(persona, description);

        // Simulate another agent claiming the task first
        using (var scope = _scopeService.CreateWriteScope())
        {
            var task = await scope.Tasks.FindAsync(taskId);
            task!.AgentId = otherAgentId; // Other agent claims it
            await scope.SaveChangesAsync();
            scope.Complete();
        }

        // Act - Try to get task after it's already been claimed
        var result = await SystemUnderTest.GetNextTaskAsync(agentId);

        // Assert - Should return a synthetic default task instructing a re-query
        ShouldBeBooleanExtensions.ShouldBeTrue(result.Success);
        result.TaskId.ShouldNotBeNull();
        result.TaskId!.ShouldStartWith("system:");
        result.Persona.ShouldNotBeNull();
        result.Description.ShouldNotBeNull();
        ShouldBeNullExtensions.ShouldNotBeNull<string>(result.Message);
        ShouldBeStringTestExtensions.ShouldContain(result.Message, "No tasks available");
        ShouldBeStringTestExtensions.ShouldContain(result.Message, "call this tool again");

        // Verify the task is still assigned to the other agent
        using var readScope = _scopeService.CreateReadScope();
        var claimedTask = await readScope.Tasks.FindAsync(taskId);
        claimedTask.ShouldNotBeNull();
        claimedTask.AgentId.ShouldBe(otherAgentId);
    }

    [Fact]
    public async Task WhenMultipleUnassignedTasksWithDifferentPriorities_ShouldReturnHighestPriorityFirst()
    {
        // Arrange
        var agentId = "agent-priority-test";
        var lowPriorityPersona = "You are a reviewer. Review code for quality.";
        var lowPriorityDescription = "Review documentation for typos";
        var highPriorityPersona = "You are a security reviewer. Review for security issues.";
        var highPriorityDescription = "Critical security review needed immediately";

        // Create a running agent
        await CreateRunningAgentAsync(agentId);

        // Create low priority task first (older timestamp)
        var lowPriorityTaskId = await CreateUnassignedTaskWithPriorityAsync(lowPriorityPersona, lowPriorityDescription, TaskPriority.Low);

        // Advance time to ensure different timestamps
        _timeService.AdvanceTime(TimeSpan.FromMilliseconds(100));

        // Create high priority task second (newer timestamp but higher priority)
        var highPriorityTaskId = await CreateUnassignedTaskWithPriorityAsync(highPriorityPersona, highPriorityDescription, TaskPriority.Critical);

        // Act
        var result = await SystemUnderTest.GetNextTaskAsync(agentId);

        // Assert - Should get the high priority task despite being newer
        ShouldBeBooleanExtensions.ShouldBeTrue(result.Success);
        ShouldBeStringTestExtensions.ShouldBe(result.TaskId, highPriorityTaskId);
        ShouldBeStringTestExtensions.ShouldBe(result.Persona, highPriorityPersona);
        ShouldBeStringTestExtensions.ShouldBe(result.Description, highPriorityDescription);

        // Verify the task is now assigned to the requesting agent
        using var scope = _scopeService.CreateReadScope();
        var claimedTask = await scope.Tasks.FindAsync(highPriorityTaskId);
        claimedTask.ShouldNotBeNull();
        claimedTask.AgentId.ShouldBe(agentId);

        // Verify low priority task is still unassigned
        var lowPriorityTask = await scope.Tasks.FindAsync(lowPriorityTaskId);
        lowPriorityTask.ShouldNotBeNull();
        lowPriorityTask.AgentId.ShouldBe(string.Empty);
    }

    private async Task<string> CreateUnassignedTaskWithPriorityAsync(string persona, string description, TaskPriority priority)
    {
        using var scope = _scopeService.CreateWriteScope();
        var taskId = Guid.NewGuid().ToString();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = string.Empty, // Unassigned task
            Status = DataLayer.Entities.TaskStatus.Pending,
            Persona = persona,
            Description = description,
            Priority = priority,
            CreatedAt = _serviceProvider.GetRequiredService<ITimeService>().UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return taskId;
    }

    [Fact]
    public async Task WhenMultipleAssignedTasksWithDifferentPriorities_ShouldReturnHighestPriorityFirst()
    {
        // Arrange
        var agentId = "agent-assigned-priority";
        var lowPriorityPersona = "You are a reviewer. Review code for quality.";
        var lowPriorityDescription = "Review documentation for typos";
        var highPriorityPersona = "You are a security reviewer. Review for security issues.";
        var highPriorityDescription = "Critical security review needed immediately";

        // Create a running agent
        await CreateRunningAgentAsync(agentId);

        // Create low priority assigned task first (older timestamp)
        var lowPriorityTaskId = await CreatePendingTaskWithPriorityAsync(agentId, lowPriorityPersona, lowPriorityDescription, TaskPriority.Low);

        // Advance time to ensure different timestamps
        _timeService.AdvanceTime(TimeSpan.FromMilliseconds(100));

        // Create high priority assigned task second (newer timestamp but higher priority)
        var highPriorityTaskId = await CreatePendingTaskWithPriorityAsync(agentId, highPriorityPersona, highPriorityDescription, TaskPriority.High);

        // Act
        var result = await SystemUnderTest.GetNextTaskAsync(agentId);

        // Assert - Should get the high priority task despite being newer
        ShouldBeBooleanExtensions.ShouldBeTrue(result.Success);
        ShouldBeStringTestExtensions.ShouldBe(result.TaskId, highPriorityTaskId);
        ShouldBeStringTestExtensions.ShouldBe(result.Persona, highPriorityPersona);
        ShouldBeStringTestExtensions.ShouldBe(result.Description, highPriorityDescription);
    }

    private async Task<string> CreatePendingTaskWithPriorityAsync(string agentId, string persona, string description, TaskPriority priority)
    {
        using var scope = _scopeService.CreateWriteScope();
        var taskId = Guid.NewGuid().ToString();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = DataLayer.Entities.TaskStatus.Pending,
            Persona = persona,
            Description = description,
            Priority = priority,
            CreatedAt = _serviceProvider.GetRequiredService<ITimeService>().UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return taskId;
    }

    [Fact]
    public async Task WhenHighPriorityUnassignedAndLowPriorityAssigned_ShouldReturnAssignedTaskFirst()
    {
        // Arrange
        var agentId = "agent-mixed-priority";
        var assignedPersona = "You are a reviewer. Review code for quality.";
        var assignedDescription = "Review basic documentation";
        var unassignedPersona = "You are an emergency responder. Handle critical issues.";
        var unassignedDescription = "Critical system failure needs immediate attention";

        // Create a running agent
        await CreateRunningAgentAsync(agentId);

        // Create high priority unassigned task first
        var unassignedTaskId = await CreateUnassignedTaskWithPriorityAsync(unassignedPersona, unassignedDescription, TaskPriority.Critical);

        // Advance time to ensure different timestamps
        _timeService.AdvanceTime(TimeSpan.FromMilliseconds(100));

        // Create low priority assigned task second
        var assignedTaskId = await CreatePendingTaskWithPriorityAsync(agentId, assignedPersona, assignedDescription, TaskPriority.Low);

        // Act
        var result = await SystemUnderTest.GetNextTaskAsync(agentId);

        // Assert - Should get the assigned task despite unassigned having higher priority
        ShouldBeBooleanExtensions.ShouldBeTrue(result.Success);
        ShouldBeStringTestExtensions.ShouldBe(result.TaskId, assignedTaskId);
        ShouldBeStringTestExtensions.ShouldBe(result.Persona, assignedPersona);
        ShouldBeStringTestExtensions.ShouldBe(result.Description, assignedDescription);

        // Verify the unassigned task is still unassigned and available for claiming
        using var scope = _scopeService.CreateReadScope();
        var unassignedTask = await scope.Tasks.FindAsync(unassignedTaskId);
        unassignedTask.ShouldNotBeNull();
        unassignedTask.AgentId.ShouldBe(string.Empty);
    }
}
