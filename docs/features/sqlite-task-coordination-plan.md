# SQLite Task Coordination Feature - Implementation Plan

## Overview

Implement a SQLite-based task coordination system that enables sophisticated workflow decomposition, persona-based task assignment, and coordinated execution across multiple AI agents with dependency management and fault tolerance.

## Requirements Analysis

### Functional Requirements

1. **Task Decomposition**: Planner agents break complex goals into executable tasks
2. **Persona-Based Assignment**: Tasks matched to appropriate agent personas  
3. **Dependency Management**: Tasks respect execution order and dependencies
4. **Task Leasing**: Agents claim tasks with heartbeat-based lease management
5. **Progress Tracking**: Real-time visibility into task execution status
6. **Fault Tolerance**: Failed agents release tasks for reassignment
7. **MCP Integration**: Expose coordination APIs via Model Context Protocol
8. **Audit Logging**: Complete history of task execution and agent activities

### Non-Functional Requirements

1. **Performance**: Handle 100+ concurrent tasks efficiently
2. **Reliability**: SQLite WAL mode for concurrent access
3. **Testability**: All components must be unit testable with fakes
4. **Cross-Platform**: Windows, Linux, macOS support
5. **Backward Compatibility**: Work alongside existing direct agent launching

## Implementation Phases

### Phase 1: Database Foundation

**TDD Approach:**

1. **RED**: Write failing test for `ITaskCoordinationService.EnqueueTaskAsync()`
2. **GREEN**: Implement minimal SQLite-based service
3. **COMMIT**: Working task creation functionality  
4. **REFACTOR**: Improve database abstraction
5. **COMMIT**: Refactored foundation

**Components:**
- SQLite database schema creation
- `ITaskCoordinationService` interface
- Basic task CRUD operations
- Database migration system

### Phase 2: Task Dependencies

**TDD Approach:**

1. **RED**: Write failing test for dependency validation
2. **GREEN**: Implement dependency checking logic
3. **COMMIT**: Working dependency management
4. **REFACTOR**: Extract dependency resolution service
5. **COMMIT**: Clean dependency architecture

**Components:**
- Dependency graph validation
- Available task resolution (respecting dependencies)
- Circular dependency detection

### Phase 3: Agent Lifecycle

**TDD Approach:**

1. **RED**: Write failing test for agent registration
2. **GREEN**: Implement agent registration and heartbeat
3. **COMMIT**: Working agent management
4. **REFACTOR**: Extract agent session management
5. **COMMIT**: Clean agent lifecycle

**Components:**
- Agent registration and deregistration
- Heartbeat monitoring
- Task lease management
- Agent failure detection and task reassignment

### Phase 4: MCP Server Integration

**TDD Approach:**

1. **RED**: Write failing test for MCP tool execution
2. **GREEN**: Implement basic MCP server
3. **COMMIT**: Working MCP integration
4. **REFACTOR**: Improve tool organization
5. **COMMIT**: Clean MCP architecture

**Components:**
- MCP server process
- Task management tools for planners
- Task execution tools for workers
- Integration with existing agent launcher

### Phase 5: Advanced Features

**TDD Approach:**

1. **RED**: Write failing test for task retry logic
2. **GREEN**: Implement retry mechanisms
3. **COMMIT**: Working retry system
4. **REFACTOR**: Extract retry policy configuration
5. **COMMIT**: Configurable retry system

**Components:**
- Task retry and failure handling
- Progress reporting and logging
- Task result processing
- Performance monitoring

## Database Design

### Core Schema

```sql
-- Core tables for task coordination
CREATE TABLE personas (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    definition TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    description TEXT NOT NULL,
    persona_id TEXT NOT NULL,
    created_by TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (persona_id) REFERENCES personas(id)
);

CREATE TABLE task_status (
    task_id INTEGER PRIMARY KEY,
    status TEXT NOT NULL CHECK (status IN ('pending', 'claimed', 'in_progress', 'completed', 'failed')),
    assigned_agent_id TEXT,
    claimed_at TEXT,
    lease_expires_at TEXT,
    FOREIGN KEY (task_id) REFERENCES tasks(id)
);

CREATE TABLE task_dependencies (
    task_id INTEGER NOT NULL,
    depends_on INTEGER NOT NULL,
    PRIMARY KEY (task_id, depends_on),
    FOREIGN KEY (task_id) REFERENCES tasks(id),
    FOREIGN KEY (depends_on) REFERENCES tasks(id)
);

CREATE TABLE agents (
    id TEXT PRIMARY KEY,
    persona_id TEXT NOT NULL,
    registered_at TEXT NOT NULL DEFAULT (datetime('now')),
    last_heartbeat TEXT NOT NULL DEFAULT (datetime('now')),
    status TEXT NOT NULL DEFAULT 'active',
    FOREIGN KEY (persona_id) REFERENCES personas(id)
);
```

