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
5. **Plugin Event Hooks**: Rich lifecycle hooks for advanced integration patterns
6. **IDE Integration**: Native VS Code integration with workspace-aware events
7. **Advanced Tools API**: Comprehensive tooling beyond basic MCP protocol

### Integration Hook Details

#### Plugin Event Hooks
The gemini-cli provides three core lifecycle hooks for deep integration:

1. **beforeRequest Hook**
   - **Purpose**: Intercept and mutate any API request before sending to Gemini
   - **Use Case**: Add coordination context, inject agent metadata, modify prompts
   - **Documentation**: [Event Hooks - beforeRequest](https://github.com/google-gemini/gemini-cli/blob/main/docs/extension.md#event-hooks–beforerequest)

2. **afterResponse Hook**
   - **Purpose**: Inspect or transform raw API responses immediately after arrival
   - **Use Case**: Extract coordination signals, log agent decisions, trigger workflows
   - **Documentation**: [Event Hooks - afterResponse](https://github.com/google-gemini/gemini-cli/blob/main/docs/extension.md#event-hooks–afterresponse)

3. **configChanged Hook**
   - **Purpose**: React in real-time when configuration files are updated
   - **Use Case**: Dynamic agent reconfiguration, hot-reload coordination settings
   - **Documentation**: [Event Hooks - configChanged](https://github.com/google-gemini/gemini-cli/blob/main/docs/extension.md#event-hooks–configchanged)

#### IDE Integration Capabilities
Native VS Code extension provides workspace-aware coordination:

- **Workspace Context Loading**: Access to recent files, selections, cursor position
- **Native Diff-View Previews**: Built-in accept/reject commands for agent changes
- **Custom VS Code Commands**: `gemini.cli.run`, `gemini.cli.acceptDiff`, etc.
- **Documentation**: [IDE Integration](https://github.com/google-gemini/gemini-cli/blob/main/docs/ide-integration.md)

#### Advanced MCP & Tools API
Beyond basic MCP protocol, gemini-cli provides comprehensive tooling:

- **MCP Protocol Extensions**: Enhanced context/asset sharing patterns
- **Tools API**: Filesystem, shell, HTTP, Google Search, memory tools
- **Documentation**: 
  - [MCP Protocol](https://github.com/google-gemini/gemini-cli/blob/main/docs/mcp-protocol.md)
  - [Tools API](https://github.com/google-gemini/gemini-cli/blob/main/docs/tools-api.md)

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

### Phase 1: Core MCP Tools Development (builds on existing AISwarm.Server)
- Add basic MCP tool implementations to existing server
- Implement `wait_for_next_event` with connection holding
- Add task management tools (`claim_task`, `report_completion`)
- Test tools individually with manual gemini-cli invocation

### Phase 2: Advanced Integration Hooks
- **Plugin Event Hooks Implementation**:
  - `beforeRequest`: Inject agent context into all API calls
  - `afterResponse`: Extract coordination signals from responses
  - `configChanged`: Support dynamic agent reconfiguration
- **VS Code Integration Setup**:
  - Configure workspace-aware context loading
  - Set up native diff-view previews for agent changes
  - Implement custom commands for agent coordination
- **Extended Tools API Integration**:
  - Leverage built-in filesystem, shell, HTTP tools
  - Integrate Google Search capabilities for research tasks
  - Use memory tools for persistent agent context

### Phase 3: AgentLauncher Integration
- Modify AgentLauncher to configure gemini-cli instead of custom approaches
- Generate appropriate settings.json configurations with hook setups
- Add agent persona initialization prompts
- Implement plugin and extension management
- Test single-agent coordination with full hook integration

### Phase 4: Multi-Agent Coordination with Advanced Features
- Test multiple agents coordinating through enhanced MCP tools
- Implement advanced task distribution using event hooks
- Add workspace-aware collaboration via VS Code integration
- Implement real-time coordination monitoring
- End-to-end workflow testing with all integration points

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
- [Gemini CLI Extension Hooks - beforeRequest](https://github.com/google-gemini/gemini-cli/blob/main/docs/extension.md#event-hooks–beforerequest)
- [Gemini CLI Extension Hooks - afterResponse](https://github.com/google-gemini/gemini-cli/blob/main/docs/extension.md#event-hooks–afterresponse)
- [Gemini CLI Extension Hooks - configChanged](https://github.com/google-gemini/gemini-cli/blob/main/docs/extension.md#event-hooks–configchanged)
- [Gemini CLI IDE Integration](https://github.com/google-gemini/gemini-cli/blob/main/docs/ide-integration.md)
- [Gemini CLI MCP Protocol](https://github.com/google-gemini/gemini-cli/blob/main/docs/mcp-protocol.md)
- [Gemini CLI Tools API](https://github.com/google-gemini/gemini-cli/blob/main/docs/tools-api.md)
- [Model Context Protocol Specification](https://modelcontextprotocol.io/)
- ADR-0003: Separate MCP Coordination Server Project
- [MCP C# SDK Documentation](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)

## Notes
This decision represents a significant simplification of our approach. By leveraging official tooling with native MCP support, we avoid complex custom implementations while achieving the same coordination goals through standard patterns.