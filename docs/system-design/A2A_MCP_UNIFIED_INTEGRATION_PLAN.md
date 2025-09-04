# A2A-MCP Unified Server Integration Plan

## 🎯 Strategic Overview

**Goal**: Integrate A2A server functionality directly into AISwarm.Server MCP project to create a single unified binary that aligns with A2A protocols and ecosystem standards.

**Benefits**:
- Single binary deployment (no separate A2A server)
- Unified configuration and monitoring
- Native MCP tool integration with A2A functionality
- Aligned with A2A ecosystem standards for future interoperability
- Simplified architecture and maintenance

## 🏗️ Architecture Redesign

### Current State
```
AISwarm.Server (MCP)     AISwarm.A2AServer (Separate)
├── MCP Tools           ├── TaskStore
├── HTTP Server         ├── A2A Endpoints  
├── Stdio Transport     ├── Agent Management
└── Infrastructure      └── Task Management
```

### Target State
```
AISwarm.Server (Unified MCP + A2A)
├── MCP Tools
│   ├── Existing tools
│   └── A2A Integration Tools (native)
├── Dual Transport
│   ├── Stdio (for MCP clients)
│   └── HTTP (for A2A agents + MCP HTTP)
├── A2A Protocol Layer
│   ├── Agent Card endpoint
│   ├── Task Management
│   ├── Agent Registration
│   └── WebSocket support
└── Unified Services
    ├── TaskStore
    ├── AgentManager
    └── Shared Infrastructure
```

## 📋 A2A Protocol Alignment

### 1. Task Management Schema

**Current AISwarm Schema** → **A2A-Aligned Schema**

```csharp
// Current (AISwarm specific)
public class Task
{
    public string Id { get; set; }
    public string Description { get; set; }
    public string Status { get; set; }
}

// A2A-Aligned (follows A2A standards)
public class A2ATask
{
    // Core A2A fields (standard)
    public string Id { get; set; }
    public string ContextId { get; set; }
    public TaskType Type { get; set; }
    public TaskStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Task content (A2A standard)
    public string Description { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public A2AMessage[] Messages { get; set; }
    
    // Agent assignment (A2A standard)
    public string AssignedAgentId { get; set; }
    public string[] RequiredCapabilities { get; set; }
    public TaskPriority Priority { get; set; }
    
    // Results (A2A standard)
    public TaskResult Result { get; set; }
    public string Error { get; set; }
    
    // AISwarm extensions (backward compatibility)
    public WorkspaceConfig Workspace { get; set; }
    public string CreatedBy { get; set; }
}

// A2A Standard Enums
public enum TaskType
{
    CodeGeneration,
    CodeReview, 
    Testing,
    Refactoring,
    Documentation,
    Analysis
}

public enum TaskStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public enum TaskPriority
{
    Low,
    Normal,
    High,
    Critical
}
```

### 2. Agent/Persona Schema Alignment

**Current Persona System** → **A2A Agent Card Standard**

```csharp
// Current (AISwarm persona)
public class Persona
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string[] Skills { get; set; }
}

// A2A-Aligned Agent Card (follows A2A Agent Card spec)
public class A2AAgentCard
{
    // A2A Standard fields
    public string Name { get; set; }
    public string Description { get; set; }
    public string Url { get; set; }
    public A2AProvider Provider { get; set; }
    public string ProtocolVersion { get; set; } = "0.3.0";
    public string Version { get; set; }
    
    // A2A Capabilities
    public A2ACapabilities Capabilities { get; set; }
    public A2ASkill[] Skills { get; set; }
    
    // AISwarm extensions
    public string PersonaType { get; set; } // implementer, reviewer, planner, etc.
    public DateTime LastSeen { get; set; }
    public AgentStatus Status { get; set; }
}

public class A2AProvider
{
    public string Organization { get; set; } = "AISwarm";
    public string Url { get; set; } = "https://github.com/mrlarson2007/aiswarm";
}

public class A2ACapabilities
{
    public bool Streaming { get; set; } = true;
    public bool PushNotifications { get; set; } = true;
    public bool StateTransitionHistory { get; set; } = true;
}

public class A2ASkill
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string[] Tags { get; set; }
    public string[] Examples { get; set; }
    public string[] InputModes { get; set; }
    public string[] OutputModes { get; set; }
}
```

