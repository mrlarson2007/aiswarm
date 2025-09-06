# A2A System Design

## Agent-to-Agent Communication for AgentSwarm

**Date:** September 5, 2025  
**Status:** System Design  
**Branch:** a2a-design-docs  

> **Note**: This document provides high-level system architecture and design patterns. For detailed technical implementation, see [A2A Implementation Guide](A2A_IMPLEMENTATION_GUIDE.md). For integration planning, see [A2A Integration Plan](A2A_INTEGRATION_PLAN.md).

---

## Architecture Overview

AgentSwarm integrates A2A protocol to enable direct agent-to-agent communication while maintaining existing MCP orchestration capabilities.

```mermaid
graph TB
    subgraph "AgentSwarm Server"
        MCP[MCP Server]
        A2AClient[A2A Client Service]
        TaskMgr[Task Manager]
        AgentReg[Agent Registry]
        DB[(Database)]
    end
    
    subgraph "A2A Agent 1"
        Gemini1[Gemini CLI]
        A2AHost1[A2A Agent Host]
        Persona1[Persona: Implementer]
    end
    
    subgraph "A2A Agent 2"
        Gemini2[Gemini CLI]
        A2AHost2[A2A Agent Host]
        Persona2[Persona: Reviewer]
    end
    
    subgraph "Non-A2A Agent"
        Gemini3[Gemini CLI]
        MCP3[MCP Tools Only]
    end
    
    subgraph "User Interfaces"
        VSCode[VS Code MCP]
        GeminiCLI[Gemini CLI User]
    end
    
    %% User interactions
    VSCode --> MCP
    GeminiCLI --> MCP
    
    %% AgentSwarm internal connections
    MCP --> TaskMgr
    MCP --> AgentReg
    TaskMgr --> A2AClient
    A2AClient --> AgentReg
    TaskMgr --> DB
    AgentReg --> DB
    
    %% A2A direct communication
    A2AClient -.->|Task Push| A2AHost1
    A2AClient -.->|Task Push| A2AHost2
    A2AHost1 -.->|Status Update| A2AClient
    A2AHost2 -.->|Status Update| A2AClient
    A2AHost1 -.->|Direct Message| A2AHost2
    A2AHost2 -.->|Direct Message| A2AHost1
    
    %% A2A Agent internal
    A2AHost1 --> Gemini1
    A2AHost2 --> Gemini2
    Gemini1 --> Persona1
    Gemini2 --> Persona2
    
    %% Traditional MCP communication (fallback)
    MCP -.->|Memory Channels| MCP3
    MCP -.->|Memory Channels| A2AHost1
    MCP -.->|Memory Channels| A2AHost2
    
    %% Agent discovery
    A2AClient -.->|/.well-known/agent.json| A2AHost1
    A2AClient -.->|/.well-known/agent.json| A2AHost2
    
    %% Styling
    classDef agentswarm fill:#e1f5fe,stroke:#01579b,stroke-width:2px
    classDef a2aagent fill:#f3e5f5,stroke:#4a148c,stroke-width:2px
    classDef traditional fill:#fff3e0,stroke:#e65100,stroke-width:2px
    classDef user fill:#e8f5e8,stroke:#2e7d32,stroke-width:2px
    
    class MCP,A2AClient,TaskMgr,AgentReg,DB agentswarm
    class Gemini1,A2AHost1,Persona1,Gemini2,A2AHost2,Persona2 a2aagent
    class Gemini3,MCP3 traditional
    class VSCode,GeminiCLI user
```

### Current System
- **AgentSwarm Server**: MCP orchestrator with memory channels for agent communication
- **Gemini CLI Agents**: JavaScript-based agents that poll for tasks via MCP tools
- **Communication**: Polling-based via MCP tools and memory channels

### Enhanced System with A2A
- **AgentSwarm Server**: MCP orchestrator + A2A client for direct agent communication
- **Gemini CLI Agents**: JavaScript agents enhanced with optional A2A server capabilities
- **Communication**: Direct agent-to-agent push notifications + MCP fallback

## Core Components

### 1. A2A Client Service (AgentSwarm Server)

The A2A Client Service enables AgentSwarm to communicate directly with A2A-capable agents. Key responsibilities:

- **Agent Discovery**: Locate and verify A2A agents via well-known URIs
- **Direct Communication**: Send messages and tasks directly to agents
- **Task Management**: Create, monitor, and cancel tasks on remote agents
- **Health Monitoring**: Track agent availability and capabilities
- **Best Agent Selection**: Find optimal agents for specific tasks based on persona and workload

### 2. A2A Agent Host (Gemini CLI)

The A2A Agent Host transforms standard Gemini CLI agents into A2A-capable agents. Core functions:

- **A2A Server**: Hosts A2A endpoints for receiving tasks and messages
- **Task Processing**: Integrates A2A tasks with Gemini processing pipeline
- **Status Reporting**: Provides real-time status updates via A2A callbacks
- **Agent Card**: Exposes agent capabilities and metadata for discovery
- **Configuration Integration**: Uses AgentSwarm-provided configuration files

