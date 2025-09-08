# Task: Launch A2A Agent Vertical Slice

**Date:** September 6, 2025  
**Status:** Planned - Ready for Implementation  
**Branch:** feature/a2a-integration  
**Type:** Vertical Slice Implementation  

---

## Objective

Implement the complete "Launch an A2A Agent" vertical slice functionality using TDD approach (RED-GREEN-REFACTOR-COMMIT).

## Scope

End-to-end flow: Launch A2A agent → Discovery → Database registration → MCP tool visibility

## Test Scenario

```
GIVEN: AgentSwarm is running
WHEN: I launch an A2A test agent with persona "test-agent"
THEN: 
  - Agent is registered in database with A2A properties
  - Agent card is discoverable at /.well-known/agent.json
  - Agent appears in list_agents MCP tool with A2A capabilities
  - Agent status shows as "Running"
```

## Implementation Tasks

### 1. Create AISwarm.TestAgent project

- **Location:** `tests/AISwarm.TestAgent/`
- **Type:** Console application
- **Purpose:** Simple C# test agent that implements A2A protocol
- **Requirements:**
  - Accept same arguments as gemini-cli for compatibility
  - Expose `/.well-known/agent.json` endpoint
  - Support A2A protocol basics (agent card, simple responses)
  - Use configurable port (default: auto-assign)

### 2. Add A2A database schema extensions

- **Location:** `AISwarm.DataLayer/Entities/Agent.cs`
- **Properties to add:**
  - `A2AUrl` (string) - A2A endpoint URL
  - `A2ACapabilities` (string) - JSON capabilities
  - `A2ASkills` (string) - JSON skills array
  - `LastHealthCheck` (DateTime) - Health monitoring
- **Migration:** Create EF Core migration for schema changes

### 3. Implement A2A agent discovery service

- **Location:** `AISwarm.Infrastructure/Services/`
- **Interface:** `IA2ADiscoveryService`
- **Implementation:** `A2ADiscoveryService`
- **Functionality:**
  - Discover agents via `/.well-known/agent.json`
  - Parse agent cards and store A2A properties in database
  - Health checking capabilities

### 4. Create launch_a2a_agent MCP tool

- **Location:** `AISwarm.Server/McpTools/`
- **Class:** `A2AAgentManagementMcpTool` (new tool, separate from existing)
- **Method:** `LaunchA2AAgentAsync`
- **Parameters:**
  - `persona` (required) - Agent persona
  - `description` (required) - Task description
  - `port` (optional) - A2A endpoint port
  - `model` (optional) - Model configuration
  - `worktreeName` (optional) - Git worktree name
  - `yolo` (optional) - Bypass prompts
- **Flow:**
  1. Launch test agent process with A2A enabled
  2. Wait for agent startup
  3. Discover A2A capabilities via `/.well-known/agent.json`
  4. Register agent in database with A2A properties

### 5. Update list_agents MCP tool

- **Location:** `AISwarm.Server/McpTools/AgentManagementMcpTool.cs`
- **Enhancement:** Add A2A properties to agent listing
- **New fields in response:**
  - `A2AEnabled` (boolean)
  - `A2AUrl` (string)
  - `A2ACapabilities` (array)
  - `A2ASkills` (array)
  - `LastHealthCheck` (DateTime)

### 6. Write high-level integration test

- **Location:** `tests/AISwarm.Tests/Integration/`
- **Class:** `LaunchA2AAgentIntegrationTests`
- **Test:** `WhenLaunchingA2AAgent_ShouldCompleteFullDiscoveryFlow`
- **Verification:**
  - Agent process starts successfully
  - A2A discovery completes
  - Database contains agent with A2A properties
  - `list_agents` returns A2A-enabled agent
  - Agent status is "Running"

## TDD Approach

1. **RED:** Write failing integration test first
2. **GREEN:** Implement minimal code to make test pass:
   - Create test agent project
   - Add database schema
   - Implement discovery service
   - Create MCP tool
   - Update list_agents
3. **REFACTOR:** Clean up implementation while keeping tests green
4. **COMMIT:** Commit working vertical slice

## Architecture Decisions

- **Separate MCP Tool:** Keep existing `launch_agent` clean, create new `launch_a2a_agent` tool
- **C# Test Agent:** Use C# for easier integration with .NET test suite (vs JavaScript)
- **Automatic Discovery:** Built into launch flow for atomic operation
- **Database Extensions:** Extend existing Agent entity rather than separate A2A table
- **High-Level Testing:** Test complete flow to avoid coupling with internal implementation

## Dependencies

- A2A .NET SDK (for future phases, not needed for test agent)
- Entity Framework Core (schema migrations)
- ASP.NET Core (for test agent HTTP endpoints)
- Existing AgentSwarm infrastructure (database, MCP tools, services)

## Success Criteria

- Integration test passes end-to-end
- Test agent can be launched via MCP tool
- A2A discovery works correctly
- Database properly stores A2A metadata
- `list_agents` shows A2A capabilities
- No breaking changes to existing functionality

---

**Next Steps:** Begin TDD implementation starting with integration test (RED phase)
