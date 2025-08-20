# ADR-0004: Use Official Gemini CLI with MCP Integration

## Status
Accepted

## Context

During development of the AISwarm coordination system, we investigated how to enable real-time coordination between multiple AI agents. Initially, we explored complex approaches involving custom Gemini API clients with interceptors and Server-Sent Events (SSE) for event-driven coordination.

### Discovery of Official Gemini CLI
After consultation with Gemini AI, we discovered the official `google-gemini/gemini-cli` tool, which provides native Model Context Protocol (MCP) support specifically designed for the type of agent coordination we're building.

### Key Capabilities Confirmed
1. **MCP Client Support**: Gemini CLI acts as an MCP client that can connect to custom MCP servers
2. **Custom Tool Integration**: Supports custom tools with `@server-name tool-name` syntax
3. **Configuration Management**: Uses `~/.gemini/settings.json` for MCP server configuration
4. **Request/Response Model**: Standard MCP request/response pattern (no complex SSE needed)

### Previous Complexity vs. Simple Solution
**What we almost built (unnecessarily complex):**
- Custom Gemini API client with interceptors
- Server-Sent Events (SSE) streaming for real-time coordination  
- Complex state management between stateless LLM and stateful coordination
- Custom event loop and "wait_for_next_event" tool handling

**What actually works (simple and standard):**
- Use official gemini-cli as-is
- Standard MCP request/response tools
- Simple configuration via settings.json
- Let Gemini drive coordination through MCP tool calls

## Decision

We will integrate with the official `google-gemini/gemini-cli` tool rather than building custom API clients or complex event systems.

### Architecture Overview
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   AgentLauncher │    │  gemini-cli     │    │ AISwarm.Server  │
│                 │───▶│                 │◄──▶│                 │
│ (Process Mgmt)  │    │ (MCP Client)    │    │ (MCP Server)    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                ▲                       ▲
                                │                       │
                                ▼                       ▼
                         Gemini LLM              Custom MCP Tools
                        (via @aiswarm)          (@aiswarm commands)
```

### MCP Tools to Implement
```csharp
// Agent lifecycle tools
@aiswarm register_agent
@aiswarm update_heartbeat
@aiswarm get_agent_status

// Task coordination tools  
@aiswarm wait_for_next_event    // Blocks until events available
@aiswarm claim_task
@aiswarm report_task_completion
@aiswarm check_for_tasks

// Worktree management tools
@aiswarm create_worktree
@aiswarm list_worktrees
```

### Configuration Approach
Each agent instance will have gemini-cli configured via `~/.gemini/settings.json`:
```json
{
  "servers": {
    "aiswarm": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/AISwarm.Server"]
    }
  }
}
```

### Agent Coordination Workflow
1. **Agent Initialization**: 
   - AgentLauncher starts gemini-cli with specific persona context
   - Gemini calls `@aiswarm register_agent` to join the coordination system
   
2. **Event-Driven Coordination**:
   - Gemini calls `@aiswarm wait_for_next_event` 
   - AISwarm.Server holds the connection until events occur (new tasks, etc.)
   - When events occur, server responds with event data
   - Gemini decides next action (claim task, check status, etc.)

3. **Task Execution**:
   - Gemini calls `@aiswarm claim_task` for available work
   - Executes task using existing AgentLauncher capabilities  
   - Calls `@aiswarm report_task_completion` when done

## Consequences

### Positive
- **Standards Compliance**: Uses official tooling with native MCP support
- **Simplicity**: No custom API clients or complex event handling needed
- **Maintainability**: Leverages official tools instead of custom implementations
- **Reliability**: Official gemini-cli handles API calls, authentication, etc.
- **Future-Proof**: Benefits from updates to official tooling

### Negative  
- **Dependency**: Requires gemini-cli to be installed on agent machines
- **Configuration**: Need to manage settings.json for each agent instance
- **Less Control**: Cannot customize gemini-cli behavior beyond MCP tools

### Mitigation Strategies
- **Installation Scripts**: Provide setup scripts to install gemini-cli
- **Configuration Templates**: Generate settings.json automatically
- **Fallback Modes**: Maintain non-coordination mode for development

## Implementation Plan

### Phase 1: MCP Tools Development (builds on existing AISwarm.Server)
- Add MCP tool implementations to existing server
- Implement `wait_for_next_event` with connection holding
- Add task management tools
- Test tools individually

### Phase 2: AgentLauncher Integration
- Modify AgentLauncher to use gemini-cli instead of custom approaches
- Generate appropriate settings.json configurations
- Add agent persona initialization prompts
- Test single-agent coordination

### Phase 3: Multi-Agent Coordination
- Test multiple agents coordinating through MCP tools
- Implement task distribution and claiming logic
- Add monitoring and health checks
- End-to-end workflow testing

## Alternatives Considered

### Custom Gemini API Client with Interceptors
**Rejected**: Unnecessarily complex, requires reimplementing functionality that already exists in gemini-cli

### Server-Sent Events (SSE) Streaming
**Rejected**: More complex than needed, standard MCP request/response is sufficient for coordination

### Polling-Based Coordination
**Rejected**: Less efficient than the `wait_for_next_event` pattern that holds connections

### Different LLM CLI Tools
**Rejected**: Gemini CLI has the specific MCP features we need

## References
- [google-gemini/gemini-cli Repository](https://github.com/google-gemini/gemini-cli)
- [Model Context Protocol Specification](https://modelcontextprotocol.io/)
- ADR-0003: Separate MCP Coordination Server Project
- [MCP C# SDK Documentation](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)

## Notes
This decision represents a significant simplification of our approach. By leveraging official tooling with native MCP support, we avoid complex custom implementations while achieving the same coordination goals through standard patterns.