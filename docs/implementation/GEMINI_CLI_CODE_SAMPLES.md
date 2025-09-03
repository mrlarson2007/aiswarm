# Gemini CLI Fork - Code Implementation Samples

## 🎯 Overview

This document provides concrete code samples for implementing A2A protocol support in the forked Gemini CLI. These samples can be directly used as implementation references.

## 📁 File Structure for A2A Integration

```text
packages/cli/src/
├── a2a/
│   ├── client.ts           # A2A client implementation
│   ├── types.ts            # A2A type definitions
│   ├── protocol.ts         # A2A protocol handlers
│   └── agent-manager.ts    # Agent lifecycle management
├── commands/
│   └── a2a-command.ts      # CLI command for A2A mode
├── config/
│   └── a2a-config.ts       # A2A configuration
└── gemini.tsx              # Modified main entry point
```

## 🔧 Core Implementation Files

### 1. A2A Type Definitions

**File: `packages/cli/src/a2a/types.ts`**

```typescript
export interface A2AConfig {
  serverUrl: string;
  agentName: string;
  capabilities: string[];
  pollingInterval: number;
  timeout: number;
  maxRetries: number;
  healthCheckInterval: number;
}

export interface A2ATask {
  id: string;
  type: string;
  description: string;
  status: TaskStatus;
  createdAt: string;
  updatedAt?: string;
  assignedAgent?: string;
  priority: TaskPriority;
  input: Record<string, any>;
  output?: Record<string, any>;
  metadata: TaskMetadata;
  requiredCapabilities: string[];
  constraints?: TaskConstraints;
}

export enum TaskStatus {
  Pending = 'pending',
  InProgress = 'in-progress', 
  Completed = 'completed',
  Failed = 'failed',
  Cancelled = 'cancelled'
}

export enum TaskPriority {
  Low = 'low',
  Normal = 'normal',
  High = 'high',
  Critical = 'critical'
}

export interface TaskMetadata {
  createdBy: string;
  updatedBy?: string;
  tags: Record<string, string>;
  estimatedDuration?: string;
  deadline?: string;
}

export interface TaskConstraints {
  maxDuration?: string;
  maxRetries?: number;
  preferredAgents?: string[];
  excludedAgents?: string[];
}

export interface A2AAgent {
  id: string;
  name: string;
  type: string;
  version: string;
  status: AgentStatus;
  capabilities: string[];
  metadata: Record<string, any>;
  lastSeen: string;
  currentTask?: string;
  health: AgentHealth;
}

export enum AgentStatus {
  Available = 'available',
  Busy = 'busy', 
  Offline = 'offline',
  Error = 'error'
}

export interface AgentHealth {
  isHealthy: boolean;
  lastError?: string;
  metrics: Record<string, any>;
}

export interface A2AApiResponse<T = any> {
  success: boolean;
  data?: T;
  error?: string;
  timestamp: string;
}
```

### 2. A2A Client Implementation

