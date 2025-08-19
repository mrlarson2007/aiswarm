# Design Refinement Questions - SQLite Task Coordination

**Status: Updated after MCP architecture research and standalone server decision**

Based on the refined understanding of Model Context Protocol and the decision to create a separate `AgentLauncher.CoordinationServer` project, many questions have been resolved.

## ✅ **RESOLVED DECISIONS**

### MCP Server Architecture
**RESOLVED**: Standalone MCP server project (`AgentLauncher.CoordinationServer`)
- Follows standard MCP patterns with separate server and client processes
- Clean separation of concerns between agent launching and coordination
- stdio transport for local development and testing

### Worktree Lifecycle Management
**RESOLVED**: Planner-controlled through MCP tools
- Planners use `create_worktree` MCP tool to create worktrees
- Workers are confined to assigned worktrees
- Planners handle integration/discard decisions

### Agent Process Management Authority
**RESOLVED**: Planner agents spawn workers via `spawn_agent` MCP tool
- Process IDs tracked in database for termination capability
- Parent-child relationship maintained for ownership validation

## 🚨 **FINAL DECISION: Manual Coordination Server Management**

**RESOLVED**: Option B - Manual coordination server startup

### **User Workflow:**
```bash
# 1. Start coordination server (manual or via VS Code)
dotnet run --project src/AgentLauncher.CoordinationServer

# 2. Launch agents that connect to running server
dotnet agentlauncher --persona planner --feature user-auth
dotnet agentlauncher --persona implementer --worktree user-auth-feature
```

### **Integration Options:**
- **VS Code Extension**: Can auto-start coordination server for development
- **Manual Start**: Users can start server explicitly for production use
- **CI/CD Integration**: Server can be started as part of automated workflows

### **Key Benefits:**
- **Clear Separation**: Server lifecycle independent from agent processes
- **Debugging**: Easier to monitor and debug server separately
- **Flexibility**: Multiple integration options (VS Code, manual, automated)
- **MCP Compliance**: Follows standard MCP server patterns
- **Development Experience**: Clear mental model of client-server architecture

## 🚀 **READY FOR TDD IMPLEMENTATION**

All critical architectural decisions are now resolved:

- ✅ **Multi-project structure**: Separate coordination server project
- ✅ **MCP architecture**: Standalone server with stdio transport  
- ✅ **Planner authority**: Worktree and agent lifecycle management
- ✅ **Server startup**: Manual management with integration flexibility

**Next step**: Begin TDD implementation following RED-GREEN-REFACTOR-COMMIT cycle!

## 1. Worktree Assignment Strategy

### Current Decision

- Agents can share worktrees when collaboration makes sense
- Tasks can specify preferred worktree or request new ones

### Outstanding Questions

1. **Worktree Lifecycle**: Who is responsible for creating/cleaning up worktrees?
   - Should planners manage worktree creation?
   - Should worktrees be cleaned up automatically when all agents leave?
   - How do we handle abandoned worktrees?

2. **Conflict Resolution**: What happens when multiple agents want to work in the same worktree simultaneously?
   - File locking strategies?
   - Git merge conflict handling?
   - Work coordination within shared worktrees?

3. **Worktree Naming**: How should we name worktrees for shared use?
   - `{repo}-feature-{name}` vs `{repo}-shared-{id}`?
   - Include agent types in naming? `{repo}-impl-review-{feature}`?

## 2. Agent Process Management

### Current Decision

- Planners can spawn and terminate worker agents
- Process IDs tracked in database for termination

### Outstanding Questions

1. **Agent Startup**: How should worker agents be launched?
   - Direct process execution vs CLI command?
   - Environment variable inheritance?
   - Working directory assignment?

2. **Graceful Shutdown**: How should agent termination work?
   - SIGTERM first, then SIGKILL after timeout?
   - Allow agents to finish current task before termination?
   - What cleanup is needed when agents are killed?

3. **Process Monitoring**: Beyond heartbeat, what process health checks?
   - CPU/Memory monitoring?
   - Process tree tracking (child processes)?
   - Restart policies for failed agents?

