# MCP Tools Reference

This document provides comprehensive documentation for all Model Context Protocol (MCP) tools available in the AISwarm coordination server.

## Overview

The AISwarm MCP server provides 13 tools for coordinating multi-agent workflows through VS Code and other MCP-compatible environments. These tools are organized into three main categories:

- **Task Management Tools (8)**: For creating, querying, and managing tasks
- **Agent Management Tools (3)**: For launching, monitoring, and terminating agents
- **Memory & State Management Tools (2)**: For persistent data storage and retrieval

## Task Management Tools

### `mcp_aiswarm_create_task`

Creates a new task and optionally assigns it to a specific agent.

**Parameters:**

- `agentId` (string, optional): ID of the agent to assign the task to. If null/empty, creates an unassigned task
- `persona` (string, required): Agent persona type (implementer, reviewer, planner, etc.)
- `description` (string, required): Detailed description of what the agent should accomplish
- `priority` (enum, optional): Task priority level - "Low", "Normal" (default), "High", or "Critical"

**Returns:**

- `CreateTaskResult` with success status and created task ID, or failure with error message

**Events Generated:**

- `TaskCreated` event published to the event bus for real-time coordination

**Example Usage:**

```typescript
mcp_aiswarm_create_task(
    agentId: "agent-123",
    persona: "implementer", 
    description: "Implement user authentication feature with JWT tokens",
    priority: "High"
)
```

### `mcp_aiswarm_get_next_task`

Primary polling endpoint for agents to retrieve their next available task. Supports configurable timeouts and retry logic.

**Parameters:**

- `agentId` (string, required): ID of the agent requesting a task
- `timeoutMs` (int, optional): Timeout in milliseconds (0-2,147,483,647). null/0 = no wait, positive = wait duration

**Configuration:**
Uses `GetNextTaskConfiguration` with configurable:

- `TimeToWaitForTask`: Maximum wait time (default: 5 minutes in production, 100ms in tests)
- `PollingInterval`: Time between polling attempts (default: 1 second in production, 10ms in tests)
- `MaxRetries`: Maximum retry attempts for race conditions (default: 10)

**Returns:**

- `GetNextTaskResult` with task details or synthetic 'system:requery:...' task ID when timeout expires

**Behavior:**

- Updates agent heartbeat automatically
- Transitions agent status from Starting → Running
- Publishes `TaskClaimed` event when task is successfully claimed
- Handles race conditions with configurable retry logic

**Example Usage:**

```typescript
// Wait up to 30 seconds for a task
mcp_aiswarm_get_next_task(agentId: "agent-123", timeoutMs: 30000)

// Return immediately if no tasks available
mcp_aiswarm_get_next_task(agentId: "agent-123")
```

### `mcp_aiswarm_get_task_status`

Retrieves detailed status information for a specific task.

**Parameters:**

- `taskId` (string, required): ID of the task to query

**Returns:**

- Task status details including current status, assigned agent, priority, timestamps, and results

**Example Usage:**

```typescript
mcp_aiswarm_get_task_status(taskId: "task-456")
```

### `mcp_aiswarm_get_tasks_by_status`

Retrieves all tasks filtered by their current status.

**Parameters:**

- `status` (string, required): Task status to filter by - "Pending", "InProgress", "Completed", or "Failed"

**Returns:**

- Array of tasks matching the specified status

**Example Usage:**

```typescript
mcp_aiswarm_get_tasks_by_status(status: "InProgress")
```

### `mcp_aiswarm_get_tasks_by_agent_id`

Retrieves all tasks assigned to a specific agent, regardless of status.

**Parameters:**

- `agentId` (string, required): ID of the agent to query tasks for

**Returns:**

- Array of all tasks assigned to the agent

**Example Usage:**

```typescript
mcp_aiswarm_get_tasks_by_agent_id(agentId: "agent-123")
```

### `mcp_aiswarm_get_tasks_by_agent_id_and_status`

Retrieves tasks for a specific agent filtered by status.

**Parameters:**

- `agentId` (string, required): ID of the agent to query tasks for
- `status` (string, required): Status to filter by

**Returns:**

- Array of tasks matching both agent ID and status criteria

**Example Usage:**

```typescript
mcp_aiswarm_get_tasks_by_agent_id_and_status(
    agentId: "agent-123", 
    status: "Completed"
)
```

### `mcp_aiswarm_report_task_completion`