## Service Architecture

### Core Interfaces

```csharp
public interface ITaskCoordinationService
{
    // Task Management
    Task<int> EnqueueTaskAsync(CreateTaskRequest request);
    Task<IEnumerable<TaskInfo>> GetAvailableTasksAsync(string personaId);
    Task<bool> ClaimTaskAsync(int taskId, string agentId);
    Task CompleteTaskAsync(int taskId, TaskResult result);
    
    // Agent Management  
    Task RegisterAgentAsync(AgentInfo agentInfo);
    Task<bool> HeartbeatAsync(string agentId);
    Task DeregisterAgentAsync(string agentId);
}

public interface ITaskDependencyService
{
    Task<bool> ValidateDependenciesAsync(int[] dependencies);
    Task<IEnumerable<int>> GetAvailableTasksAsync(string personaId);
    Task<bool> AreDependenciesSatisfiedAsync(int taskId);
}

public interface IAgentSessionService
{
    Task<string> RegisterAgentAsync(AgentRegistration registration);
    Task UpdateHeartbeatAsync(string agentId);
    Task<IEnumerable<AgentInfo>> GetActiveAgentsAsync();
    Task CleanupExpiredSessionsAsync();
}
```

## Testing Strategy

### Test Structure

```csharp
public class TaskCoordinationServiceTests
{
    private readonly FakeSqliteDatabase _database = new();
    private readonly TestLogger _logger = new();
    
    private TaskCoordinationService SystemUnderTest => 
        new TaskCoordinationService(_database, _logger);
    
    [Fact]
    public async Task WhenEnqueueingTask_ShouldCreateTaskInDatabase()
    {
        // Arrange
        var request = new CreateTaskRequest
        {
            Title = "Test Task",
            Description = "Test Description", 
            PersonaId = "implementer"
        };
        
        // Act
        var taskId = await SystemUnderTest.EnqueueTaskAsync(request);
        
        // Assert
        taskId.ShouldBeGreaterThan(0);
        var task = await _database.GetTaskAsync(taskId);
        task.Title.ShouldBe("Test Task");
    }
}
```

### Test Doubles

- `FakeSqliteDatabase`: In-memory SQLite for fast testing
- `TestMcpServer`: Mock MCP server for integration tests
- `FakeAgentSession`: Simulated agent sessions for testing

## Integration Points

### With Existing System

1. **Agent Launcher**: Add `--coordination-mode` flag
2. **Command Handlers**: Inject `ITaskCoordinationService`
3. **Persona Loading**: Use database with file fallback
4. **Git Worktrees**: Maintain existing isolation

### With External Systems

1. **IDE Integration**: MCP server provides task management UI
2. **CI/CD**: Tasks can trigger builds and deployments
3. **Monitoring**: Export metrics about task execution
4. **Logging**: Structured logging for debugging and audit

## Risk Mitigation

1. **SQLite Concurrency**: Use WAL mode and proper transaction handling
2. **Agent Failures**: Implement lease timeouts and task reassignment
3. **Database Evolution**: Schema migration system for updates
4. **Performance**: Proper indexing and query optimization
5. **Testing Complexity**: Comprehensive test doubles and fixtures

## Success Criteria

1. **Functional**: Planner can decompose work and workers execute tasks
2. **Performance**: Handle 100+ concurrent tasks with sub-second response
3. **Reliability**: 99.9% task completion rate with proper error handling
4. **Usability**: Clear MCP API for both planners and workers
5. **Maintainability**: High test coverage and clean architecture

This implementation plan provides a solid foundation for building the SQLite task coordination system while maintaining the TDD workflow and clean code principles established in the codebase.