**File: `packages/cli/src/a2a/client.ts`**
```typescript
import { EventEmitter } from 'events';
import { A2AConfig, A2ATask, A2AAgent, A2AApiResponse, TaskStatus, AgentStatus } from './types.js';

export class A2AClient extends EventEmitter {
  private config: A2AConfig;
  private isConnected: boolean = false;
  private isProcessingTask: boolean = false;
  private pollingTimer?: NodeJS.Timeout;
  private healthCheckTimer?: NodeJS.Timeout;
  private retryCount: number = 0;
  private currentTaskId?: string;

  constructor(config: A2AConfig) {
    super();
    this.config = {
      pollingInterval: 5000,
      timeout: 300000, // 5 minutes
      maxRetries: 3,
      healthCheckInterval: 30000, // 30 seconds
      ...config
    };
  }

  async connect(): Promise<void> {
    try {
      console.log(`🔌 Connecting to A2A server: ${this.config.serverUrl}`);
      
      // Verify server is accessible
      await this.healthCheck();
      
      // Register agent with server
      await this.registerAgent();
      
      // Start polling for tasks
      this.startTaskPolling();
      
      // Start health monitoring
      this.startHealthChecking();
      
      this.isConnected = true;
      this.retryCount = 0;
      
      console.log(`✅ Connected as agent: ${this.config.agentName}`);
      this.emit('connected', { agentName: this.config.agentName });
      
    } catch (error) {
      console.error(`❌ Connection failed: ${error.message}`);
      await this.handleConnectionError(error);
    }
  }

  async disconnect(): Promise<void> {
    console.log('🔌 Disconnecting from A2A server...');
    
    this.isConnected = false;
    
    // Clear timers
    if (this.pollingTimer) {
      clearInterval(this.pollingTimer);
      this.pollingTimer = undefined;
    }
    
    if (this.healthCheckTimer) {
      clearInterval(this.healthCheckTimer);
      this.healthCheckTimer = undefined;
    }
    
    // Update agent status to offline
    try {
      await this.updateAgentStatus(AgentStatus.Offline);
    } catch (error) {
      console.warn(`⚠️ Failed to update agent status: ${error.message}`);
    }
    
    this.emit('disconnected');
    console.log('✅ Disconnected from A2A server');
  }

  private async healthCheck(): Promise<void> {
    const response = await fetch(`${this.config.serverUrl}/health`, {
      method: 'GET',
      signal: AbortSignal.timeout(5000)
    });
    
    if (!response.ok) {
      throw new Error(`Health check failed: ${response.status} ${response.statusText}`);
    }
  }

  private async registerAgent(): Promise<void> {
    const agentData: Partial<A2AAgent> = {
      name: this.config.agentName,
      type: 'gemini-cli',
      version: '1.0.0',
      status: AgentStatus.Available,
      capabilities: this.config.capabilities,
      metadata: {
        platform: process.platform,
        nodeVersion: process.version,
        startedAt: new Date().toISOString()
      },
      health: {
        isHealthy: true,
        metrics: {
          uptime: process.uptime(),
          memoryUsage: process.memoryUsage()
        }
      }
    };

    const response = await fetch(`${this.config.serverUrl}/agents/register`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'User-Agent': `gemini-cli-a2a/${agentData.version}`
      },
      body: JSON.stringify(agentData),
      signal: AbortSignal.timeout(this.config.timeout)
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Agent registration failed: ${response.status} ${errorText}`);
    }

    const result: A2AApiResponse<A2AAgent> = await response.json();
    if (!result.success) {
      throw new Error(`Agent registration failed: ${result.error}`);
    }

    console.log(`✅ Agent registered: ${result.data?.id}`);
  }

  private startTaskPolling(): void {
    this.pollingTimer = setInterval(async () => {
      if (!this.isConnected || this.isProcessingTask) {
        return;
      }
      
      try {
        await this.checkForTasks();
      } catch (error) {
        console.error(`❌ Task polling error: ${error.message}`);
        this.emit('error', error);
      }
    }, this.config.pollingInterval);
  }

  private startHealthChecking(): void {
    this.healthCheckTimer = setInterval(async () => {
      if (!this.isConnected) {
        return;
      }
      
      try {
        await this.sendHeartbeat();
      } catch (error) {
        console.warn(`⚠️ Heartbeat failed: ${error.message}`);
      }
    }, this.config.healthCheckInterval);
  }

  private async checkForTasks(): Promise<void> {
    const response = await fetch(`${this.config.serverUrl}/tasks/pending`, {
      method: 'GET',
      headers: {
        'Content-Type': 'application/json',
        'X-Agent-Name': this.config.agentName
      },
      signal: AbortSignal.timeout(10000)
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch tasks: ${response.status}`);
    }

    const result: A2AApiResponse<A2ATask[]> = await response.json();
    if (!result.success || !result.data || result.data.length === 0) {
      return; // No tasks available
    }

    // Find suitable task based on capabilities
    const suitableTask = result.data.find(task => 
      task.requiredCapabilities.length === 0 || 
      task.requiredCapabilities.some(cap => this.config.capabilities.includes(cap))
    );

    if (suitableTask) {
      await this.claimAndProcessTask(suitableTask);
    }
  }

  private async claimAndProcessTask(task: A2ATask): Promise<void> {
    try {
      console.log(`🎯 Claiming task: ${task.id} - ${task.description}`);
      
      // Claim the task
      const claimedTask = await this.claimTask(task.id);
      
      this.isProcessingTask = true;
      this.currentTaskId = task.id;
      
      // Update agent status
      await this.updateAgentStatus(AgentStatus.Busy);
      
      // Emit task received event
      this.emit('task-received', claimedTask);
      
    } catch (error) {
      console.error(`❌ Failed to claim task ${task.id}: ${error.message}`);
      this.isProcessingTask = false;
      this.currentTaskId = undefined;
    }
  }

  async claimTask(taskId: string): Promise<A2ATask> {
    const response = await fetch(`${this.config.serverUrl}/tasks/${taskId}/claim`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Agent-Name': this.config.agentName
      },
      body: JSON.stringify({ agentId: this.config.agentName }),
      signal: AbortSignal.timeout(this.config.timeout)
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Failed to claim task: ${response.status} ${errorText}`);
    }

    const result: A2AApiResponse<A2ATask> = await response.json();
    if (!result.success) {
      throw new Error(`Failed to claim task: ${result.error}`);
    }

    return result.data!;
  }

  async completeTask(taskId: string, output: Record<string, any>): Promise<void> {
    try {
      console.log(`✅ Completing task: ${taskId}`);
      
      const response = await fetch(`${this.config.serverUrl}/tasks/${taskId}/complete`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Agent-Name': this.config.agentName
        },
        body: JSON.stringify({ 
          output,
          completedBy: this.config.agentName,
          completedAt: new Date().toISOString()
        }),
        signal: AbortSignal.timeout(this.config.timeout)
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to complete task: ${response.status} ${errorText}`);
      }

      const result: A2AApiResponse = await response.json();
      if (!result.success) {
        throw new Error(`Failed to complete task: ${result.error}`);
      }

      console.log(`✅ Task completed successfully: ${taskId}`);
      this.emit('task-completed', { taskId, output });
      
    } finally {
      this.isProcessingTask = false;
      this.currentTaskId = undefined;
      await this.updateAgentStatus(AgentStatus.Available);
    }
  }

  async failTask(taskId: string, error: string): Promise<void> {
    try {
      console.log(`❌ Failing task: ${taskId} - ${error}`);
      
      const response = await fetch(`${this.config.serverUrl}/tasks/${taskId}/fail`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Agent-Name': this.config.agentName
        },
        body: JSON.stringify({ 
          error,
          failedBy: this.config.agentName,
          failedAt: new Date().toISOString()
        }),
        signal: AbortSignal.timeout(this.config.timeout)
      });

      if (!response.ok) {
        const errorText = await response.text();
        console.error(`Failed to report task failure: ${response.status} ${errorText}`);
        return; // Don't throw here to avoid cascading failures
      }

      const result: A2AApiResponse = await response.json();
      if (!result.success) {
        console.error(`Failed to report task failure: ${result.error}`);
        return;
      }

      console.log(`❌ Task failure reported: ${taskId}`);
      this.emit('task-failed', { taskId, error });
      
    } finally {
      this.isProcessingTask = false;
      this.currentTaskId = undefined;
      await this.updateAgentStatus(AgentStatus.Available);
    }
  }

  private async updateAgentStatus(status: AgentStatus): Promise<void> {
    try {
      const response = await fetch(`${this.config.serverUrl}/agents/${this.config.agentName}/status`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          'X-Agent-Name': this.config.agentName
        },
        body: JSON.stringify({ 
          status,
          currentTask: this.currentTaskId,
          lastSeen: new Date().toISOString(),
          health: {
            isHealthy: true,
            metrics: {
              uptime: process.uptime(),
              memoryUsage: process.memoryUsage()
            }
          }
        }),
        signal: AbortSignal.timeout(5000)
      });

      if (!response.ok) {
        console.warn(`⚠️ Failed to update agent status: ${response.status}`);
      }
    } catch (error) {
      console.warn(`⚠️ Failed to update agent status: ${error.message}`);
    }
  }

  private async sendHeartbeat(): Promise<void> {
    await this.updateAgentStatus(this.isProcessingTask ? AgentStatus.Busy : AgentStatus.Available);
  }

  private async handleConnectionError(error: any): Promise<void> {
    this.retryCount++;
    
    if (this.retryCount <= this.config.maxRetries) {
      const delay = Math.pow(2, this.retryCount) * 1000; // Exponential backoff
      console.log(`🔄 Retrying connection in ${delay}ms (attempt ${this.retryCount}/${this.config.maxRetries})`);
      
      setTimeout(() => {
        this.connect();
      }, delay);
    } else {
      console.error(`❌ Max retries exceeded. Connection failed permanently.`);
      this.emit('connection-failed', error);
    }
  }
}
```

### 3. A2A CLI Command

**File: `packages/cli/src/commands/a2a-command.ts`**
```typescript
import { Command } from 'commander';
import { A2AClient } from '../a2a/client.js';
import { A2AConfig, A2ATask } from '../a2a/types.js';
import { runNonInteractiveGemini } from '../core/non-interactive.js';

export function createA2ACommand(): Command {
  const command = new Command('a2a');
  
  command
    .description('Run Gemini CLI in A2A agent mode')
    .option('--server <url>', 'A2A server URL', 'http://localhost:5001')
    .option('--agent-name <name>', 'Unique agent identifier', `gemini-agent-${Date.now()}`)
    .option('--capabilities <caps...>', 'Agent capabilities', ['code-generation', 'analysis', 'review'])
    .option('--polling-interval <ms>', 'Task polling interval in milliseconds', '5000')
    .option('--timeout <ms>', 'Request timeout in milliseconds', '300000')
    .option('--max-retries <count>', 'Maximum connection retry attempts', '3')
    .option('--health-interval <ms>', 'Health check interval in milliseconds', '30000')
    .option('--yolo', 'Skip confirmation prompts for file operations', false)
    .action(async (options) => {
      console.log('🚀 Starting Gemini CLI in A2A agent mode...');
      
      const config: A2AConfig = {
        serverUrl: options.server,
        agentName: options.agentName,
        capabilities: Array.isArray(options.capabilities) ? options.capabilities : [options.capabilities],
        pollingInterval: parseInt(options.pollingInterval),
        timeout: parseInt(options.timeout),
        maxRetries: parseInt(options.maxRetries),
        healthCheckInterval: parseInt(options.healthInterval)
      };

      const client = new A2AClient(config);

      // Set up event handlers
      client.on('connected', (data) => {
        console.log(`✅ Connected to A2A server`);
        console.log(`🤖 Agent: ${data.agentName}`);
        console.log(`🔧 Capabilities: ${config.capabilities.join(', ')}`);
        console.log(`⏰ Polling interval: ${config.pollingInterval}ms`);
        console.log(`\n🔍 Waiting for tasks...`);
      });

      client.on('task-received', async (task: A2ATask) => {
        console.log(`\n📝 Received task: ${task.id}`);
        console.log(`📋 Type: ${task.type}`);
        console.log(`📄 Description: ${task.description}`);
        console.log(`⚡ Priority: ${task.priority}`);
        
        try {
          console.log(`🔄 Processing task with Gemini...`);
          
          // Extract task parameters for Gemini CLI
          const prompt = task.input.prompt || task.description;
          const context = task.input.context || '';
          const outputPath = task.input.outputPath || './';
          
          // Process task using existing Gemini CLI functionality
          const result = await processTaskWithGemini(task, {
            prompt,
            context,
            outputPath,
            yolo: options.yolo
          });
          
          // Complete the task
          await client.completeTask(task.id, {
            result: result.output,
            files: result.files || [],
            executionTime: result.executionTime,
            generatedAt: new Date().toISOString()
          });
          
          console.log(`✅ Task completed successfully: ${task.id}`);
          console.log(`📁 Generated files: ${result.files?.length || 0}`);
          
        } catch (error) {
          console.error(`❌ Task processing failed: ${error.message}`);
          await client.failTask(task.id, error.message);
        }
        
        console.log(`\n🔍 Waiting for next task...`);
      });

      client.on('task-completed', (data) => {
        console.log(`✅ Task completion confirmed: ${data.taskId}`);
      });

      client.on('task-failed', (data) => {
        console.log(`❌ Task failure confirmed: ${data.taskId} - ${data.error}`);
      });

      client.on('error', (error) => {
        console.error(`❌ A2A Client error: ${error.message}`);
      });

      client.on('connection-failed', (error) => {
        console.error(`❌ Connection failed permanently: ${error.message}`);
        process.exit(1);
      });

      client.on('disconnected', () => {
        console.log('👋 Disconnected from A2A server');
      });

      // Handle graceful shutdown
      process.on('SIGINT', async () => {
        console.log('\n🛑 Shutting down A2A agent...');
        await client.disconnect();
        process.exit(0);
      });

      process.on('SIGTERM', async () => {
        console.log('\n🛑 Received terminate signal, shutting down...');
        await client.disconnect();
        process.exit(0);
      });

      // Connect to A2A server
      await client.connect();
    });

  return command;
}

async function processTaskWithGemini(task: A2ATask, options: {
  prompt: string;
  context: string;
  outputPath: string;
  yolo: boolean;
}): Promise<{
  output: string;
  files?: string[];
  executionTime: number;
}> {
  const startTime = Date.now();
  
  try {
    // Use existing Gemini CLI non-interactive functionality
    const result = await runNonInteractiveGemini({
      prompt: options.prompt,
      context: options.context,
      outputPath: options.outputPath,
      stream: false,
      autoConfirm: options.yolo,
      taskMetadata: {
        taskId: task.id,
        taskType: task.type,
        priority: task.priority
      }
    });
    
    const executionTime = Date.now() - startTime;
    
    return {
      output: result.response || 'Task completed successfully',
      files: result.generatedFiles || [],
      executionTime
    };
    
  } catch (error) {
    const executionTime = Date.now() - startTime;
    throw new Error(`Gemini processing failed after ${executionTime}ms: ${error.message}`);
  }
}
```

### 4. Modified Main Entry Point

**File: `packages/cli/src/gemini.tsx` (modifications)**
```typescript
// Add imports at the top
import { createA2ACommand } from './commands/a2a-command.js';

// In the main CLI setup (around line where other commands are added)
export async function main() {
  const program = new Command();
  
  // ... existing command setup ...
  
  // Add A2A command
  program.addCommand(createA2ACommand());
  
  // ... rest of existing setup ...
  
  // Parse arguments
  await program.parseAsync();
}

// Add A2A mode detection for minimal UI
if (process.argv.includes('a2a')) {
  // A2A mode - use minimal console output
  console.log('🚀 Gemini CLI - A2A Agent Mode');
  console.log('===============================');
}
```

### 5. Package.json Modifications

**File: `package.json` (add A2A-specific scripts)**
```json
{
  "name": "@aiswarm/gemini-cli",
  "version": "1.0.0-a2a.1",
  "description": "Google Gemini CLI with A2A protocol support",
  "scripts": {
    "build": "tsc && npm run build:copy-files",
    "build:a2a": "npm run build && npm run test:a2a",
    "test:a2a": "jest --testPathPattern=a2a --testTimeout=30000",
    "start:a2a": "node dist/index.js a2a",
    "start:a2a:dev": "npm run build && npm run start:a2a -- --server http://localhost:5001 --agent-name dev-agent --yolo"
  },
  "dependencies": {
    "commander": "^11.0.0",
    "@google/generative-ai": "^0.2.0"
  },
  "devDependencies": {
    "@types/node": "^20.0.0",
    "jest": "^29.0.0",
    "typescript": "^5.0.0"
  }
}
```

## 🧪 Testing Implementation

### A2A Integration Tests

**File: `packages/cli/tests/a2a.test.ts`**
```typescript
import { A2AClient } from '../src/a2a/client.js';
import { A2AConfig, TaskStatus, AgentStatus } from '../src/a2a/types.js';

describe('A2A Integration Tests', () => {
  let mockServer: MockA2AServer;
  let client: A2AClient;
  
  beforeEach(async () => {
    mockServer = new MockA2AServer();
    await mockServer.start(5002);
    
    const config: A2AConfig = {
      serverUrl: 'http://localhost:5002',
      agentName: 'test-agent',
      capabilities: ['test', 'code-generation'],
      pollingInterval: 1000,
      timeout: 5000,
      maxRetries: 1,
      healthCheckInterval: 10000
    };
    
    client = new A2AClient(config);
  });

  afterEach(async () => {
    await client.disconnect();
    await mockServer.stop();
  });

  test('should register agent successfully', async () => {
    await client.connect();
    
    expect(mockServer.getRegisteredAgents()).toContain('test-agent');
    expect(mockServer.getAgentStatus('test-agent')).toBe(AgentStatus.Available);
  });

  test('should claim and process task', async () => {
    const taskReceived = jest.fn();
    client.on('task-received', taskReceived);
    
    await client.connect();
    
    // Add task to mock server
    const task = mockServer.addTask({
      id: 'test-task-1',
      type: 'code-generation',
      description: 'Generate a simple function',
      status: TaskStatus.Pending,
      input: { prompt: 'Create a hello world function' },
      requiredCapabilities: ['code-generation']
    });
    
    // Wait for task to be claimed
    await new Promise(resolve => setTimeout(resolve, 1500));
    
    expect(taskReceived).toHaveBeenCalledWith(
      expect.objectContaining({
        id: 'test-task-1',
        status: TaskStatus.InProgress
      })
    );
  });

  test('should complete task successfully', async () => {
    await client.connect();
    
    const task = mockServer.addTask({
      id: 'test-task-2',
      type: 'test',
      description: 'Test task',
      status: TaskStatus.Pending,
      input: {},
      requiredCapabilities: ['test']
    });
    
    // Manually claim and complete task
    const claimedTask = await client.claimTask('test-task-2');
    expect(claimedTask.status).toBe(TaskStatus.InProgress);
    
    await client.completeTask('test-task-2', { result: 'Task completed' });
    
    const completedTask = mockServer.getTask('test-task-2');
    expect(completedTask?.status).toBe(TaskStatus.Completed);
    expect(completedTask?.output).toEqual({ result: 'Task completed' });
  });

  test('should handle task failure gracefully', async () => {
    await client.connect();
    
    mockServer.addTask({
      id: 'test-task-3',
      type: 'test',
      description: 'Failing task',
      status: TaskStatus.Pending,
      input: {},
      requiredCapabilities: ['test']
    });
    
    const claimedTask = await client.claimTask('test-task-3');
    await client.failTask('test-task-3', 'Simulated failure');
    
    const failedTask = mockServer.getTask('test-task-3');
    expect(failedTask?.status).toBe(TaskStatus.Failed);
  });

  test('should retry connection on failure', async () => {
    // Stop server to simulate connection failure
    await mockServer.stop();
    
    const connectionFailed = jest.fn();
    client.on('connection-failed', connectionFailed);
    
    await expect(client.connect()).rejects.toThrow();
    
    // Should attempt retries
    expect(connectionFailed).toHaveBeenCalled();
  });
});

// Mock A2A Server for testing
class MockA2AServer {
  private server: any;
  private agents: Map<string, any> = new Map();
  private tasks: Map<string, any> = new Map();
  
  async start(port: number): Promise<void> {
    const express = require('express');
    const app = express();
    app.use(express.json());
    
    // Health check
    app.get('/health', (req, res) => {
      res.json({ status: 'ok' });
    });
    
    // Agent registration
    app.post('/agents/register', (req, res) => {
      const agent = req.body;
      this.agents.set(agent.name, agent);
      res.json({ success: true, data: agent });
    });
    
    // Get pending tasks
    app.get('/tasks/pending', (req, res) => {
      const pendingTasks = Array.from(this.tasks.values())
        .filter(task => task.status === TaskStatus.Pending);
      res.json({ success: true, data: pendingTasks });
    });
    
    // Claim task
    app.post('/tasks/:id/claim', (req, res) => {
      const task = this.tasks.get(req.params.id);
      if (task && task.status === TaskStatus.Pending) {
        task.status = TaskStatus.InProgress;
        task.assignedAgent = req.body.agentId;
        res.json({ success: true, data: task });
      } else {
        res.status(400).json({ success: false, error: 'Task not available' });
      }
    });
    
    // Complete task
    app.post('/tasks/:id/complete', (req, res) => {
      const task = this.tasks.get(req.params.id);
      if (task) {
        task.status = TaskStatus.Completed;
        task.output = req.body.output;
        res.json({ success: true, data: task });
      } else {
        res.status(404).json({ success: false, error: 'Task not found' });
      }
    });
    
    // Fail task
    app.post('/tasks/:id/fail', (req, res) => {
      const task = this.tasks.get(req.params.id);
      if (task) {
        task.status = TaskStatus.Failed;
        task.error = req.body.error;
        res.json({ success: true, data: task });
      } else {
        res.status(404).json({ success: false, error: 'Task not found' });
      }
    });
    
    // Update agent status
    app.put('/agents/:name/status', (req, res) => {
      const agent = this.agents.get(req.params.name);
      if (agent) {
        Object.assign(agent, req.body);
        res.json({ success: true, data: agent });
      } else {
        res.status(404).json({ success: false, error: 'Agent not found' });
      }
    });
    
    this.server = app.listen(port);
  }
  
  async stop(): Promise<void> {
    if (this.server) {
      this.server.close();
    }
  }
  
  addTask(task: any): any {
    this.tasks.set(task.id, { ...task, createdAt: new Date().toISOString() });
    return this.tasks.get(task.id);
  }
  
  getTask(id: string): any {
    return this.tasks.get(id);
  }
  
  getRegisteredAgents(): string[] {
    return Array.from(this.agents.keys());
  }
  
  getAgentStatus(name: string): string | undefined {
    return this.agents.get(name)?.status;
  }
}
```

## 🐳 Docker Implementation

### Dockerfile for A2A Gemini CLI

**File: `Dockerfile.a2a`**
```dockerfile
FROM node:20-alpine

# Install system dependencies
RUN apk add --no-cache git curl

# Set working directory
WORKDIR /app

# Copy package files
COPY package*.json ./
COPY packages/cli/package*.json ./packages/cli/

# Install dependencies
RUN npm ci --only=production

# Copy built application
COPY packages/cli/dist/ ./packages/cli/dist/
COPY packages/cli/node_modules/ ./packages/cli/node_modules/

# Create non-root user
RUN addgroup -g 1001 -S gemini && \
    adduser -S gemini -u 1001 -G gemini

# Create directories for generated files
RUN mkdir -p /app/output && \
    chown -R gemini:gemini /app

USER gemini

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Default command - A2A mode
ENTRYPOINT ["node", "packages/cli/dist/index.js", "a2a"]

# Default arguments
CMD ["--server", "http://host.docker.internal:5001", \
     "--agent-name", "gemini-docker-agent", \
     "--capabilities", "code-generation,analysis,review", \
     "--yolo"]
```

### Docker Compose for Development

**File: `docker-compose.a2a.yml`**
```yaml
version: '3.8'

services:
  aiswarm-a2a-server:
    build:
      context: ../../../
      dockerfile: src/AISwarm.A2AServer/Dockerfile
    ports:
      - "5001:5001"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:5001
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5001/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    volumes:
      - ./data:/app/data

  gemini-a2a-agent-1:
    build:
      context: .
      dockerfile: Dockerfile.a2a
    depends_on:
      aiswarm-a2a-server:
        condition: service_healthy
    environment:
      - A2A_SERVER_URL=http://aiswarm-a2a-server:5001
      - AGENT_NAME=gemini-agent-1
      - CAPABILITIES=code-generation,python,javascript
    command: [
      "--server", "http://aiswarm-a2a-server:5001",
      "--agent-name", "gemini-agent-1", 
      "--capabilities", "code-generation,python,javascript",
      "--yolo"
    ]
    volumes:
      - ./output/agent-1:/app/output
    restart: unless-stopped

  gemini-a2a-agent-2:
    build:
      context: .
      dockerfile: Dockerfile.a2a
    depends_on:
      aiswarm-a2a-server:
        condition: service_healthy
    environment:
      - A2A_SERVER_URL=http://aiswarm-a2a-server:5001
      - AGENT_NAME=gemini-agent-2
      - CAPABILITIES=analysis,review,documentation
    command: [
      "--server", "http://aiswarm-a2a-server:5001",
      "--agent-name", "gemini-agent-2",
      "--capabilities", "analysis,review,documentation", 
      "--yolo"
    ]
    volumes:
      - ./output/agent-2:/app/output
    restart: unless-stopped

volumes:
  output:
```

## 🚀 Quick Start Scripts

### Development Setup

**File: `scripts/setup-a2a-dev.sh`**
```bash
#!/bin/bash
set -e

echo "🚀 Setting up Gemini CLI A2A development environment..."

# Check prerequisites
if ! command -v node &> /dev/null; then
    echo "❌ Node.js is required but not installed"
    exit 1
fi

if ! command -v npm &> /dev/null; then
    echo "❌ npm is required but not installed"
    exit 1
fi

# Install dependencies
echo "📦 Installing dependencies..."
npm ci

# Build the project
echo "🔨 Building project..."
npm run build

# Run tests
echo "🧪 Running A2A tests..."
npm run test:a2a

# Setup complete
echo "✅ A2A development environment ready!"
echo ""
echo "🔧 Available commands:"
echo "  npm run start:a2a:dev    - Start in development mode"
echo "  npm run test:a2a         - Run A2A tests"
echo "  npm run build:a2a        - Build and test"
echo ""
echo "🐳 Docker commands:"
echo "  docker-compose -f docker-compose.a2a.yml up -d"
echo "  docker-compose -f docker-compose.a2a.yml logs -f"
```

### Production Build Script

**File: `scripts/build-a2a-production.sh`**
```bash
#!/bin/bash
set -e

echo "🏗️ Building Gemini CLI A2A for production..."

# Clean previous builds
echo "🧹 Cleaning previous builds..."
rm -rf dist/
rm -rf node_modules/

# Install production dependencies
echo "📦 Installing production dependencies..."
npm ci --only=production

# Build application
echo "🔨 Building application..."
npm run build

# Run tests
echo "🧪 Running tests..."
npm run test:a2a

# Build Docker image
echo "🐳 Building Docker image..."
docker build -f Dockerfile.a2a -t aiswarm/gemini-cli:latest .
docker build -f Dockerfile.a2a -t aiswarm/gemini-cli:a2a-$(date +%Y%m%d) .

echo "✅ Production build complete!"
echo ""
echo "🐳 Docker images built:"
echo "  aiswarm/gemini-cli:latest"
echo "  aiswarm/gemini-cli:a2a-$(date +%Y%m%d)"
```

This comprehensive code sample provides everything needed to implement A2A support in the forked Gemini CLI, including full TypeScript implementation, testing framework, Docker support, and development scripts.