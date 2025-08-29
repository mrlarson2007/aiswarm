
# C# CLI Agent Launcher Plan

## Purpose

- Launch Gemini CLI agents with a context file containing detailed instructions (persona).
- All instruction/context files are built-in resources (embedded in the executable).
- The CLI is self-contained for easy installation and deployment.

## Features (Initial Version)

- **Agent Launching:**
  - Launch Gemini CLI in a new console window for each agent.
  - Pass a context file (persona/instructions) to Gemini using the correct flag.
  - Allow selection of Gemini model (e.g., default, pro) per agent launch; default is configurable, can override per launch.
  - Use Gemini CLI flags: `-m <model>` for model selection, `-i <prompt>` for interactive session with initial prompt.
- **Context File Management:**
  - Provide built-in context/instruction files for planner, implementer, reviewer, etc.
  - Option to select which agent persona to launch.
- **Worktree Management:**
  - Optionally create a git worktree for each agent (except planner, which stays on main/master).
  - Automatically switch to the correct branch/worktree for each agent.
- **Simple CLI Interface:**
  - List available agent types/personas.
  - Launch agent with selected persona and context.
  - Optionally specify worktree/branch for agent.

## Future Features

- Add/modify context files (personas) via CLI.
- Support for custom agent types.
- Integration with other LLMs or tools.
- Automated merging of agent worktrees.
- More advanced agent communication.

## Flow

1. User runs CLI tool.
2. User selects agent type/persona.
3. User selects Gemini model (default or override).
4. Tool creates worktree (if needed) and context file from built-in resources.
5. Tool launches Gemini CLI in a new console window, passing the context file and model flag using `-m <model> -i <prompt>`.
6. Planner agent stays on main/master; other agents get their own worktree/branch.

---

## Updated Design Decisions & Clarifications

### Persona/Context Files

- Use Markdown for default personas and instructions (human-friendly, easy to edit).
- Default templates are embedded resources; users can add more in a well-known directory (per OS).
- CLI will support loading additional templates from this directory in future versions.

### Worktree Management

- CLI will create git worktrees for agents (except planner, which stays on main/master).
- Automated merging/cleanup will be added after basic worktree creation is working.
- User confirmation for destructive actions (e.g., cleanup) can be added as needed.

### Gemini CLI Integration

- Assume Gemini CLI is available in PATH.
- Use PowerShell on Windows, bash/shell on Mac/Linux for launching agents.
- Design is flexible to support other LLM agent tools in the future.

### Agent Communication

- Agents communicate with the user or a planning agent via markdown files in their workspace.
- No direct agent-to-agent communication for now; keep it simple.

### Logging & Activity

- Logging is optional and can be added for debugging or development.

### Cross-Platform Support

- Focus on Windows for first pass, but avoid hard-coding Windows-only logic.
- Keep code open for future Mac/Linux support.

### Deployment

- Prefer .NET tool install (`dotnet tool install -g ...`) for easy installation, updates, and cross-platform support.

### Security & Permissions

- No restrictions for now; can be added later if needed.

---

---

## Implementation Roadmap (Phased)

### Phase 1: Scaffold CLI Tool

- Create a new dotnet CLI tool using the built-in template (`dotnet new tool`).
- Set up basic project structure and ensure it builds and runs.

### Phase 2: Command Line Arguments

- Add support for command line arguments:
  - Agent type/persona selection
  - Gemini model selection (with default and override)
  - Worktree/branch specification
- Validate argument parsing and help output.

### Phase 3: Context File Management

- Embed default markdown persona/instruction files as resources.
- Add logic to copy or generate context files in agent workspace.
- Support loading additional templates from a well-known directory (future).

### Phase 4: Worktree Creation

- Implement git worktree creation for agents (except planner).
- Switch to correct branch/worktree for each agent.
- Add user confirmation for destructive actions (cleanup, delete).

### Phase 5: Gemini CLI Launch

- Launch Gemini CLI in a new console window for each agent.
- Pass context file and model flag using `-m <model> -i <prompt>`.
- Use PowerShell on Windows, bash/shell on Mac/Linux.

### Phase 6: Review & Polish

