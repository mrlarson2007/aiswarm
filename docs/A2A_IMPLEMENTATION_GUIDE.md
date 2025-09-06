# A2A Implementation Guide

## Technical Implementation for Agent-to-Agent Communication

**Date:** September 5, 2025  
**Status:** Implementation Guide  
**Branch:** a2a-design-docs  

---

## Implementation Strategy

### Phase 1: A2A Client Integration

Add A2A client capabilities to AgentSwarm server for direct agent communication.

#### A2A Client Service

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AISwarm.A2A.Models;

public interface IA2AClientService
{
    Task<AgentCard> DiscoverAgentAsync(Uri agentUrl);
    Task<AgentMessage> SendMessageAsync(Uri agentUrl, AgentMessage message);
    Task<AgentTask> CreateTaskAsync(Uri agentUrl, string description, object metadata);
    Task<AgentTask> GetTaskStatusAsync(Uri agentUrl, string taskId);
    Task<AgentTask> CancelTaskAsync(Uri agentUrl, string taskId);
    Task<AgentCard?> FindBestAgentForTaskAsync(string description, string persona);
}
```

#### Database Schema Extensions

```sql
-- Add A2A capabilities to existing tables
ALTER TABLE Agents ADD COLUMN A2AUrl TEXT;
ALTER TABLE Agents ADD COLUMN A2ACapabilities TEXT; -- JSON
ALTER TABLE Agents ADD COLUMN A2ASkills TEXT; -- JSON
ALTER TABLE Agents ADD COLUMN LastHealthCheck DATETIME;

-- Task dependencies support
CREATE TABLE TaskDependencies (
    Id TEXT PRIMARY KEY,
    ParentTaskId TEXT NOT NULL,
    ChildTaskId TEXT NOT NULL,
    DependencyType TEXT DEFAULT 'blocks',
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (ParentTaskId) REFERENCES WorkItems (Id),
    FOREIGN KEY (ChildTaskId) REFERENCES WorkItems (Id)
);

-- Optional communication logging (for monitoring only)
CREATE TABLE AgentCommunicationLog (
    Id TEXT PRIMARY KEY,
    FromAgentId TEXT NOT NULL,
    ToAgentId TEXT NOT NULL,
    MessageType TEXT NOT NULL, -- 'task_creation', 'status_update', 'message'
    MessageContent TEXT,
    Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    WorkflowId TEXT
);

-- Enhanced WorkItems for dependency tracking
ALTER TABLE WorkItems ADD COLUMN DependsOnCount INTEGER DEFAULT 0;
ALTER TABLE WorkItems ADD COLUMN WorkflowId TEXT;
```

#### Agent Configuration File Format

```json
{
  "agentSwarm": {
    "agentId": "agent-uuid",
    "serverUrl": "http://localhost:5000"
  },
  "gemini": {
    "persona": "implementer",
    "personaFile": "./persona.md",
    "model": "gemini-2.0-flash-exp",
    "systemPrompt": "...",
    "temperature": 0.7
  },
  "a2a": {
    "enabled": true,
    "port": 3100
  }
}
```

#### NPM Package Structure

```
@aiswarm/a2a-agent/
├── src/
│   ├── interfaces/
│   │   ├── IAgentHost.js
│   │   ├── IGeminiProcessor.js
│   │   └── IAgentCard.js
│   ├── services/
│   │   ├── A2AAgentHost.js
│   │   ├── GeminiA2AAdapter.js
│   │   └── TaskStatusManager.js
│   ├── models/
│   │   ├── AgentTask.js
│   │   └── AgentMessage.js
│   └── index.js
├── package.json
└── README.md
```

#### Enhanced MCP Tools

- Enhance `create_task` to support task dependencies and A2A dispatch
- Enhance `list_agents` tool for A2A agent discovery with metadata
- Add `agent_message` tool for direct A2A agent communication
- Enhance `get_task_status` tool to use A2A when available
- Add `coordinate_agents` tool for workflow orchestration

### Phase 2: Gemini CLI A2A Package

Create JavaScript package for making gemini-cli agents A2A-capable.

#### Gemini CLI Agent Enhancement

```javascript
// Enhanced gemini-cli agent startup
const { A2AAgentHost } = require('@aiswarm/a2a-agent');
const { GeminiProcessor } = require('./gemini-processor');
const fs = require('fs');

class AISwarmA2AAgent {
    constructor(geminiProcessor, agentConfig) {
        this.geminiProcessor = geminiProcessor;
        this.agentConfig = agentConfig;
        this.a2aHost = null;
    }
    
    async start() {
        // Initialize A2A capabilities if enabled
        if (this.agentConfig.a2a?.enabled) {
            this.a2aHost = new A2AAgentHost({
                port: this.agentConfig.a2a.port,
                agentCard: this.getAgentCard()
            });
            
            // Handle incoming A2A tasks
            this.a2aHost.onTaskReceived = async (task) => {
                await this.processTask(task);
            };
            
            // Handle incoming A2A messages
            this.a2aHost.onMessageReceived = async (message) => {
                await this.processMessage(message);
            };
            
            await this.a2aHost.start();
            console.log(`A2A agent started on port ${this.agentConfig.a2a.port}`);
        }
        
        // Continue with standard gemini-cli operation
        await this.startGeminiProcessing();
    }
    