4. **Agent Permissions**: What authority should planners have?
   - Can any planner terminate any agent?
   - Should there be agent ownership/hierarchy validation?
   - Can agents refuse termination requests?

## 3. Database Design Details

### Current Decision

- SQLite database at `.aiswarm/coordination.db`
- Enhanced schema with process management fields

### Outstanding Questions

1. **Concurrency Handling**: How do we handle concurrent database access?
   - SQLite WAL mode configuration?
   - Connection pooling strategy?
   - Transaction boundary definitions?

2. **Database Migrations**: How do we evolve the schema?
   - Versioning strategy?
   - Automatic migration on startup?
   - Backward compatibility requirements?

3. **Data Retention**: How long should we keep historical data?
   - Completed task cleanup policies?
   - Agent session archival?
   - Log rotation strategies?

4. **Backup/Recovery**: How do we handle database corruption?
   - Automatic backup strategies?
   - Recovery procedures?
   - Data export/import capabilities?

## 4. MCP Server Architecture

### Current Decision

- MCP server provides coordination APIs
- Can be embedded or standalone process

### Outstanding Questions

1. **Process Model**: Embedded vs standalone trade-offs?
   - **Embedded**: Simpler deployment, shared lifecycle
   - **Standalone**: Better isolation, can survive agent crashes
   - Should we support both modes?

2. **Server Lifecycle**: How is the MCP server managed?
   - Auto-start when first agent launches?
   - Manual startup requirement?
   - Graceful shutdown coordination?

3. **Security Model**: How do we secure MCP server access?
   - Authentication required?
   - Authorization levels (planner vs worker permissions)?
   - Rate limiting per agent?

4. **API Versioning**: How do we handle API evolution?
   - Version negotiation between agents and server?
   - Backward compatibility guarantees?
   - Feature flags for new capabilities?

## 5. Task Execution Model

### Current Decision

- Tasks assigned to personas with dependency management
- Agents claim and execute tasks

### Outstanding Questions

1. **Task Context**: How much context should tasks contain?
   - Embedded instructions vs references to external files?
   - Git branch/commit references?
   - Environment variable specifications?

2. **Result Handling**: How should task results be structured?
   - Standardized result format vs free-form?
   - Artifact tracking (created files, git commits)?
   - Success/failure criteria definition?

3. **Task Scheduling**: Beyond dependencies, what scheduling rules?
   - Priority-based scheduling?
   - Agent capability matching?
   - Load balancing across agents?

4. **Error Recovery**: How should failed tasks be handled?
   - Automatic retry policies?
   - Manual intervention requirements?
   - Partial failure handling?

## 6. Development Experience

### Outstanding Questions

1. **Debugging Tools**: What tooling do we need for development?
   - Database inspection utilities?
   - Task execution tracing?
   - Agent communication logging?

2. **Local Development**: How should developers work with the system?
   - Development vs production database separation?
   - Test data seeding?
   - Mock/fake implementations for testing?

3. **CLI Interface**: What command-line tools should we provide?
   - Task queue inspection?
   - Agent management commands?
   - Database administration utilities?

## 7. Integration Concerns

### Outstanding Questions

1. **Existing Workflow Compatibility**: How does this integrate with current usage?
   - Can users still launch agents directly without coordination?
   - Migration path from current file-based personas?
   - Fallback behavior when coordination system is unavailable?

2. **External Tool Integration**: How does this work with other tools?
   - Git hooks integration?
   - CI/CD pipeline triggers?
   - IDE extensions and plugins?

3. **Performance Impact**: What are the performance implications?
   - Database query performance with many tasks?
   - Agent startup time overhead?
   - Network latency for MCP calls?

## Next Steps

Which of these question areas should we dive into first? I suggest we prioritize:

1. **Agent Process Management** - Critical for planner authority model
2. **Worktree Assignment Strategy** - Affects how agents collaborate  
3. **MCP Server Architecture** - Foundation for all coordination
4. **Database Design Details** - Performance and reliability foundation

What's your instinct on which area needs the most attention before we start implementing?