- Test all features end-to-end.
- Add optional logging for debugging/development.
- Refine CLI help, error handling, and user experience.

### Phase 7: Future Enhancements

- Add/modify context files via CLI.
- Support custom agent types and other LLMs.
- Implement automated merging/cleanup of worktrees.
- Add advanced agent communication and central state tracking.

---

Work through each phase sequentially, ensuring each piece is working before moving to the next. Adjust priorities and add features as needed based on feedback and usage.

---

## Event Bus & MCP Notifications Plan (Current Status)

## Where We Are (feature/event-bus-tdd)

- Minimal in-memory event bus + service:
  - `InMemoryEventBus`, `EventEnvelope`, `EventFilter`, `IEventBus`.
  - `WorkItemNotificationService` with `SubscribeForAgent`, `SubscribeForPersona`, `PublishTaskCreated`.
- Tests (green):
  - Happy path: agent subscription receives `TaskCreated`.
  - Input validation: throws on null/whitespace `agentId` and `persona`.
- Branch: `feature/event-bus-tdd` with incremental TDD commits.

## Next Tests (TDD – one at a time)

- Persona delivery: case-insensitive persona subscription receives `TaskCreated`.
- Cancellation: subscription cancels promptly on `CancellationToken`.
- Dual delivery: publish with both `agentId` and `persona` delivers to both streams.
- Poison resilience: non-`TaskCreated` payload doesn’t break enumerators.
- Publish watchdog: warn/track when publish exceeds threshold (introduce timing hook/logging).
- Backpressure/coalescing: introduce bounded channels and simple coalescing (after tests justify).

## MCP Integration Options

- Resource subscription (native):
  - Expose resources: `aiswarm://work-items/agents/{agentId}` and `aiswarm://work-items/personas/{persona}`.
  - Implement `SubscribeToResources`/`UnsubscribeFromResources`; emit `resources/updated` on events.
- Await tool (explicit wait):
  - Tool `await_next_work_item_event(agentId?, persona?, timeoutMs=30000)`.
  - Subscribes internally and returns first event or null on timeout.
  - Exactly one of `agentId` or `persona` required; persona case-insensitive.

Decision lean: start with the await tool (Gemini-friendly), keep resource subscription on deck.

## Future: Gemini StdIn/Broker Control

- Keep stdin open:
  - If we spawn Gemini ourselves, keep the redirected stdin handle open to push prompts/commands on demand.
  - Caveat: once all copies of stdin are closed, it cannot be reattached; must respawn process.
- Lightweight broker:
  - Run a small named-pipe or TCP broker that holds Gemini’s stdin; multiple senders connect to the broker which forwards lines to Gemini.
  - Pros: decouples producers, supports out-of-band triggers (e.g., event notifications prompting checks).
  - Consider simple auth (token/ACL) and backpressure (bounded queue, drop/oldest).

## Short-Term Actions

- Add TDD tests listed above, one per cycle, with minimal changes to pass.
- Decide and implement the first MCP bridge (await tool) behind a thin DI-registered handler.
- Document the chosen path and revisit broker/stdin approach if we need direct push control.

## Persona Routing (Task Assignment)

- Work items carry a `PersonaId` used only for routing/assignment; the full `Persona` text is still returned to the agent for now.
- Unassigned tasks with a `PersonaId` are claimable only by agents with matching `Agent.PersonaId`.
- Unassigned tasks with no `PersonaId` remain claimable by any agent.

---

## Current Status: MCP Coordination System

### What We Built

Implemented a Model Context Protocol (MCP) coordination system with the following components:

### MCP Tools Implemented (14/15 Core Tools)

**Agent Lifecycle Management**:
- `list_agents` - List and filter agents by persona
- `launch_agent` - Launch new agents with persona/context  
- `kill_agent` - Stop/terminate agents with lifecycle tracking
- `register_agent` - Handled implicitly by `launch_agent`

**Task Coordination System**:
- `create_task` - Create tasks with agent assignment
- `get_task_status` - Get individual task details
- `get_tasks_by_status` - Filter tasks by status
- `get_tasks_by_agent_id` - Filter tasks by agent
- `get_tasks_by_agent_id_and_status` - Combined filtering
- `get_next_task` - Get next available task for agent
- `report_task_completion` - Mark task as completed
- `report_task_failure` - Mark task as failed

