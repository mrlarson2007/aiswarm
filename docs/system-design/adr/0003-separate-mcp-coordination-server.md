# ADR-0003: Separate MCP Coordination Server Project

## Status
Accepted

## Context

During the design of the SQLite-based task coordination system, we initially considered embedding MCP server capabilities directly into the existing `AgentLauncher` project. However, after reviewing the Model Context Protocol architecture and best practices, we identified significant concerns with this approach:

### Problems with Embedded Approach
1. **Mixed Responsibilities**: The `AgentLauncher` project is focused on launching individual agents with specific personas, while coordination is a completely different concern
2. **Deployment Complexity**: Mixing agent and server concerns would make it harder to deploy and scale the coordination infrastructure
3. **Testing Isolation**: Unit testing coordination logic would be complicated by agent launcher dependencies
4. **Process Management**: MCP servers should be independent processes that agents connect to, not embedded within agents
5. **Lifecycle Management**: Coordination server should have different startup/shutdown semantics than individual agents

### MCP Architecture Requirements
Based on Microsoft's MCP C# SDK documentation:
- MCP servers are standalone processes with their own lifecycle
- Clients connect to servers via transport mechanisms (stdio, SSE)
- Servers expose tools and resources through well-defined interfaces
- Clean separation between server and client responsibilities

## Decision

We will create a separate `AgentLauncher.CoordinationServer` project that implements a standalone MCP server for task coordination.

### Solution Structure
```
src/
├── AgentLauncher/                    # Existing agent launcher (MCP client)
│   ├── Program.cs                    # CLI interface for launching agents
│   ├── Services/                     # Agent-specific services
│   └── Resources/                    # Persona definitions
├── AgentLauncher.CoordinationServer/ # New MCP server project
│   ├── Program.cs                    # MCP server host
│   ├── Services/                     # Coordination services
│   ├── Tools/                        # MCP tool implementations
│   └── Data/                         # SQLite database access
└── AgentLauncher.Shared/             # Shared contracts and models
    ├── Models/                       # Shared data models
    └── Contracts/                    # Interface definitions
```

### Key Benefits
1. **Clean Separation of Concerns**: Agent launching vs coordination are separate projects
2. **Independent Deployment**: Can deploy coordination server separately from agents
3. **Testability**: Each project can be unit tested in isolation
4. **MCP Compliance**: Follows standard MCP server-client architecture
5. **Scalability**: Coordination server can be scaled independently

### Integration Points
- **Agent Launcher**: Becomes MCP client that connects to coordination server
- **Coordination Server**: Standalone MCP server with task management tools
- **Shared Library**: Common models and contracts used by both projects

## Consequences

### Positive
- **Cleaner Architecture**: Each project has a single, well-defined responsibility
- **Better Testing**: Isolated unit testing for coordination logic
- **MCP Standards Compliance**: Follows established MCP patterns
- **Future Flexibility**: Easier to add new agent types or coordination features
- **Development Experience**: Clearer project boundaries for developers

### Negative
- **Additional Complexity**: More projects to manage in the solution
- **Deployment Coordination**: Need to ensure coordination server is running before agents
- **Inter-process Communication**: Slight overhead compared to in-process coordination

### Mitigation Strategies
- **Auto-start**: Agent launcher can automatically start coordination server if not running
- **Health Checks**: Agents can verify coordination server availability before connecting
- **Shared Contracts**: Use shared library to ensure compatibility between projects
- **Documentation**: Clear setup instructions for developers

## Implementation Notes

### Coordination Server Features
- SQLite database for task and agent management
- MCP tools for worktree management, agent spawning, task coordination
- Health monitoring and agent lifecycle management
- Stdio transport for local development

### Agent Launcher Changes
- Add MCP client capabilities to connect to coordination server
- Modify startup to check for coordination server availability
- Add `--coordination-mode` flag to enable/disable coordination features
- Maintain backward compatibility for non-coordination usage

### Development Workflow

**Manual Coordination Server Management (Option B)**

```bash
# 1. Start coordination server (manual or via tooling)
dotnet run --project src/AgentLauncher.CoordinationServer

# 2. Launch agents that connect to running server
dotnet agentlauncher --persona planner --feature user-auth
dotnet agentlauncher --persona implementer --worktree user-auth-feature
```

**Integration Scenarios:**
- **Manual Development**: Developer starts server in terminal before launching agents
- **VS Code Integration**: Extension can auto-start server for development workflows
- **CI/CD Automation**: Server started as part of build/deployment pipelines

**Benefits of Manual Management:**
- Clear separation between server and client lifecycles
- Easier debugging and monitoring of coordination server
- Follows standard MCP server deployment patterns
- Flexibility for different integration scenarios

## Alternatives Considered

### Embedded MCP Server
**Rejected**: Would violate separation of concerns and make testing more complex

### Single Monolithic Project
**Rejected**: Would mix agent and coordination responsibilities, making the codebase harder to maintain

### External Coordination Service
**Rejected**: Adds unnecessary deployment complexity for a tool primarily used locally

## References
- [Build a Model Context Protocol (MCP) server in C#](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)
- [MCP C# SDK Repository](https://github.com/modelcontextprotocol/csharp-sdk)
- ADR-0002: Shared Context Between Agents (SQLite coordination system)