    getAgentCard() {
        return {
            name: this.agentConfig.agentSwarm.agentId,
            persona: this.agentConfig.gemini.persona || 'implementer',
            capabilities: ['code-generation', 'analysis', 'debugging'],
            skills: ['javascript', 'typescript', 'python', 'csharp'],
            agentSwarm: {
                serverUrl: this.agentConfig.agentSwarm.serverUrl,
                agentId: this.agentConfig.agentSwarm.agentId
            }
        };
    }
    
    async processTask(task) {
        try {
            // Process with Gemini using persona system prompt
            const result = await this.geminiProcessor.executePrompt(task.description, {
                systemPrompt: this.agentConfig.gemini.systemPrompt,
                model: this.agentConfig.gemini.model,
                temperature: this.agentConfig.gemini.temperature
            });
            
            // Report completion via A2A
            await this.a2aHost.reportTaskStatus(task.id, 'completed', {
                result: result
            });
            
        } catch (error) {
            // Report failure via A2A
            await this.a2aHost.reportTaskStatus(task.id, 'failed', {
                error: error.message
            });
        }
    }
    
    async processMessage(message) {
        // Handle direct A2A messages from other agents
        console.log(`Received A2A message from ${message.fromAgent}: ${message.content}`);
        
        // Process message and optionally respond
        if (message.requiresResponse) {
            await this.a2aHost.sendMessage(message.fromAgent, {
                type: 'response',
                content: 'Message received and processed'
            });
        }
    }
}

// Agent startup with configuration file
async function main() {
    const configPath = process.argv[2]; // Configuration file path passed by AgentSwarm
    const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
    
    const geminiProcessor = new GeminiProcessor(config.gemini);
    const agent = new AISwarmA2AAgent(geminiProcessor, config);
    
    await agent.start();
}

if (require.main === module) {
    main().catch(console.error);
}
```

### Phase 3: Task Dispatch Service

Implement push-based task delivery system.

#### A2A Task Dispatcher

```csharp
public class A2ATaskDispatchService
{
    public async Task<TaskDispatchResult> DispatchTaskAsync(WorkItem task)
    {
        // 1. Find best available A2A agent
        var agent = await _agentRegistry.FindBestAgentAsync(task.Persona, task.Priority);
        if (agent?.A2AUrl == null)
        {
            return TaskDispatchResult.NoAgentAvailable();
        }
        
        // 2. Push task directly to agent via A2A
        var a2aTask = await _a2aClient.CreateTaskAsync(
            new Uri(agent.A2AUrl), 
            task.Description,
            metadata: new { 
                taskId = task.Id, 
                persona = task.Persona,
                priority = task.Priority
            });
        
        // 3. Update local database (no delegation tracking)
        task.Status = TaskStatus.InProgress;
        task.AssignedToAgentId = agent.Id;
        await _taskRepository.UpdateAsync(task);
        
        return TaskDispatchResult.Dispatched(agent.A2AUrl, a2aTask.Id);
    }
}
```

#### Agent Discovery and Health Monitoring

```csharp
public class AgentDiscoveryService
{
    public async Task<Agent?> FindBestAgentAsync(string persona, string priority)
    {
        // Health check A2A agents and select best match
        var healthyAgents = await GetHealthyA2AAgentsAsync(persona);
        return SelectLeastBusyAgent(healthyAgents, priority);
    }
    
    public async Task<AgentCard> DiscoverAgentCardAsync(string agentUrl)
    {
        // Use well-known URI for agent discovery
        var cardUrl = $"{agentUrl}/.well-known/agent.json";
        return await _httpClient.GetFromJsonAsync<AgentCard>(cardUrl);
    }
    
    private async Task<List<Agent>> GetHealthyA2AAgentsAsync(string persona)
    {
        var agents = await _agentRepository.GetAgentsByPersonaAsync(persona);
        var healthyAgents = new List<Agent>();
        
        foreach (var agent in agents)
        {
            if (agent.A2AUrl != null && await IsAgentHealthyAsync(agent.A2AUrl))
            {
                healthyAgents.Add(agent);
            }
        }
        
        return healthyAgents;
    }
}
```

#### Agent Configuration Management

```csharp
public class A2AAgentLauncher
{
    public async Task<string> LaunchGeminiAgentAsync(string persona, int port)
    {
        // Create agent-specific git worktree
        var agentId = Guid.NewGuid().ToString();
        var worktreeName = $"aiswarm-agent-{agentId}";
        var worktreePath = await CreateGitWorktreeAsync(worktreeName);
        
        // Copy persona files to agent worktree
        await CopyPersonaFilesAsync(persona, worktreePath);
        
        // Create agent configuration file
        var configPath = await CreateAgentConfigAsync(agentId, persona, port, worktreePath);
        
        // Launch gemini-cli with configuration in worktree
        await LaunchGeminiCliAsync(configPath, worktreePath);
        
        return agentId;
    }
    
