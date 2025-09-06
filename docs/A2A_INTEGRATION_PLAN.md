# A2A Integration Plan

## Implementation Phases for Agent-to-Agent Communication

**Date:** September 5, 2025  
**Status:** Integration Roadmap  
**Branch:** a2a-design-docs  

---

## Overview

This document outlines the phased approach for integrating Agent-to-Agent (A2A) communication protocol into the existing AgentSwarm system.

## Phase 1: Foundation Setup

### Database Schema Extensions

**Priority**: High  
**Dependencies**: None  

Add A2A support to existing database schema without breaking changes.

- Add A2A URL and capabilities columns to Agents table
- Create TaskDependencies table for workflow support
- Add optional AgentCommunicationLog table for monitoring
- Extend WorkItems table with dependency tracking

### A2A Client Service Integration

**Priority**: High  
**Dependencies**: Database schema extensions  

Add A2A .NET SDK integration to AgentSwarm server.

- Install A2A .NET SDK 0.3.1-preview
- Create A2AClientService wrapper
- Add service registration to DI container
- Implement basic agent discovery

### MCP Tools Enhancement

**Priority**: Medium  
**Dependencies**: A2A client service  

Enhance existing MCP tools with A2A capabilities.

- Update `create_task` to support task dependencies and A2A dispatch
- Enhance `list_agents` to include A2A metadata
- Add `agent_message` tool for direct agent communication
- Update `get_task_status` to use A2A when available
- Add `coordinate_agents` for workflow management

## Phase 2: Agent Configuration System

### Git Worktree Management

**Priority**: High  
**Dependencies**: None  

Implement agent isolation using git worktrees.

- Create A2AAgentLauncher service
- Implement git worktree creation/cleanup
- Add persona file management
- Create agent configuration file generation

### Configuration File Structure

**Priority**: Medium  
**Dependencies**: Git worktree management  

Define standardized agent configuration format.

- AgentSwarm connection settings (agent ID, server URL)
- Gemini configuration (persona, model, temperature)
- A2A settings (enabled flag, port assignment)
- Persona file path and system prompt integration

### Launch Command Integration

**Priority**: Medium  
**Dependencies**: Configuration system  

Update agent launch commands to use A2A configuration.

- Modify `launch_agent` MCP tool
- Add configuration file parameter support
- Implement agent startup monitoring

## Phase 3: JavaScript A2A Package

### NPM Package Development

**Priority**: High  
**Dependencies**: None (can be developed in parallel)  

Create `@aiswarm/a2a-agent` JavaScript package.

- Interface definitions for agent host and Gemini processor
- Service implementations for A2A communication
- Model classes for tasks and messages
- Main entry point and documentation

### Gemini CLI Integration

**Priority**: High  
**Dependencies**: NPM package  

Integrate A2A capabilities into gemini-cli agents.

- Add @a2aproject/a2a-node dependency
- Implement A2AAgentHost wrapper
- Add task processing pipeline
- Implement status reporting via A2A

### Agent Card Implementation

**Priority**: Medium  
**Dependencies**: Gemini CLI integration  

Implement well-known URI agent discovery.

- Create agent card endpoint
- Implement capabilities and skills metadata
- Add health check endpoint

## Phase 4: Task Dispatch Service

### A2A Task Dispatcher

**Priority**: High  
**Dependencies**: All previous phases  

Implement push-based task delivery.

- Create A2ATaskDispatchService
- Implement agent discovery logic
- Add task routing based on persona/capabilities
- Implement fallback to memory channel system

### Agent Health Monitoring

**Priority**: Medium  
**Dependencies**: Task dispatcher  

Add agent health checking and discovery.

- Periodic agent health checks
- Agent capability discovery
- Load balancing based on agent availability

### Direct Communication Support

**Priority**: Low  
**Dependencies**: Task dispatcher  

Enable agent-to-agent direct communication.

- Agent discovery via well-known URIs
- Direct messaging between agents
- Workflow coordination tools

## Phase 5: Testing and Validation

### Unit Testing

**Priority**: High  
**Dependencies**: All implementation phases  

Comprehensive test coverage for A2A components.

- A2A client service tests
- Configuration management tests
- Task dispatch logic tests
- Agent discovery tests

### Integration Testing

**Priority**: High  
**Dependencies**: Unit testing  

End-to-end testing scenarios.

- Single agent task execution
- Multi-agent parallel processing
- Agent failure scenarios
- Network failure handling

### Load Testing

**Priority**: Medium  
**Dependencies**: Integration testing  

Performance validation.

- 10+ concurrent agents
- High-frequency task dispatch
- Agent discovery performance
- Memory channel fallback testing

## Migration Strategy

### Backward Compatibility

Maintain full compatibility with existing system during transition.

- Keep all existing MCP tools working unchanged
- Maintain current memory channel event system
- Support non-A2A agents indefinitely
- No breaking changes to existing APIs

### Rollout Approach

Gradual introduction of A2A capabilities.

1. **Silent Integration**: Add A2A support without changing behavior
2. **Opt-in A2A**: Allow agents to register A2A capabilities
3. **Automatic Detection**: Prefer A2A when available, fallback to memory channels
4. **Full A2A**: All new agents use A2A by default

### Monitoring and Observability

Track adoption and performance during rollout.

- A2A vs memory channel usage metrics
- Task completion times comparison
- Agent discovery success rates
- Communication failure rates

## Risk Mitigation

### Technical Risks

- **A2A SDK Stability**: Preview SDK may have breaking changes
  - Mitigation: Wrapper service to isolate A2A SDK dependencies
- **Network Failures**: A2A relies on HTTP communication
  - Mitigation: Robust fallback to existing memory channel system
- **Agent Discovery**: Well-known URI may fail
  - Mitigation: Cache known agents, graceful degradation

### Operational Risks

- **Configuration Complexity**: Multiple configuration formats
  - Mitigation: Automated configuration generation, validation
- **Git Worktree Management**: File system complexity
  - Mitigation: Cleanup automation, monitoring
- **Agent Lifecycle**: Process management complexity
  - Mitigation: Health monitoring, automatic restart

## Dependencies

### External Dependencies

- **A2A .NET SDK**: 0.3.1-preview
- **@a2aproject/a2a-node**: JavaScript A2A library
- **Git**: For worktree management
- **Node.js**: For Gemini CLI agents

### Internal Dependencies

- **Entity Framework Core**: Database schema management
- **MediatR**: Command/query handling
- **MCP Server**: Tool integration
- **Memory Channel System**: Fallback communication

## Success Criteria

### Phase Completion Criteria

Each phase must meet specific criteria before proceeding:

1. **Phase 1**: Database extensions deployed, A2A client service operational
2. **Phase 2**: Agent configuration system working, git worktree management stable
3. **Phase 3**: JavaScript package published, Gemini CLI integration functional
4. **Phase 4**: Task dispatch working, agent discovery operational
5. **Phase 5**: All tests passing, performance validated

### System Integration Criteria

- No degradation of existing functionality
- Successful task execution via A2A
- Agent discovery working reliably
- Fallback to memory channels when needed
- Configuration system generating valid agent configs

## Documentation Requirements

### Technical Documentation

- A2A API documentation
- Configuration file schema
- Agent card specification
- Error handling guidelines

### User Documentation

- Agent launch procedures
- A2A agent development guide
- Troubleshooting guide
- Migration instructions