Reports successful completion of a task with results.

**Parameters:**

- `taskId` (string, required): ID of the task to mark as completed
- `result` (string, required): Detailed result of the completed task

**Returns:**

- Success/failure status

**Events Generated:**

- `TaskCompleted` event published to the event bus

**Example Usage:**

```typescript
mcp_aiswarm_report_task_completion(
    taskId: "task-456",
    result: "Authentication feature implemented successfully with JWT tokens, 15 tests passing"
)
```

### `mcp_aiswarm_report_task_failure`

Reports task failure with error details.

**Parameters:**

- `taskId` (string, required): ID of the task to mark as failed
- `errorMessage` (string, required): Detailed error message explaining the failure

**Returns:**

- Success/failure status

**Events Generated:**

- `TaskFailed` event published to the event bus

**Example Usage:**

```typescript
mcp_aiswarm_report_task_failure(
    taskId: "task-456",
    errorMessage: "Build failed due to missing dependency: Microsoft.AspNetCore.Authentication.JwtBearer"
)
```

## Agent Management Tools

### `mcp_aiswarm_list_agents`

Lists all registered agents with optional persona filtering.

**Parameters:**

- `personaFilter` (string, optional): Filter agents by persona type (implementer, reviewer, planner, etc.)

**Returns:**

- Array of agent information including:
  - Agent ID and persona
  - Current status and process ID
  - Registration and last heartbeat timestamps
  - Working directory and worktree name
  - AI model being used

**Example Usage:**

```typescript
// List all agents
mcp_aiswarm_list_agents()

// List only implementer agents
mcp_aiswarm_list_agents(personaFilter: "implementer")
```

### `mcp_aiswarm_launch_agent`

Launches a new agent with specified configuration.

**Parameters:**

- `persona` (string, required): Agent persona type (implementer, reviewer, planner, etc.)
- `description` (string, required): Description of what the agent should accomplish
- `worktreeName` (string, optional): Name for git worktree isolation
- `model` (string, optional): AI model to use (defaults to system default)
- `yolo` (boolean, optional): Bypass permission prompts for autonomous operation (default: false)

**Returns:**

- `LaunchAgentResult` with agent ID and launch details, or failure with error message

**Behavior:**

- Creates isolated git worktree if `worktreeName` specified
- Registers agent in database with "Starting" status
- Launches Gemini CLI process with appropriate persona instructions

**Example Usage:**

```typescript
mcp_aiswarm_launch_agent(
    persona: "implementer",
    description: "Implement user authentication feature",
    worktreeName: "auth-feature",
    model: "gemini-1.5-pro",
    yolo: false
)
```

### `mcp_aiswarm_kill_agent`

Terminates a running agent and cleans up associated resources.

**Parameters:**

- `agentId` (string, required): ID of the agent to terminate

**Returns:**

- Success/failure status with termination details

**Behavior:**

- Terminates the agent's process
- Updates agent status to "Stopped"
- Cleans up associated resources

**Example Usage:**

```typescript
mcp_aiswarm_kill_agent(agentId: "agent-123")
```

## Memory & State Management Tools

### `mcp_aiswarm_save_memory`

Saves data to the persistent memory system for agent communication and state persistence.

**Parameters:**

- `key` (string, required): Unique key for the memory entry
- `value` (string, required): Data to store
- `type` (string, optional): Content type (json, text, binary, etc.) - defaults to "text"
- `metadata` (string, optional): JSON metadata for extensibility and rich queries
- `namespace` (string, optional): Namespace for organization - defaults to empty string

**Returns:**

- `SaveMemoryResult` with success status and saved key/namespace, or failure with error message

**Behavior:**

- Stores data in SQLite database with transaction support
- Updates timestamps for creation and modification
- Supports namespaced organization

**Example Usage:**

```typescript
mcp_aiswarm_save_memory(
    key: "auth-config",
    value: JSON.stringify({provider: "jwt", expires: "24h"}),
    type: "json",
    metadata: JSON.stringify({category: "configuration", version: "1.0"}),
    namespace: "authentication"
)
```

### `mcp_aiswarm_read_memory`

Reads stored memory entries with automatic access tracking.

**Parameters:**

- `key` (string, required): Key of the memory entry to read
- `namespace` (string, optional): Namespace of the memory - defaults to empty string

**Returns:**

