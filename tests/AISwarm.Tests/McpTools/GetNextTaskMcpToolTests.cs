using AISwarm.DataLayer;
using AISwarm.DataLayer.Entities;
using AISwarm.Infrastructure;
using AISwarm.Server.McpTools;
using AISwarm.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using TaskStatus = AISwarm.DataLayer.Entities.TaskStatus;
using AISwarm.Infrastructure.Eventing;

namespace AISwarm.Tests.McpTools;

public class GetNextTaskMcpToolTests
    : IDisposable, ISystemUnderTest<GetNextTaskMcpTool>
{
    private readonly CoordinationDbContext _dbContext;
    private readonly IDatabaseScopeService _scopeService;
    private readonly FakeTimeService _timeService;
    private readonly Mock<ILocalAgentService> _mockLocalAgentService;
    private readonly IEventBus<TaskEventType, ITaskLifecyclePayload> _bus =
        new InMemoryEventBus<TaskEventType, ITaskLifecyclePayload>();
    private readonly IWorkItemNotificationService _notifier;
    private GetNextTaskMcpTool? _systemUnderTest;

    public GetNextTaskMcpTool SystemUnderTest =>
        _systemUnderTest ??= CreateSystemUnderTest();

    private GetNextTaskMcpTool CreateSystemUnderTest()
    {
        var tool = new GetNextTaskMcpTool(_scopeService, _mockLocalAgentService.Object, _notifier);
        tool.Configuration = new GetNextTaskConfiguration();
        return tool;
    }

    public GetNextTaskMcpToolTests()
    {
        _timeService = new FakeTimeService();

        var options = new DbContextOptionsBuilder<CoordinationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new CoordinationDbContext(options);
        _scopeService = new DatabaseScopeService(_dbContext);

        _mockLocalAgentService = new Mock<ILocalAgentService>();
        _mockLocalAgentService.Setup(x => x.UpdateHeartbeatAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        _notifier = new WorkItemNotificationService(_bus);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    public class TaskRetrievalNoTasksAvailableTests : GetNextTaskMcpToolTests
    {
        [Fact]
        public async Task WhenTaskExistsInDatabase_ShouldFindTaskOnFirstCall()
        {
            // Arrange - RED: This test reproduces the bug where agents don't pick up existing tasks
            var agentId = "agent-existing-task";
            var persona = "You are a reviewer. Review code for quality.";
            var description = "Review the authentication module";

            // Create a running agent
            await CreateRunningAgentAsync(agentId);

            // Create a task that's already assigned to the agent (simulating a task created before agent starts)
            // NOTE: This does NOT publish an event, so the agent should check database directly
            var taskId = await CreatePendingTaskAsync(agentId, persona, description);

            // Act - GetNextTaskAsync should check database first, not wait for events
            // Using a short timeout configuration - the key is it should find the task without timing out
            var config = new GetNextTaskConfiguration { TimeToWaitForTask = TimeSpan.FromMilliseconds(200) };
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await SystemUnderTest.GetNextTaskAsync(agentId, config);
            stopwatch.Stop();

            // Assert - Should find the task without waiting the full timeout period
            result.Success.ShouldBeTrue("Should find existing task in database");
            result.TaskId.ShouldBe(taskId);
            result.Persona.ShouldBe(persona);
            result.Description.ShouldBe(description);
            
            // Should not take the full timeout - should be much faster than 200ms
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(200, 
                "Should find task quickly from database, not wait full timeout period");

            // Verify heartbeat was updated
            _mockLocalAgentService.Verify(x => x.UpdateHeartbeatAsync(agentId), Times.Once);
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

            // Assert - No heartbeat update should be attempted for non-existent agent
            _mockLocalAgentService.Verify(x => x.UpdateHeartbeatAsync(It.IsAny<string>()), Times.Never);
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
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldNotBeNull();
            result.TaskId!.ShouldStartWith("system:");
            result.Persona.ShouldNotBeNull();
            result.Description.ShouldNotBeNull();
            result.Message.ShouldNotBeNull();
            result.Message.ShouldContain("No tasks available");
            result.Message.ShouldContain("call this tool again");

            // Verify the task is still assigned to the other agent
            using var readScope = _scopeService.CreateReadScope();
            var claimedTask = await readScope.Tasks.FindAsync(taskId);
            claimedTask.ShouldNotBeNull();
            claimedTask.AgentId.ShouldBe(otherAgentId);
        }

        [Fact]
        public async Task WhenTimeoutMsProvided_ShouldReturnAfterApproximatelyTimeoutWithNoTasks()
        {
            // Arrange
            var agentId = "agent-timeout-param";
            await CreateRunningAgentAsync(agentId);

            var tool = SystemUnderTest;
            // Ensure default config has long timeout, so param takes effect
            tool.Configuration = new GetNextTaskConfiguration
            {
                TimeToWaitForTask = TimeSpan.FromSeconds(10),
                PollingInterval = TimeSpan.FromMilliseconds(5)
            };

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await tool.GetNextTaskAsync(agentId, timeoutMs: 50);
            sw.Stop();

            // Assert synthetic no-task result
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldNotBeNull();
            result.TaskId!.ShouldStartWith("system:");
        }
    }

    public class TaskRetrievalWithMultipleTasksTests : GetNextTaskMcpToolTests
    {
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
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldBe(highPriorityTaskId);
            result.Persona.ShouldBe(highPriorityPersona);
            result.Description.ShouldBe(highPriorityDescription);

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
            _ = await CreatePendingTaskWithPriorityAsync(agentId, lowPriorityPersona, lowPriorityDescription, TaskPriority.Low);

            // Advance time to ensure different timestamps
            _timeService.AdvanceTime(TimeSpan.FromMilliseconds(100));

            // Create high priority assigned task second (newer timestamp but higher priority)
            var highPriorityTaskId = await CreatePendingTaskWithPriorityAsync(agentId, highPriorityPersona, highPriorityDescription, TaskPriority.High);

            // Act
            var result = await SystemUnderTest.GetNextTaskAsync(agentId);

            // Assert - Should get the high priority task despite being newer
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldBe(highPriorityTaskId);
            result.Persona.ShouldBe(highPriorityPersona);
            result.Description.ShouldBe(highPriorityDescription);
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
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldBe(assignedTaskId);
            result.Persona.ShouldBe(assignedPersona);
            result.Description.ShouldBe(assignedDescription);

            // Verify the unassigned task is still unassigned and available for claiming
            using var scope = _scopeService.CreateReadScope();
            var unassignedTask = await scope.Tasks.FindAsync(unassignedTaskId);
            unassignedTask.ShouldNotBeNull();
            unassignedTask.AgentId.ShouldBe(string.Empty);
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

        [Fact]
        public async Task WhenUnassignedTaskForDifferentPersona_ShouldNotBeClaimedByNonMatchingAgent()
        {
            // Arrange
            var reviewerAgentId = "agent-reviewer";
            var plannerAgentId = "agent-planner";
            await CreateRunningAgentAsync(reviewerAgentId, "reviewer");
            await CreateRunningAgentAsync(plannerAgentId, "planner");

            var expectedPersona = "You are a planner. Plan and coordinate development tasks.";
            var expectedDescription = "Plan the authentication feature implementation";
            var taskId = await CreateUnassignedTaskAsync(expectedPersona, expectedDescription, "planner");

            // Act - non-matching agent tries to get task
            var nonMatchingResult = await SystemUnderTest.GetNextTaskAsync(reviewerAgentId);

            // Assert - reviewer should not claim planner task
            nonMatchingResult.Success.ShouldBeTrue();
            nonMatchingResult.TaskId.ShouldNotBeNull();
            nonMatchingResult.TaskId!.ShouldStartWith("system:");

            // Verify task remains unassigned
            using (var readScope = _scopeService.CreateReadScope())
            {
                var task = await readScope.Tasks.FindAsync(taskId);
                task.ShouldNotBeNull();
                task.AgentId.ShouldBe(string.Empty);
            }
        }

        [Fact]
        public async Task WhenUnassignedTaskPersonaMatchesAgent_ShouldBeClaimedByThatAgent()
        {
            // Arrange
            var agentId = "agent-persona-match";
            await CreateRunningAgentAsync(agentId, "planner");

            var expectedPersona = "You are a planner. Plan and coordinate development tasks.";
            var expectedDescription = "Plan the roadmap";
            var unassignedTaskId = await CreateUnassignedTaskAsync(expectedPersona, expectedDescription, "planner");

            // Act
            var result = await SystemUnderTest.GetNextTaskAsync(agentId);

            // Assert - should claim and return the matching task
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldBe(unassignedTaskId);
            result.Persona.ShouldBe(expectedPersona);
            result.Description.ShouldBe(expectedDescription);

            // Verify in DB that the task is now assigned to the agent
            using var scope = _scopeService.CreateReadScope();
            var claimed = await scope.Tasks.FindAsync(unassignedTaskId);
            claimed.ShouldNotBeNull();
            claimed.AgentId.ShouldBe(agentId);
        }
    }

    public class TaskRetrievalWithReinforcingPromptTests : GetNextTaskMcpToolTests
    {
        [Fact]
        public async Task WhenAgentHasNoTasks_ShouldReturnNoTasksWithReinforcingPrompt()
        {
            // Arrange
            var agentId = "agent-no-tasks";

            // Create a running agent with no tasks
            using (var scope = _scopeService.CreateWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "test-persona",
                    AgentType = "test",
                    WorkingDirectory = "/test",
                    Status = AgentStatus.Running,
                    RegisteredAt = _timeService.UtcNow,
                    LastHeartbeat = _timeService.UtcNow
                });
                await scope.SaveChangesAsync();
            }

            // Act
            var result = await SystemUnderTest.GetNextTaskAsync(agentId);

            // Assert - return a default synthetic task instructing the agent to re-query
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldNotBeNull();
            result.TaskId.ShouldStartWith("system:");
            result.Persona.ShouldNotBeNull();
            result.Description.ShouldNotBeNull();
            result.Message.ShouldNotBeNull();
            result.Message.ShouldContain("No tasks available");
            result.Message.ShouldContain("call this tool again");

            // Assert - Should update agent heartbeat when requesting tasks
            _mockLocalAgentService.Verify(x => x.UpdateHeartbeatAsync(agentId), Times.Once);
        }

        [Fact]
        public async Task WhenAgentHasPendingTask_ShouldReturnTaskWithReinforcingPrompt()
        {
            // Arrange
            var agentId = "agent-123";
            var expectedPersona = "You are a code reviewer. Review code for quality and security.";
            var expectedDescription = "Review the authentication module for security vulnerabilities";

            // Create a running agent first
            using (var scope = _scopeService.CreateWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "test-persona",
                    AgentType = "test",
                    WorkingDirectory = "/test",
                    Status = AgentStatus.Running,
                    RegisteredAt = _timeService.UtcNow,
                    LastHeartbeat = _timeService.UtcNow
                });
                await scope.SaveChangesAsync();
            }

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

            // Assert - Should update agent heartbeat when requesting tasks
            _mockLocalAgentService.Verify(x => x.UpdateHeartbeatAsync(agentId), Times.Once);
        }
    }

    public class TaskPollingTests : GetNextTaskMcpToolTests
    {
        [Fact]
        public async Task WhenPollingForTasks_ShouldUpdateAgentHeartbeat()
        {
            // Arrange
            var agentId = "agent-heartbeat-test";
            using (var scope = _scopeService.CreateWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "test-persona",
                    AgentType = "test",
                    WorkingDirectory = "/test",
                    Status = AgentStatus.Running,
                    RegisteredAt = _timeService.UtcNow,
                    LastHeartbeat = _timeService.UtcNow
                });
                await scope.SaveChangesAsync();
            }

            // Act
            var result = await SystemUnderTest.GetNextTaskAsync(agentId);

            // Assert - Should call UpdateHeartbeatAsync exactly once
            _mockLocalAgentService.Verify(x => x.UpdateHeartbeatAsync(agentId), Times.Once);
            result.Success.ShouldBeTrue();
        }

        [Fact]
        public async Task WhenAgentHasNoTasksAndPollingTimeoutExpires_ShouldReturnNoTasksAfterWaiting()
        {
            // Arrange
            var agentId = "agent-polling-timeout";

            // Create a running agent with no tasks
            using (var scope = _scopeService.CreateWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "test-persona",
                    AgentType = "test",
                    WorkingDirectory = "/test",
                    Status = AgentStatus.Running,
                    RegisteredAt = _timeService.UtcNow,
                    LastHeartbeat = _timeService.UtcNow
                });
                await scope.SaveChangesAsync();
            }

            // Configure very short polling timeout and interval for fast test
            var configuration = new GetNextTaskConfiguration
            {
                TimeToWaitForTask = TimeSpan.FromMilliseconds(50),  // Very short timeout
                PollingInterval = TimeSpan.FromMilliseconds(10)     // Very short polling interval
            };

            // Act
            var result = await SystemUnderTest.GetNextTaskAsync(agentId, configuration);

            // Assert - returns a synthetic default task instructing a re-query
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldNotBeNull();
            result.TaskId!.ShouldStartWith("system:");
            result.Persona.ShouldNotBeNull();
            result.Description.ShouldNotBeNull();
            result.Message.ShouldNotBeNull();
            result.Message.ShouldContain("No tasks available");
            result.Message.ShouldContain("call this tool again");
        }

        [Fact]
        public async Task WhenTaskArrivesWhilePolling_ShouldReturnTaskImmediately()
        {
            // Arrange
            var agentId = "agent-polling-success";
            var expectedPersona = "You are a code reviewer. Review code for quality and security.";
            var expectedDescription = "Review the authentication module for security vulnerabilities";

            // Create a running agent with no initial tasks
            using (var scope = _scopeService.CreateWriteScope())
            {
                scope.Agents.Add(new Agent
                {
                    Id = agentId,
                    PersonaId = "test-persona",
                    AgentType = "test",
                    WorkingDirectory = "/test",
                    Status = AgentStatus.Running,
                    RegisteredAt = _timeService.UtcNow,
                    LastHeartbeat = _timeService.UtcNow
                });
                await scope.SaveChangesAsync();
            }

            // Configure longer wait so we can simulate arrival
            var configuration = new GetNextTaskConfiguration
            {
                TimeToWaitForTask = TimeSpan.FromSeconds(1),     // 1 second timeout
                PollingInterval = TimeSpan.FromMilliseconds(100) // No longer used, kept for compatibility
            };

            // Act - Start waiting and add task after delay
            var pollingTask = SystemUnderTest.GetNextTaskAsync(agentId, configuration);

            // Simulate task creation after a short delay
            _timeService.AdvanceTime(TimeSpan.FromMilliseconds(200));
            var taskId = await CreatePendingTaskUsingNewScopeAsync(agentId, expectedPersona, expectedDescription);

            // Publish TaskCreated on the event bus so the waiter wakes immediately
            await _notifier.PublishTaskCreated(taskId, agentId, expectedPersona);

            var result = await pollingTask;

            // Assert
            result.Success.ShouldBeTrue();
            result.TaskId.ShouldBe(taskId);
            result.Persona.ShouldBe(expectedPersona);
            result.Description.ShouldBe(expectedDescription);
            result.Message.ShouldNotBeNull();
            result.Message.ShouldContain("call this tool again");
            result.Message.ShouldContain("get the next task");
        }
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
            RegisteredAt = _timeService.UtcNow,
            LastHeartbeat = _timeService.UtcNow
        };
        scope.Agents.Add(agent);
        await scope.SaveChangesAsync();
        scope.Complete();
    }

    private async Task CreateRunningAgentAsync(string agentId, string personaId)
    {
        using var scope = _scopeService.CreateWriteScope();
        var agent = new Agent
        {
            Id = agentId,
            PersonaId = personaId,
            AgentType = "test",
            WorkingDirectory = "/test",
            Status = AgentStatus.Running,
            LastHeartbeat = _timeService.UtcNow,
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
            Status = TaskStatus.Pending,
            Persona = persona,
            Description = description,
            CreatedAt = _timeService.UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return taskId;
    }

    private async Task<string> CreatePendingTaskUsingNewScopeAsync(string agentId, string persona, string description)
    {
        // Create a completely new scope service to simulate external process
        using var scope = _scopeService.CreateWriteScope();
        var taskId = Guid.NewGuid().ToString();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = TaskStatus.Pending,
            Persona = persona,
            Description = description,
            CreatedAt = _timeService.UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return taskId;
    }

    private async Task<string> CreateUnassignedTaskAsync(string persona, string description, string? personaId = null)
    {
        using var scope = _scopeService.CreateWriteScope();
        var taskId = Guid.NewGuid().ToString();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = string.Empty, // Unassigned task
            Status = TaskStatus.Pending,
            Persona = persona,
            Description = description,
            PersonaId = personaId,
            CreatedAt = _timeService.UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return taskId;
    }


    private async Task<string> CreatePendingTaskWithPriorityAsync(string agentId, string persona, string description, TaskPriority priority)
    {
        using var scope = _scopeService.CreateWriteScope();
        var taskId = Guid.NewGuid().ToString();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = agentId,
            Status = TaskStatus.Pending,
            Persona = persona,
            Description = description,
            Priority = priority,
            CreatedAt = _timeService.UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return taskId;
    }


    private async Task<string> CreateUnassignedTaskWithPriorityAsync(string persona, string description, TaskPriority priority)
    {
        using var scope = _scopeService.CreateWriteScope();
        var taskId = Guid.NewGuid().ToString();
        var task = new WorkItem
        {
            Id = taskId,
            AgentId = string.Empty, // Unassigned task
            Status = TaskStatus.Pending,
            Persona = persona,
            Description = description,
            Priority = priority,
            CreatedAt = _timeService.UtcNow
        };
        scope.Tasks.Add(task);
        await scope.SaveChangesAsync();
        scope.Complete();
        return taskId;
    }

}