### 3. Message Protocol Alignment

```csharp
// A2A Standard Message format
public class A2AMessage
{
    public string Kind { get; set; } = "message";
    public string Role { get; set; } // "user" | "agent"
    public A2APart[] Parts { get; set; }
    public string MessageId { get; set; }
    public string TaskId { get; set; }
    public string ContextId { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public class A2APart
{
    public string Kind { get; set; } // "text" | "code" | "file"
    public string Text { get; set; }
    public string Language { get; set; } // for code parts
    public string FilePath { get; set; } // for file parts
}
```

## 🔧 Implementation Strategy

### Phase 1: Schema Migration (Week 1)
1. **Create A2A-aligned models** in AISwarm.Shared
2. **Migrate existing task data** to new schema
3. **Update MCP tools** to use A2A schema
4. **Maintain backward compatibility** with existing APIs

### Phase 2: Service Integration (Week 1-2)
1. **Move TaskStore** from A2AServer to AISwarm.Server
2. **Add A2A endpoints** to existing HTTP server
3. **Implement Agent Card** endpoint (/.well-known/agent-card.json)
4. **Add agent management** services

### Phase 3: Protocol Compliance (Week 2)
1. **A2A message handling** (JSON-RPC 2.0 compatible)
2. **WebSocket support** for real-time communication
3. **Standard A2A endpoints** (/tasks, /agents, /messages)
4. **Agent Card compliance** with A2A spec

### Phase 4: MCP Tool Enhancement (Week 2-3)
1. **Native A2A MCP tools** (CreateA2ATask, MonitorA2ATask)
2. **Agent management tools** (ListAgents, AssignTask)
3. **Unified workflow** (MCP → A2A → Agent → Results)

## 📁 Project Structure Changes

### File Organization
```
src/AISwarm.Server/
├── Program.cs (enhanced with A2A endpoints)
├── Services/
│   ├── A2A/
│   │   ├── IA2ATaskService.cs
│   │   ├── A2ATaskService.cs
│   │   ├── IAgentManager.cs
│   │   ├── AgentManager.cs
│   │   └── A2AMessageHandler.cs
│   └── (existing services)
├── McpTools/
│   ├── A2A/
│   │   ├── A2ATaskManagementTools.cs
│   │   ├── A2AAgentTools.cs
│   │   └── A2AMonitoringTools.cs
│   └── (existing tools)
├── Endpoints/
│   ├── A2AEndpoints.cs
│   ├── AgentCardEndpoint.cs
│   └── WebSocketEndpoints.cs
└── Models/
    └── A2A/
        ├── A2ATask.cs
        ├── A2AAgentCard.cs
        ├── A2AMessage.cs
        └── A2AProtocolModels.cs

src/AISwarm.Shared/
├── Models/
│   └── A2A/ (shared models)
└── Interfaces/
    └── A2A/ (shared interfaces)
```

### Configuration Integration
```json
{
  "AISwarm": {
    "MCP": {
      "Transport": "dual", // stdio + http
      "StdioEnabled": true,
      "HttpEnabled": true,
      "HttpPort": 5000
    },
    "A2A": {
      "Enabled": true,
      "ProtocolVersion": "0.3.0",
      "AgentCard": {
        "Name": "AISwarm Agent",
        "Description": "Multi-modal AI development agent",
        "Provider": {
          "Organization": "AISwarm",
          "Url": "https://github.com/mrlarson2007/aiswarm"
        }
      },
      "WebSocket": {
        "Enabled": true,
        "Path": "/ws"
      },
      "TaskManagement": {
        "MaxConcurrentTasks": 50,
        "TaskTimeoutMinutes": 30,
        "RetryAttempts": 3
      }
    }
  }
}
```

## 🔌 MCP Tool Integration