**Memory System**:
- `save_memory` - Save data to shared memory
- `read_memory` - Read data from shared memory

### Remaining Core Tools (1/15)
- `wait_for_agent_ready` - Wait until agent is ready to start work

Note: `get_agent_status` removed as redundant with `list_agents` functionality.

## Recent Design Decisions

### Removed: Generic Event Waiting
- **Decision**: Remove `wait_for_next_event` (too broad, complex)
- **Replacement**: `wait_for_agent_ready` (focused, specific use case)
- **Rationale**: Agents need simple readiness check, not generic event streaming

### Removed: Explicit Heartbeat
- **Decision**: Remove `update_heartbeat` as separate tool
- **Implementation**: Implicit heartbeat via `get_next_task` and task status updates
- **Benefits**: Simpler API, natural activity tracking, no overhead

### Agent Coordination Pattern
- **Flow**: Agents poll for tasks → process → report status → repeat
- **Heartbeat**: Implicit through regular task operations
- **Event-Driven Architecture**: InMemoryEventBus with TaskEvent/AgentEvent envelopes
- **Database Persistence**: EF Core with SQLite, transaction scoping via DatabaseScopeService  
- **Test Coverage**: 181 tests passing with comprehensive TDD workflow
- **Clean Architecture**: DI, SOLID principles, proper separation of concerns

---

## Production Roadmap

### Critical Foundation

**Local Development Workflow**
- Problem: Manual install cycle slows development iteration
- Solution: Automated `dev-install` script for global tool refresh
- Impact: Faster development testing with real MCP tools

**Self-Contained Packaging**  
- Problem: Users need .NET runtime installed
- Solution: Self-contained deployment with embedded runtime
- Impact: Zero-dependency installation for end users

**Complete Final MCP Tool**
- Missing: `wait_for_agent_ready` 
- Impact: 100% core functionality complete

**Global Tool Publishing**
- Problem: No distribution mechanism
- Solution: NuGet global tool + GitHub releases
- Impact: Easy installation via `dotnet tool install -g`

### User Experience

**Unified CLI Experience**
- Problem: Separate AgentLauncher and MCP server
- Solution: AgentLauncher calls MCP server internally  
- Impact: Single entry point for all operations

**Installation Documentation**
- Problem: No guidance for new users
- Solution: Clear README with quick start guide
- Impact: Smooth onboarding experience

**Enhanced Personas**
- Problem: Basic personas need expansion
- Solution: Rich, detailed persona instructions
- Impact: Better out-of-box agent behavior

### Operational Concerns

**Configuration Management**
- Problem: Hard-coded settings  
- Solution: Environment variables, config files
- Impact: Production deployment flexibility

**Error Handling & Recovery**
- Problem: Basic error handling only
- Solution: Graceful degradation, retry logic
- Impact: Production reliability

**Health & Monitoring**
- Problem: No operational visibility
- Solution: Health endpoints, structured logging  
- Impact: Production monitoring capability

### Iterative Enhancements

**Memory Tools Expansion**
- Current: Basic save/read memory
- Enhancement: List, delete, search, bulk operations
- Pattern: One tool per PR for incremental delivery

**File System Organization**
- Agent folders: Current state persistence
- Memory subfolder: File-based memory backup
- Log folder: Structured log file output

**Advanced Agent Features**
- Agent restart capability: Resume from saved state
- Agent communication: Direct agent-to-agent messaging
- Agent pools: Resource management and load balancing

### Security & Scaling

**Basic Security**
- Input validation: Prevent injection attacks
- Authentication: Basic token/API key support
- Authorization: Role-based access control

**Performance Optimization**
- Load testing: Concurrent agent limits
- Resource management: Memory/CPU monitoring
- Database optimization: Query performance, connection pooling

**Deployment Automation**
- Docker containers: Production deployment
- CI/CD pipelines: Automated releases  
- Infrastructure as Code: Deployment automation

