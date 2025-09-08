# Task: Implement IA2AService Interface and Service

**Date:** September 8, 2025  
**Status:** Ready for Implementation  
**Branch:** feature/a2a-service  
**Worktree:** a2a-service  
**Type:** Service Layer Implementation  

---

## Objective

Create a clean IA2AService interface and implementation for launching and managing A2A agents, following TDD approach with full unit test coverage.

## Scope

Interface design → Service implementation → Full unit test coverage → No external dependencies

## Test Scenario

```csharp
GIVEN: IA2AService is implemented
WHEN: I call LaunchAgentAsync with valid parameters
THEN: 
  - Agent process is started successfully
  - Agent configuration is applied correctly
  - Service returns agent instance info
  - Service can query agent status
  - Service can stop agent gracefully
```

## Implementation Tasks

### 1. Define IA2AService interface

- **Location:** `src/AISwarm.Infrastructure/Services/IA2AService.cs`
- **Methods:**
  - `Task<A2AAgentInstance> LaunchAgentAsync(A2AAgentConfig config)`
- **Models:** Supporting models for config and instance (simplified scope)

### 2. Create supporting models

- **Location:** `src/AISwarm.Infrastructure/Models/A2A/`
- **Models:**
  - `A2AAgentConfig` - Launch configuration (name, port, capabilities, etc.)
  - `A2AAgentInstance` - Running agent info (id, process, url, etc.)
  - `A2AAgentStatus` - Agent health status (running, stopped, error, etc.)
- **Validation:** Add data annotations for required fields

### 3. Implement A2AService

- **Location:** `src/AISwarm.Infrastructure/Services/A2AService.cs`
- **Dependencies:** 
  - `IProcessLauncher` (existing) - for launching agent processes
  - `ILogger<A2AService>` - for logging
  - `ITimeService` (existing) - for timestamps
- **Functionality:**
  - Launch AISwarm.TestAgent in A2A mode  
  - Return agent instance with basic info
  - Simple process launching only

### 4. Write comprehensive unit tests

- **Location:** `tests/AISwarm.Tests/Infrastructure/Services/A2AServiceTests.cs`
- **Test classes (nested):**
  - `LaunchAgentTests` - Test agent launching scenarios
  - `AgentStatusTests` - Test status checking functionality
  - `StopAgentTests` - Test agent termination
  - `GetRunningAgentsTests` - Test agent enumeration
- **Test coverage:** All public methods, error cases, edge conditions
- **Mocking:** Use existing test doubles (FakeProcessLauncher, etc.)

### 5. Add integration tests

- **Location:** `tests/AISwarm.Tests/Infrastructure/Services/A2AServiceIntegrationTests.cs`
- **Tests:**
  - End-to-end agent launch and stop
  - Real process interaction (using TestAgent)
  - HTTP health check validation
- **Setup:** Use real ProcessLauncher with TestAgent

### 6. Add service registration

- **Location:** `src/AISwarm.Infrastructure/ServiceRegistration.cs`
- **Registration:** Add `services.AddScoped<IA2AService, A2AService>()`
- **Configuration:** Add any needed configuration options

## TDD Approach

1. **RED:** Write failing test for IA2AService interface
2. **GREEN:** Create minimal interface and models  
3. **RED:** Write failing test for LaunchAgentAsync
4. **GREEN:** Implement minimal agent launching
5. **REFACTOR:** Clean up implementation
6. **COMMIT:** Complete service with tests

## Architecture Decisions

- **Clean Interface:** Focus on essential A2A agent lifecycle operations
- **Process-Based:** Launch TestAgent as separate process, not in-process
- **HTTP Health Checks:** Use agent's A2A endpoints for status validation
- **Memory Tracking:** Track agents in ConcurrentDictionary, no database
- **Dependency Injection:** Follow existing AISwarm DI patterns

## File Structure

```
src/AISwarm.Infrastructure/
├── Services/
│   ├── IA2AService.cs           # New interface
│   └── A2AService.cs            # New implementation
├── Models/A2A/                  # New folder
│   ├── A2AAgentConfig.cs        # Launch configuration
│   ├── A2AAgentInstance.cs      # Running agent info
│   └── A2AAgentStatus.cs        # Status enumeration
└── ServiceRegistration.cs       # Enhanced registration

tests/AISwarm.Tests/Infrastructure/Services/
├── A2AServiceTests.cs           # Unit tests
└── A2AServiceIntegrationTests.cs # Integration tests
```

## Dependencies

- **Existing:** IProcessLauncher, ILogger, ITimeService
- **New:** None (uses existing AISwarm infrastructure)

## Success Criteria

- ✅ IA2AService interface is well-designed and clean
- ✅ A2AService implementation follows existing patterns
- ✅ All unit tests pass with >95% code coverage
- ✅ Integration tests validate real agent launch/stop
- ✅ Service is registered in DI container
- ✅ No breaking changes to existing code

## Sample Interface

```csharp
public interface IA2AService
{
    Task<A2AAgentInstance> LaunchAgentAsync(A2AAgentConfig config);
}

public class A2AAgentConfig
{
    public required string AgentName { get; set; }
    public required string[] Capabilities { get; set; }
    public int Port { get; set; } = 0; // 0 = auto-assign
    public string? Description { get; set; }
    public string? WorktreeName { get; set; }
}
```

---

**Next Steps:** Begin TDD implementation in worktree `a2a-service`