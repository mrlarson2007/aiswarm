# ADR-0002: SQLite-Based Task Coordination System

## Status

Proposed

## Context

Currently, AI agents in the AI Swarm Agent Launcher operate in complete isolation with no task coordination mechanism:

- Each agent works independently in separate git worktrees
- No systematic way to break down complex work into coordinated tasks
- No dependency management between related work items
- No central coordination or assignment of work to appropriate agent personas
- No progress tracking or heartbeat monitoring of agent health
- Manual coordination required for complex multi-step workflows

This prevents effective coordination of complex development workflows that require multiple specialized agents working in sequence or parallel with dependencies.

### Business Requirements

1. **Task Decomposition**: Planner agents should break complex goals into executable tasks
2. **Persona-Based Assignment**: Tasks should be matched to appropriate agent personas
3. **Dependency Management**: Tasks should respect dependencies and execution order
4. **Progress Monitoring**: Real-time visibility into task progress and agent health
5. **Fault Tolerance**: Handle agent failures with task reassignment capabilities
6. **Audit Trail**: Complete logging of task execution and agent activities

### Technical Constraints

1. Must maintain git worktree isolation for agent workspaces
2. Should integrate with existing persona system
3. Must support multiple concurrent agents
4. Should provide MCP server integration for IDE/editor access
5. Must follow existing TDD and clean code patterns
6. Should be cross-platform (Windows, Linux, macOS)

## Decision

We will implement a **SQLite-based task coordination system** with an integrated MCP server for agent communication and coordination.

### Architecture Components

1. **SQLite Database**: Central coordination database with schema for tasks, dependencies, agents, and logs
2. **MCP Server**: Model Context Protocol server exposing coordination APIs
3. **Task Queue System**: Persona-based task assignment with dependency management
4. **Agent Lifecycle Management**: Registration, heartbeat monitoring, and failure detection
5. **Persona-Task Binding**: Tasks assigned to specific personas with context loading

### Database Schema

```sql
-- Core persona definitions
CREATE TABLE personas (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    definition TEXT NOT NULL, -- JSON or markdown
    created_at TEXT NOT NULL
);

-- Immutable task definitions
CREATE TABLE tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    title TEXT NOT NULL,
    description TEXT NOT NULL,
    persona_id TEXT NOT NULL,
    context_ref TEXT, -- reference to context files/data
    priority INTEGER DEFAULT 0,
    created_by TEXT, -- agent_id of creator
    created_at TEXT NOT NULL,
    FOREIGN KEY (persona_id) REFERENCES personas(id)
);

-- Mutable task execution state
CREATE TABLE task_status (
    task_id INTEGER PRIMARY KEY,
    status TEXT NOT NULL CHECK (status IN ('pending', 'claimed', 'in_progress', 'completed', 'failed', 'cancelled')),
    assigned_agent_id TEXT,
    claimed_at TEXT,
    started_at TEXT,
    completed_at TEXT,
    lease_expires_at TEXT,
    result_data TEXT, -- JSON
    FOREIGN KEY (task_id) REFERENCES tasks(id)
);

-- Task dependency DAG
CREATE TABLE task_dependencies (
    task_id INTEGER NOT NULL,
    depends_on INTEGER NOT NULL,
    PRIMARY KEY (task_id, depends_on),
    FOREIGN KEY (task_id) REFERENCES tasks(id),
    FOREIGN KEY (depends_on) REFERENCES tasks(id)
);

-- Agent registration and sessions
CREATE TABLE agents (
    id TEXT PRIMARY KEY,
    persona_id TEXT NOT NULL,
    worktree_path TEXT,
    registered_at TEXT NOT NULL,
    last_heartbeat TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('active', 'idle', 'offline')),
    FOREIGN KEY (persona_id) REFERENCES personas(id)
);

-- Task execution logs
CREATE TABLE task_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task_id INTEGER NOT NULL,
    agent_id TEXT NOT NULL,
    level TEXT NOT NULL CHECK (level IN ('debug', 'info', 'warn', 'error')),
    message TEXT NOT NULL,
    logged_at TEXT NOT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(id),
    FOREIGN KEY (agent_id) REFERENCES agents(id)
);
```

### MCP Server APIs

**For Planner Agents:**
- `enqueue_task(title, description, persona_id, dependencies[])`
- `get_task_status(task_id)`
- `cancel_task(task_id)`
- `get_task_tree()` - view all tasks and dependencies

**For Worker Agents:**
- `heartbeat_and_get_available_tasks(agent_id, persona_id)`
- `claim_task(task_id, agent_id)`
- `report_progress(task_id, message, level)`
- `complete_task(task_id, result_data)`
- `fail_task(task_id, error_message)`

### Integration Approach

- Store SQLite database in `.aiswarm/coordination.db` within working directory
- Support flexible worktree assignment (agents can share worktrees)
- Enable planner agents to spawn and terminate worker agents
- Add new `--coordination-mode` option to agent launcher
- Integrate `ITaskCoordinationService` into command handlers
- Store personas in database while keeping file-based fallback
- MCP server runs as separate process or embedded service

## Consequences

### Positive

- **Sophisticated Task Coordination**: Complex workflows can be decomposed and coordinated automatically
- **Persona-Based Specialization**: Tasks are matched to appropriate agent capabilities
- **Dependency Management**: Proper ordering and coordination of interdependent work
- **Fault Tolerance**: Agent failures are detected and tasks can be reassigned
- **Progress Visibility**: Real-time insight into task execution and agent health
- **MCP Integration**: IDE/editor integration through standardized protocol
- **Audit Trail**: Complete history of task execution and agent activities
- **Scalability**: SQLite can handle significant task loads efficiently

### Negative

- **System Complexity**: Significantly more complex than file-based approach
- **SQLite Limitations**: Concurrent write limitations, no built-in clustering
- **MCP Server Dependency**: Additional service to deploy and maintain
- **Database Schema Evolution**: Schema migrations needed for changes
- **Lease Management Complexity**: Handling task leases and timeouts properly

### Neutral

- **Performance**: SQLite is fast for expected workloads but adds I/O overhead
- **Deployment**: Single SQLite file is portable but MCP server needs process management
- **Testing**: Database testing requires more sophisticated test setup

### Risk Mitigation

- Use SQLite WAL mode for better concurrent access
- Implement proper transaction handling for atomic operations
- Add database migration system for schema evolution
- Provide fallback to direct agent launching if coordination system fails
- Implement comprehensive logging for debugging coordination issues

### Future Considerations

- Could migrate to PostgreSQL/SQL Server for enterprise scale
- Distributed task coordination with multiple MCP servers
- Integration with external workflow engines (GitHub Actions, etc.)
- Real-time task updates via WebSocket or Server-Sent Events