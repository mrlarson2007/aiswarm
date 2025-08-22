using AISwarm.DataLayer.Contracts;
using AISwarm.DataLayer.Database;
using AISwarm.DataLayer.Services;
using AISwarm.Server.McpTools;
using AgentLauncher.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace AgentLauncher.Tests.McpTools;

public class GetNextTaskMcpToolTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IDatabaseScopeService _scopeService;

    public GetNextTaskMcpToolTests()
    {
        var services = new ServiceCollection();

        // Add database services
        services.AddDbContext<CoordinationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));
        services.AddSingleton<ITimeService, FakeTimeService>();
        services.AddSingleton<IDatabaseScopeService, DatabaseScopeService>();

        // Add MCP tools
        services.AddSingleton<GetNextTaskMcpTool>();

        _serviceProvider = services.BuildServiceProvider();
        _scopeService = _serviceProvider.GetRequiredService<IDatabaseScopeService>();
    }

    [Fact]
    public async Task WhenNonExistentAgent_ShouldReturnFailureResult()
    {
        // Arrange
        var nonExistentAgentId = "non-existent-agent";

        var getNextTaskTool = _serviceProvider.GetRequiredService<GetNextTaskMcpTool>();

        // Act
        var result = await getNextTaskTool.GetNextTaskAsync(nonExistentAgentId);

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

        var getNextTaskTool = _serviceProvider.GetRequiredService<GetNextTaskMcpTool>();

        // Act
        var result = await getNextTaskTool.GetNextTaskAsync(agentId);

        // Assert
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldBeNull();
        result.Persona.ShouldBeNull();
        result.Description.ShouldBeNull();
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

        var getNextTaskTool = _serviceProvider.GetRequiredService<GetNextTaskMcpTool>();

        // Act
        var result = await getNextTaskTool.GetNextTaskAsync(agentId);

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
        var configuration = new AISwarm.Server.McpTools.GetNextTaskConfiguration
        {
            TimeToWaitForTask = TimeSpan.FromMilliseconds(50),  // Very short timeout
            PollingInterval = TimeSpan.FromMilliseconds(10)     // Very short polling interval
        };

        var getNextTaskTool = _serviceProvider.GetRequiredService<GetNextTaskMcpTool>();

        // Act
        var startTime = DateTime.UtcNow;
        var result = await getNextTaskTool.GetNextTaskAsync(agentId, configuration);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldBeNull();
        result.Persona.ShouldBeNull();
        result.Description.ShouldBeNull();
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
        var configuration = new AISwarm.Server.McpTools.GetNextTaskConfiguration
        {
            TimeToWaitForTask = TimeSpan.FromSeconds(1),     // 1 second timeout
            PollingInterval = TimeSpan.FromMilliseconds(100) // Check every 100ms
        };

        var getNextTaskTool = _serviceProvider.GetRequiredService<GetNextTaskMcpTool>();

        // Act - Start polling in background and add task after delay
        var pollingTask = getNextTaskTool.GetNextTaskAsync(agentId, configuration);
        
        // Wait a bit then add a task using a SEPARATE database scope (simulating external process)
        await Task.Delay(200);
        var taskId = await CreatePendingTaskUsingNewScopeAsync(agentId, expectedPersona, expectedDescription);
        
        var startTime = DateTime.UtcNow;
        var result = await pollingTask;
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldBe(taskId);
        result.Persona.ShouldBe(expectedPersona);
        result.Description.ShouldBe(expectedDescription);
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("call this tool again");
        result.Message.ShouldContain("get the next task");
        
        // Should have returned quickly once task was available, not waited full timeout
        elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(900));
    }

    private async Task CreateRunningAgentAsync(string agentId)
    {
        using var scope = _scopeService.CreateWriteScope();
        var agent = new AISwarm.DataLayer.Entities.Agent
        {
            Id = agentId,
            PersonaId = "test-persona",
            AgentType = "test",
            WorkingDirectory = "/test",
            Status = AISwarm.DataLayer.Entities.AgentStatus.Running,
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
        var task = new AISwarm.DataLayer.Entities.WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = AISwarm.DataLayer.Entities.TaskStatus.Pending,
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
        var task = new AISwarm.DataLayer.Entities.WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = AISwarm.DataLayer.Entities.TaskStatus.Pending,
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

        var getNextTaskTool = _serviceProvider.GetRequiredService<GetNextTaskMcpTool>();

        // Act
        var result = await getNextTaskTool.GetNextTaskAsync(agentId);

        // Assert
        result.Success.ShouldBeTrue();
        result.TaskId.ShouldBe(unassignedTaskId);
        result.Persona.ShouldBe(expectedPersona);
        result.Description.ShouldBe(expectedDescription);
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("call this tool again");
        
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
        var task = new AISwarm.DataLayer.Entities.WorkItem
        {
            Id = taskId,
            AgentId = string.Empty, // Unassigned task
            Status = AISwarm.DataLayer.Entities.TaskStatus.Pending,
            Persona = persona,
            Description = description,
            CreatedAt = _serviceProvider.GetRequiredService<ITimeService>().UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return taskId;
    }
}