- `ReadMemoryResult` with memory entry data, or failure if not found

**Behavior:**

- Retrieves data from SQLite database
- Updates access timestamp when memory is successfully read
- Returns full memory entry with metadata and timestamps

**Example Usage:**

```typescript
mcp_aiswarm_read_memory(
    key: "auth-config",
    namespace: "authentication"
)
```

## Event System Integration

The MCP tools are integrated with a comprehensive event system that enables real-time coordination:

### Event Types

- **TaskCreated**: Published when `mcp_aiswarm_create_task` creates a new task
- **TaskClaimed**: Published when `mcp_aiswarm_get_next_task` successfully claims a task
- **TaskCompleted**: Published when `mcp_aiswarm_report_task_completion` marks a task as completed
- **TaskFailed**: Published when `mcp_aiswarm_report_task_failure` reports a task failure

### Event Bus Architecture

The system uses `InMemoryEventBus<TType, TPayload>` with:

- Generic type support for different event categories
- Channel-based subscription model
- Event filtering and envelope wrapping
- Automatic cleanup and disposal

### Notification Services

- **WorkItemNotificationService**: Handles task-related events and notifications
- **AgentNotificationService**: Manages agent lifecycle events
- **DatabaseEventLoggerService**: Persists events to database for audit trails

## Configuration

### GetNextTaskConfiguration

Configurable polling behavior for `mcp_aiswarm_get_next_task`:

```csharp
public class GetNextTaskConfiguration
{
    public TimeSpan TimeToWaitForTask { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(10);
    public int MaxRetries { get; set; } = 50;
    
    public static GetNextTaskConfiguration Production => new()
    {
        TimeToWaitForTask = TimeSpan.FromMinutes(5),
        PollingInterval = TimeSpan.FromSeconds(1),
        MaxRetries = 10
    };
}
```

**Production vs Test Settings:**

- **Production**: 5-minute timeout, 1-second polling, 10 retries
- **Test**: 100ms timeout, 10ms polling, 50 retries

## Error Handling

All MCP tools implement comprehensive error handling:

- **Validation**: Parameter validation with descriptive error messages
- **Database Errors**: Transaction rollback and error propagation
- **Race Conditions**: Configurable retry logic with exponential backoff
- **Agent Not Found**: Graceful handling of missing agent references
- **Timeout Handling**: Synthetic responses when operations exceed timeout limits

## Best Practices

### For Agent Development

1. **Task Polling**: Use reasonable timeout values in `mcp_aiswarm_get_next_task` to balance responsiveness and resource usage
2. **Error Reporting**: Always use `mcp_aiswarm_report_task_failure` for comprehensive error tracking
3. **Memory Usage**: Use namespaced memory keys for organization and conflict prevention
4. **Heartbeat Maintenance**: Regular task polling automatically maintains agent heartbeats

### For Task Coordination

1. **Priority Management**: Use task priorities to ensure critical work gets attention
2. **Status Monitoring**: Regularly check task status using query tools
3. **Event Integration**: Subscribe to event bus notifications for real-time updates
4. **Resource Cleanup**: Use `mcp_aiswarm_kill_agent` for proper agent termination

### For Memory Management

1. **Key Naming**: Use descriptive, hierarchical key names
2. **Namespace Organization**: Group related data using namespaces
3. **Metadata Usage**: Include searchable metadata for complex queries
4. **Content Types**: Specify appropriate content types for data validation

## Integration Examples

### VS Code Setup

Add to `.vscode/mcp.json`:

```json
{
    "servers": {
        "aiswarm": {
            "type": "stdio",
            "command": "dotnet",
            "args": ["run", "--project", "src/AISwarm.Server"],
            "env": {
                "WorkingDirectory": "${workspaceFolder}"
            }
        }
    }
}
```

### Typical Workflow

1. **Launch Agent**: `mcp_aiswarm_launch_agent`
2. **Create Tasks**: `mcp_aiswarm_create_task` with appropriate priorities
3. **Agent Polling**: Agent uses `mcp_aiswarm_get_next_task` in loop
4. **Status Monitoring**: Use query tools to track progress
5. **Completion**: Agent reports results with `mcp_aiswarm_report_task_completion`
6. **Cleanup**: Use `mcp_aiswarm_kill_agent` when done

This comprehensive MCP tools suite enables sophisticated multi-agent coordination workflows with real-time event-driven communication and persistent state management.