### 3. Enhanced MCP Tools

- **create_task**: Support task dependencies and A2A dispatch
- **list_agents**: A2A agent discovery with metadata
- **agent_message**: Direct A2A agent communication
- **get_task_status**: A2A status checking with MCP fallback

### 4. Agent Configuration System

AgentSwarm creates isolated environments for each agent with comprehensive configuration:

- **Git Worktrees**: Isolated file system environments for each agent instance
- **Persona Integration**: Agent-specific persona files and system prompts
- **A2A Settings**: Port assignments and A2A capabilities configuration
- **AgentSwarm Integration**: Connection details and agent identification
- **Gemini Configuration**: Model selection, temperature, and processing parameters

## Database Schema Extensions

### Agent Metadata

Extends existing Agents table with A2A-specific information:
- A2A endpoint URLs for direct communication
- Agent capabilities and skills (JSON metadata)
- Health check timestamps for monitoring

### Task Dependencies

New table supporting workflow orchestration:
- Parent-child task relationships
- Dependency types (blocking, parallel, optional)
- Creation timestamps for tracking

### Communication Logging

Optional monitoring table for A2A interactions:
- Agent communication events
- Message types and content
- Workflow correlation identifiers

## Communication Patterns

```mermaid
sequenceDiagram
    participant User as User/VS Code
    participant MCP as AgentSwarm MCP
    participant A2A as A2A Client Service
    participant DB as Database
    participant Agent1 as A2A Agent 1
    participant Agent2 as A2A Agent 2
    
    Note over User,Agent2: Task Creation & Distribution
    User->>MCP: create_task(description, persona)
    MCP->>DB: Store task
    MCP->>A2A: Find best agent for task
    A2A->>DB: Query available agents
    A2A->>Agent1: Health check (/.well-known/agent.json)
    Agent1-->>A2A: Agent card with capabilities
    A2A->>Agent1: POST /tasks (task creation)
    Agent1-->>A2A: Task accepted (HTTP 201)
    A2A->>DB: Update task status (InProgress)
    A2A-->>MCP: Task dispatched
    MCP-->>User: Task created successfully
    
    Note over Agent1,Agent2: Direct Agent Communication
    Agent1->>Agent2: GET /.well-known/agent.json
    Agent2-->>Agent1: Agent card
    Agent1->>Agent2: POST /messages (direct communication)
    Agent2-->>Agent1: Message received
    
    Note over Agent1,DB: Status Reporting
    Agent1->>A2A: PUT /tasks/{id}/status (progress update)
    A2A->>DB: Update task status
    Agent1->>A2A: PUT /tasks/{id}/status (completed)
    A2A->>DB: Mark task completed
    
    Note over User,MCP: Status Checking
    User->>MCP: get_task_status(taskId)
    MCP->>DB: Query task status
    MCP-->>User: Task status (completed)
```

### Agent Discovery

1. AgentSwarm maintains registry of known agents
2. A2A agents expose agent cards at `/.well-known/agent.json`
3. Health checking via periodic discovery calls

### Task Distribution

1. Tasks created via MCP tools
2. AgentSwarm identifies best available A2A agent
3. Direct task push via A2A protocol
4. Fallback to MCP memory channels for non-A2A agents

### Status Reporting

1. A2A agents report status via push notifications
2. AgentSwarm updates task status in database
3. MCP tools query status from database

## Agent Lifecycle

### Startup
1. AgentSwarm creates git worktree for agent
2. Copies persona files to worktree
3. Generates agent configuration file
4. Launches Gemini CLI with configuration
5. Agent starts A2A server (if enabled)

### Operation
1. Agent receives tasks via A2A push notifications
2. Processes tasks using persona-specific prompts
3. Reports status and results via A2A callbacks
4. Can communicate directly with other A2A agents

### Shutdown
1. Agent stops A2A server
2. Reports final status to AgentSwarm
3. AgentSwarm cleans up worktree

## Mixed Environment Support

The system supports both A2A-enabled and traditional MCP-only agents:

- **A2A to A2A**: Direct communication via A2A protocol
- **A2A to MCP**: Via AgentSwarm server mediation
- **MCP to A2A**: Via MCP tools with A2A dispatch
- **MCP to MCP**: Via memory channels (existing system)

## Implementation Phases

### Phase 1: A2A Client Integration
- Add A2A client service to AgentSwarm server
- Implement agent discovery and communication
- Extend database schema

### Phase 2: Gemini CLI A2A Package
- Create @aiswarm/a2a-agent NPM package
- Implement A2A server capabilities for Gemini CLI
- Agent configuration file system

### Phase 3: Task Dispatch Service
- Implement push-based task delivery
- Agent registry with health monitoring
- Mixed environment support

### Phase 4: Integration Testing
- End-to-end testing with multiple agents
- Performance validation
- Documentation updates