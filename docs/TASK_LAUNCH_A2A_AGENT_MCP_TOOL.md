# Task: Implement LaunchA2AAgent MCP Tool

**Date:** September 8, 2025  
**Status:** Ready for Implementation  
**Branch:** feature/a2a-mcp-tool  
**Worktree:** a2a-mcp-tool  
**Type:** MCP Tool Implementation  

---

## Objective

Create a LaunchA2AAgent MCP tool that uses the IA2AService interface to launch A2A agents, with full unit test coverage using a fake service implementation.

## Scope

MCP tool → IA2AService integration → Fake service for testing → Full unit test coverage

## Test Scenario

```csharp
GIVEN: LaunchA2AAgent MCP tool is implemented
WHEN: I call the tool with valid parameters
THEN: 
  - IA2AService.LaunchAgentAsync is called with correct config
  - Agent is launched successfully
  - Tool returns success result with agent details
  - Error cases are handled gracefully
```

## Implementation Tasks

### 1. Create LaunchA2AAgentMcpTool

- **Location:** `src/AISwarm.Server/McpTools/LaunchA2AAgentMcpTool.cs`
- **Parameters:**
  - `agentName` (required) - Agent identifier
  - `capabilities` (optional) - Comma-separated capabilities, default: "code-generation"
  - `description` (optional) - Agent description
  - `port` (optional) - HTTP port, default: auto-assign
  - `worktreeName` (optional) - Git worktree name
- **Dependencies:** `IA2AService` (injected)

### 2. Create result models

- **Location:** `src/AISwarm.Server/Entities/LaunchA2AAgentResult.cs`
- **Properties:**
  - `Success` (bool) - Operation success
  - `AgentId` (string?) - Launched agent ID
  - `AgentUrl` (string?) - Agent A2A endpoint URL
  - `ErrorMessage` (string?) - Error details if failed
- **Base class:** Extend existing `Result<T>` pattern

### 3. Add parameter validation

- **Validation rules:**
  - AgentName: required, valid identifier format
  - Capabilities: valid capability names
  - Port: valid port range if specified
  - WorktreeName: valid git worktree name if specified
- **Error handling:** Return descriptive validation errors

### 4. Create FakeA2AService for testing

- **Location:** `tests/AISwarm.Tests/TestDoubles/FakeA2AService.cs`
- **Purpose:** Test double that implements IA2AService
- **Functionality:**
  - Configurable success/failure responses
  - Track method calls for verification
  - Simulate different agent launch scenarios
- **Pattern:** Follow existing test double patterns

### 5. Write comprehensive unit tests

- **Location:** `tests/AISwarm.Tests/Server/McpTools/LaunchA2AAgentMcpToolTests.cs`
- **Test classes (nested):**
  - `SuccessfulLaunchTests` - Happy path scenarios
  - `ValidationTests` - Parameter validation
  - `ErrorHandlingTests` - Service failure scenarios
  - `A2AServiceIntegrationTests` - Service interaction verification
- **Test coverage:** All parameters, validation, error cases
- **Mocking:** Use FakeA2AService

### 6. Add MCP tool registration

- **Location:** `src/AISwarm.Server/Program.cs`
- **Registration:** Ensure LaunchA2AAgentMcpTool is registered
- **Verification:** Tool appears in MCP tool list

## TDD Approach

1. **RED:** Write failing test for MCP tool with FakeA2AService
2. **GREEN:** Create minimal MCP tool class and result model
3. **RED:** Write failing test for parameter validation
4. **GREEN:** Add parameter validation logic
5. **RED:** Write failing test for IA2AService integration
6. **GREEN:** Add service call implementation
7. **RED:** Write failing test for error handling
8. **GREEN:** Add error handling and logging
9. **REFACTOR:** Clean up implementation
10. **COMMIT:** Complete MCP tool with tests

## Architecture Decisions

- **Interface Dependency:** Depend only on IA2AService interface, not implementation
- **Fake for Testing:** Use fake service implementation for isolated unit tests
- **Parameter Validation:** Validate inputs before calling service
- **Error Handling:** Convert service exceptions to user-friendly messages
- **Existing Patterns:** Follow existing MCP tool patterns and naming

## File Structure

```
src/AISwarm.Server/McpTools/
└── LaunchA2AAgentMcpTool.cs     # New MCP tool

src/AISwarm.Server/Entities/
└── LaunchA2AAgentResult.cs      # New result model

tests/AISwarm.Tests/
├── TestDoubles/
│   └── FakeA2AService.cs        # New test double
└── Server/McpTools/
    └── LaunchA2AAgentMcpToolTests.cs # New unit tests
```

## Dependencies

- **Interface:** IA2AService (from Task 1)
- **Existing:** ILogger, existing MCP infrastructure
- **Testing:** FakeA2AService (created in this task)

## Success Criteria

- ✅ LaunchA2AAgentMcpTool follows existing MCP tool patterns
- ✅ FakeA2AService provides reliable test isolation
- ✅ All unit tests pass with >95% code coverage
- ✅ Parameter validation handles all edge cases
- ✅ Error handling provides clear user feedback
- ✅ Tool is properly registered and discoverable

## Sample Usage

```csharp
// MCP Tool call
var result = await mcpTool.LaunchA2AAgentAsync(
    agentName: "test-agent",
    capabilities: "code-generation,analysis",
    description: "Test agent for A2A protocol",
    port: 3001,
    worktreeName: "test-worktree"
);

// Expected result
{
    "Success": true,
    "AgentId": "agent-12345",
    "AgentUrl": "http://localhost:3001",
    "ErrorMessage": null
}
```

## Sample FakeA2AService

```csharp
public class FakeA2AService : IA2AService
{
    public bool ShouldSucceed { get; set; } = true;
    public List<A2AAgentConfig> LaunchCalls { get; } = new();
    
    public Task<A2AAgentInstance> LaunchAgentAsync(A2AAgentConfig config)
    {
        LaunchCalls.Add(config);
        
        if (!ShouldSucceed)
            throw new InvalidOperationException("Simulated failure");
            
        return Task.FromResult(new A2AAgentInstance
        {
            AgentId = $"agent-{Guid.NewGuid():N}",
            AgentUrl = $"http://localhost:{config.Port}",
            Status = A2AAgentStatus.Running
        });
    }
}
```

---

**Next Steps:** Begin TDD implementation in worktree `a2a-mcp-tool`