    private async Task<string> CreateGitWorktreeAsync(string worktreeName)
    {
        // Create git worktree for agent isolation
        var worktreePath = Path.Combine(_workspaceConfig.WorktreesPath, worktreeName);
        
        var processInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"worktree add \"{worktreePath}\" HEAD",
            WorkingDirectory = _workspaceConfig.RepositoryPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        
        using var process = Process.Start(processInfo);
        await process.WaitForExitAsync();
        
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Failed to create git worktree: {error}");
        }
        
        return worktreePath;
    }
    
    private async Task CopyPersonaFilesAsync(string persona, string worktreePath)
    {
        var personaSourcePath = Path.Combine(_workspaceConfig.PersonasPath, $"{persona}.md");
        var personaDestPath = Path.Combine(worktreePath, "persona.md");
        
        if (File.Exists(personaSourcePath))
        {
            var bytes = await File.ReadAllBytesAsync(personaSourcePath);
            await File.WriteAllBytesAsync(personaDestPath, bytes);
        }
        else
        {
            throw new FileNotFoundException($"Persona file not found: {personaSourcePath}");
        }
    }
    
    private async Task LaunchGeminiCliAsync(string configPath, string worktreePath)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "npx",
            Arguments = $"@google/generative-ai-cli --config \"{configPath}\"",
            WorkingDirectory = worktreePath,
            UseShellExecute = false,
            CreateNoWindow = false
        };
        
        // Launch as background process
        Process.Start(processInfo);
    }
    
    private async Task<string> CreateAgentConfigAsync(string agentId, string persona, int port, string worktreePath)
    {
        var config = new
        {
            agentSwarm = new
            {
                agentId = agentId,
                serverUrl = _agentSwarmConfig.ServerUrl // Use configured server URL
            },
            gemini = new
            {
                persona = persona,
                personaFile = Path.Combine(worktreePath, "persona.md"),
                model = GetModelForPersona(persona),
                systemPrompt = await File.ReadAllTextAsync(Path.Combine(worktreePath, "persona.md")),
                temperature = GetTemperatureForPersona(persona)
            },
            a2a = new
            {
                enabled = true,
                port = port
            }
        };
        
        var configPath = Path.Combine(worktreePath, "agent-config.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        
        return configPath;
    }
    
    private string GetModelForPersona(string persona)
    {
        // Return persona-specific model configuration
        return persona switch
        {
            "architect" => "gemini-2.0-flash-exp",
            "reviewer" => "gemini-1.5-pro",
            "implementer" => "gemini-2.0-flash-exp",
            _ => "gemini-2.0-flash-exp"
        };
    }
    
    private double GetTemperatureForPersona(string persona)
    {
        // Return persona-specific temperature settings
        return persona switch
        {
            "architect" => 0.3,  // More focused for design work
            "reviewer" => 0.1,   // Very focused for code review
            "implementer" => 0.7, // More creative for implementation
            _ => 0.7
        };
    }
}
```

### Phase 4: Integration Testing

End-to-end testing and validation.

#### Test Scenarios

- **Single Agent**: Create task → Push to agent → Receive completion via A2A
- **Multi-Agent**: Parallel task execution across multiple Gemini agents
- **Agent Discovery**: Dynamic agent discovery via well-known URIs
- **Direct Communication**: Agent-to-agent messaging without AgentSwarm mediation
- **Error Handling**: Agent failures, network issues, A2A timeouts
- **Load Testing**: Performance with 10+ concurrent Gemini CLI agents

#### Monitoring and Observability

- A2A communication logging
- Task dispatch metrics
- Agent health monitoring
- Gemini CLI agent lifecycle tracking

## Migration Strategy

### Backward Compatibility

- Keep existing MCP tools working unchanged
- Add A2A as enhancement, not replacement
- Maintain current database schema with extensions
- Support both memory channel and A2A-based agents during transition

### Rollout Plan

1. **Phase 1**: Add A2A client to server (no agent changes needed)
2. **Phase 2**: Create JavaScript A2A agent package
3. **Phase 3**: Implement task dispatch with agent configuration
4. **Phase 4**: Full A2A operation with direct agent communication

## Technical Considerations

### Agent Discovery Strategy

- Use well-known URI pattern (`/.well-known/agent.json`)
- Periodic health checking of registered agents
- Graceful fallback when agents become unavailable

### Communication Patterns

- **Push Notifications**: A2A agents receive immediate task notifications
- **Status Callbacks**: Agents report progress via A2A protocol
- **Direct Messaging**: Agent-to-agent communication without server mediation

### Error Handling

- Network failures: Retry logic with exponential backoff
- Agent unavailability: Automatic fallback to MCP memory channels
- Task failures: Clear error reporting and task reassignment

## Dependencies

- **A2A .NET SDK**: 0.3.1-preview for server-side integration
- **@a2aproject/a2a-node**: JavaScript library for Gemini CLI agents
- **Git Worktrees**: For agent isolation and configuration management
- **Entity Framework**: Database schema extensions