### New A2A-Integrated MCP Tools
```csharp
[McpServerToolType]
public class A2AIntegrationTools
{
    private readonly IA2ATaskService _taskService;
    private readonly IAgentManager _agentManager;

    [McpServerTool]
    public async Task<TaskCreationResult> CreateA2ATask(
        [Description("Task description")]
        string description,
        
        [Description("Task type: code-generation, code-review, testing")]
        string taskType = "code-generation",
        
        [Description("Required agent capabilities")]
        string[] capabilities = null,
        
        [Description("Task priority")]
        string priority = "normal",
        
        [Description("Workspace directory")]
        string workspaceDir = null)
    {
        var task = await _taskService.CreateTaskAsync(new CreateA2ATaskRequest
        {
            Description = description,
            Type = Enum.Parse<TaskType>(taskType, true),
            RequiredCapabilities = capabilities ?? new[] { "code-generation" },
            Priority = Enum.Parse<TaskPriority>(priority, true),
            Workspace = new WorkspaceConfig { Directory = workspaceDir }
        });
        
        return new TaskCreationResult
        {
            TaskId = task.Id,
            Status = task.Status.ToString(),
            EstimatedDuration = "2-5 minutes",
            AssignedAgent = await _agentManager.FindBestAgentAsync(task)
        };
    }

    [McpServerTool]
    public async Task<TaskStatusResult> GetA2ATaskStatus(
        [Description("Task ID to check")]
        string taskId)
    {
        var task = await _taskService.GetTaskAsync(taskId);
        return new TaskStatusResult
        {
            TaskId = task.Id,
            Status = task.Status.ToString(),
            Progress = await _taskService.GetTaskProgressAsync(taskId),
            Result = task.Result,
            Error = task.Error
        };
    }

    [McpServerTool]
    public async Task<AgentInfo[]> ListA2AAgents(
        [Description("Filter by capabilities")]
        string[] capabilities = null)
    {
        var agents = await _agentManager.GetAvailableAgentsAsync(capabilities);
        return agents.Select(a => new AgentInfo
        {
            Id = a.Id,
            Name = a.Name,
            Status = a.Status.ToString(),
            Capabilities = a.Skills.Select(s => s.Name).ToArray(),
            LastSeen = a.LastSeen
        }).ToArray();
    }
}
```

## 🌐 A2A Ecosystem Compatibility

### Standard Compliance
- **A2A Protocol 0.3.0** compliance for interoperability
- **Agent Card specification** for agent discovery
- **JSON-RPC 2.0** message format
- **Standard HTTP endpoints** following A2A conventions

### Future Ecosystem Integration
- **Agent marketplace** compatibility
- **Cross-platform agent discovery**
- **Standard task exchange** with other A2A systems
- **Protocol evolution** support

### Interoperability Benefits
- **Gemini CLI agents** can connect to any A2A-compliant system
- **Other A2A agents** can connect to AISwarm
- **Task exchange** between different A2A implementations
- **Ecosystem growth** through standardization

## 🚀 Migration Path

### Backward Compatibility
- **Existing MCP tools** continue to work unchanged
- **Current task format** automatically converted to A2A schema
- **Gradual migration** of features to A2A-aligned versions
- **Feature flags** for enabling/disabling A2A functionality

### Deployment Strategy
1. **Development**: Test unified server with both MCP and A2A protocols
2. **Staging**: Validate backward compatibility and new features
3. **Production**: Single binary deployment with feature flags
4. **Migration**: Gradual enablement of A2A features

## 📊 Success Metrics

### Technical Goals
- **Single binary** replaces two separate servers
- **Protocol compliance** with A2A specification
- **Performance** maintains or improves current metrics
- **Compatibility** preserves all existing functionality

### Ecosystem Goals
- **Agent interoperability** with standard A2A agents
- **Task portability** between A2A systems
- **Future-proofing** for A2A ecosystem evolution
- **Community adoption** through standards compliance

This plan positions AISwarm as a first-class citizen in the A2A ecosystem while maintaining full backward compatibility and improving deployment simplicity.